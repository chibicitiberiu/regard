using MoreLinq;
using Microsoft.Extensions.Logging;
using Regard.Backend.Common.Providers;
using Regard.Backend.Common.Utils;
using Regard.Backend.Configuration;
using Regard.Backend.DB;
using Regard.Backend.Downloader;
using Regard.Backend.Jobs;
using Regard.Backend.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    // VideoUpdatedEventArgs / the VideoUpdated event were removed: the event was declared but never
    // raised anywhere, and its only would-be subscriber was a bridge that was never instantiated. Live
    // updates come from the EF change feed (Services/LiveUpdates/ChangeFeedInterceptor).


    public class VideoManager
    {
        private readonly DataContext dataContext;
        private readonly RegardScheduler scheduler;
        private readonly IProviderManager providerManager;
        private readonly IOptionManager optionManager;
        private readonly ILogger<VideoManager> log;
        private readonly HostThrottle hostThrottle;
        private readonly IVideoStorageService videoStorage;

        public VideoManager(DataContext dataContext,
                            RegardScheduler scheduler,
                            IProviderManager providerManager,
                            IOptionManager optionManager,
                            ILogger<VideoManager> log,
                            HostThrottle hostThrottle,
                            IVideoStorageService videoStorage)
        {
            this.dataContext = dataContext;
            this.scheduler = scheduler;
            this.providerManager = providerManager;
            this.optionManager = optionManager;
            this.log = log;
            this.hostThrottle = hostThrottle;
            this.videoStorage = videoStorage;
        }

        public Video Get(int id)
        {
            return dataContext.Videos.Find(id);
        }

        public IQueryable<Video> GetAll(UserAccount userAccount)
        {
            return dataContext.Videos.AsQueryable()
                .Where(x => x.Subscription.UserId == userAccount.Id);
        }

        /// <summary>
        /// Records vote data fetched from Return YouTube Dislike onto the video.
        ///
        /// Exists because <c>VideoController</c> has no DataContext of its own: mutating the tracked
        /// entity there would leave the change tracked but never flushed, so the counts would look right
        /// for that one response and silently persist nothing.
        ///
        /// <paramref name="rating"/> is the 0..1 liked ratio, NOT RYD's <c>rating</c> field — that one is
        /// YouTube's legacy 1..5 star average (~4.9 for a well-liked video) and storing it here would
        /// render as 24 stars and produce negative derived dislikes.
        ///
        /// Best-effort: never throws, because this runs inside a read request that must still return.
        /// </summary>
        public async Task SetVotes(Video video, long? likes, float? rating)
        {
            if (video == null)
                return;

            try
            {
                // Assign and let EF decide: unchanged numbers mean no modified property, so a repeat
                // watch-page open writes nothing and broadcasts nothing.
                video.Likes = likes ?? video.Likes;
                video.Rating = rating ?? video.Rating;
                await dataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Could not persist vote data for video {0}", video.Id);
            }
        }

        public void Update(UserAccount user, int[] videoIds, Action<Video> updateMethod)
        {
            var vids = dataContext.Videos.AsQueryable()
                .Where(v => videoIds.Contains(v.Id))
                .Where(v => v.Subscription.UserId == user.Id)
                .ToList();

            vids.ForEach(updateMethod);
            dataContext.SaveChanges();
        }

        /// <summary>
        /// Marks the given videos as watched. For downloaded videos whose subscription has
        /// Subscriptions_DeleteWatched enabled, deletes the files and refills the download window.
        /// </summary>
        /// <summary>
        /// Writes a resume position for a single video. Deliberately lightweight: a single targeted UPDATE
        /// with no VideoUpdated event, because this is high-frequency playback telemetry (every few seconds)
        /// and must not fan out through ToApi + SignalR on each tick. Owner-scoped.
        /// <paramref name="updatedAt"/> defaults to now; pass an explicit value when adopting an external
        /// (Jellyfin) position so the timestamp reflects that source rather than "just now".
        /// </summary>
        public void SetPlaybackPosition(UserAccount user, int videoId, int seconds, int? durationSeconds = null, DateTimeOffset? updatedAt = null)
        {
            var video = dataContext.Videos.AsQueryable()
                .Where(v => v.Id == videoId && v.Subscription.UserId == user.Id)
                .FirstOrDefault();
            // Never store a resume point for an already-watched video: a late progress report (racing the
            // ~90% mark-watched, which clears the position) must not resurrect one.
            if (video == null || video.IsWatched)
                return;

            video.PlaybackPositionSeconds = seconds;
            video.PlaybackPositionUpdated = updatedAt ?? DateTimeOffset.Now;

            // Backfill duration from the player when it's known but wasn't captured during enrichment.
            // Without a duration the resume progress bar can't be drawn (it needs position/duration).
            if (durationSeconds.HasValue && durationSeconds.Value > 0 && (video.Duration == null || video.Duration <= 0))
                video.Duration = durationSeconds.Value;

            dataContext.SaveChanges();
        }

        public async Task MarkWatched(UserAccount user, int[] videoIds)
        {
            // Set the flag (+ notify the frontend) via the shared update path. A finished video restarts
            // from 0, so clear any resume position at the same time.
            Update(user, videoIds, video => { video.IsWatched = true; video.PlaybackPositionSeconds = null; });

            // Forward auto-delete for subs that opt in: either schedule a grace-period deletion (mark now,
            // the sweep deletes later) or delete immediately when grace == 0 (legacy behavior).
            var candidates = dataContext.Videos.AsQueryable()
                .Where(v => videoIds.Contains(v.Id))
                .Where(v => v.Subscription.UserId == user.Id)
                .Where(v => v.DownloadedPath != null)
                .ToList()
                .Where(v => optionManager.GetForSubscription(Options.Subscriptions_DeleteWatched, v.SubscriptionId));

            await ScheduleOrMarkForDeletion(candidates, user.Id);
        }

        /// <summary>
        /// For each video, either mark it for deletion after the subscription's grace period
        /// (DeleteScheduledAt = now + grace) or, when grace == 0, delete immediately (+ refill).
        /// </summary>
        private async Task ScheduleOrMarkForDeletion(IEnumerable<Video> videos, string userId)
        {
            var deleteNow = new List<Video>();
            var scheduled = new List<Video>();
            foreach (var v in videos)
            {
                int grace = optionManager.GetForSubscription(Options.Subscriptions_DeleteGracePeriod, v.SubscriptionId);
                if (grace <= 0)
                {
                    deleteNow.Add(v);
                }
                else
                {
                    v.DeleteScheduledAt = DateTimeOffset.Now.AddMinutes(grace);
                    scheduled.Add(v);
                }
            }

            // Immediate-delete path (grace 0): apply the MarkDeletedAsWatched reverse rule to unwatched
            // videos first, so a freed slot doesn't immediately re-download them — matching what the sweep
            // and manual DeleteFiles do. (On-watch videos are already watched, so this is a no-op for them.)
            bool changed = scheduled.Count > 0;
            foreach (var v in deleteNow)
            {
                if (!v.IsWatched
                    && optionManager.GetForSubscription(Options.Subscriptions_MarkDeletedAsWatched, v.SubscriptionId))
                {
                    v.IsWatched = true;
                    changed = true;
                }
            }

            if (changed)
                dataContext.SaveChanges();

            if (deleteNow.Count > 0)
                await DeleteWatchedFilesJob.Schedule(scheduler, deleteNow.Select(v => v.Id).ToArray());
        }

        /// <summary>
        /// Manually schedule the user's downloaded videos for deletion after the grace period (or delete
        /// immediately if grace == 0). Same mechanism as the on-watch path, but user-triggered.
        /// </summary>
        public async Task MarkForDeletion(UserAccount user, int[] videoIds)
        {
            var vids = dataContext.Videos.AsQueryable()
                .Where(v => videoIds.Contains(v.Id))
                .Where(v => v.Subscription.UserId == user.Id)
                .Where(v => v.DownloadedPath != null)
                .ToList();

            await ScheduleOrMarkForDeletion(vids, user.Id);
        }

        /// <summary>
        /// Cancel a scheduled deletion ("Unmark for deletion"). The video is retained and keeps counting
        /// toward the subscription quota (so the window won't refill to replace it).
        /// </summary>
        public void UnmarkForDeletion(UserAccount user, int[] videoIds)
        {
            Update(user, videoIds, v => v.DeleteScheduledAt = null);
        }

        /// <summary>
        /// Queues a manual download. <paramref name="force"/> is the "Download again" case: the job
        /// re-fetches even when the video is already marked downloaded, deleting its files first. Only
        /// user-initiated calls pass it — auto-download and restart reconciliation stay on the
        /// already-downloaded no-op, which is what keeps them idempotent.
        /// </summary>
        public async Task Download(UserAccount user, int[] videoIds, bool force = false)
        {
            // This verifies that only user's videos are downloaded
            var videosToDownload = dataContext.Videos.AsQueryable()
                .Where(v => videoIds.Contains(v.Id))
                .Where(v => v.Subscription.UserId == user.Id)
                .ToList();

            // A manual download clears a prior cancel/skip so the video is no longer excluded.
            bool anyUnskipped = false;
            foreach (var video in videosToDownload)
                if (video.DownloadSkipped)
                {
                    video.DownloadSkipped = false;
                    anyUnskipped = true;
                }
            if (anyUnskipped)
                await dataContext.SaveChangesAsync();

            // Un-enriched (flat) videos are enriched in DownloadVideoJob before the download, so every
            // path (manual + auto) is covered there — no need to enrich here.
            foreach (var video in videosToDownload)
            {
                // A forced re-download deletes the video's files before fetching, so two of them racing
                // would have the second wipe what the first just finished. The auto-download path already
                // dedups on this registry (VideoDownloaderService.ProcessDownloadRules); borrow it here
                // for the destructive case only, so an impatient double-click can't cost the user a file.
                // The entry is released by DownloadVideoJob.OnAfterExecute.
                if (force)
                {
                    if (hostThrottle.IsKnown(video.Id))
                    {
                        log.LogInformation("videoId={0}: re-download already queued, ignoring duplicate request", video.Id);
                        continue;
                    }
                    hostThrottle.MarkKnown(video.Id);
                }

                await DownloadVideoJob.Schedule(scheduler, video, force);
            }
        }

        /// <summary>
        /// Re-fetches one video's metadata from its provider, right now, ignoring the age-based refresh
        /// schedule. Shared by the background refresh job and the user-initiated "Refresh metadata"
        /// action, so the two can't drift.
        ///
        /// Costs one paced yt-dlp extraction, so callers are responsible for rationing it — this is the
        /// expensive half of a refresh. Returns false (and logs) on failure rather than throwing, so a
        /// batch caller can carry on with the rest.
        /// </summary>
        public async Task<bool> RefreshMetadataNow(Video video)
        {
            if (video == null)
                return false;

            try
            {
                // Look the provider up by id rather than asking each one "can you handle this?":
                // IProviderManager.FindForVideo probes via CanHandleVideo, which YouTubeDLProvider answers
                // with a full paced extraction — doubling the cost of every refresh.
                var provider = providerManager.Get<IVideoProvider>(video.VideoProviderId)
                    ?? await providerManager.FindForVideo(video).FirstOrDefaultAsync();
                if (provider == null)
                {
                    log.LogWarning("No video provider for {0} (providerId={1})", video, video.VideoProviderId);
                    return false;
                }

                await provider.UpdateMetadata(new[] { video }, true, true);
                await dataContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Could not refresh metadata for video {0}", video.Id);
                return false;
            }
        }

        /// <summary>
        /// Queues a metadata refresh for videos the user owns. One job per video, each costing a paced
        /// extraction — the endpoint is per-video precisely so this can't be aimed at the whole library.
        /// </summary>
        public async Task<int> RefreshMetadata(UserAccount user, int[] videoIds)
        {
            var videos = dataContext.Videos.AsQueryable()
                .Where(v => videoIds.Contains(v.Id))
                .Where(v => v.Subscription.UserId == user.Id)
                .ToList();

            foreach (var video in videos)
                await RefreshVideoMetadataJob.Schedule(scheduler, video);

            return videos.Count;
        }

        /// <summary>
        /// Queues a sidecar refetch (subtitles + a metadata refresh from the same extraction) for videos
        /// the user owns. Videos that aren't downloaded are dropped here rather than queued to no-op.
        /// </summary>
        public async Task<int> Reprocess(UserAccount user, int[] videoIds, bool auto = false)
        {
            var videos = dataContext.Videos.AsQueryable()
                .Where(v => videoIds.Contains(v.Id))
                .Where(v => v.Subscription.UserId == user.Id)
                .Where(v => v.DownloadedPath != null)
                .ToList();

            foreach (var video in videos)
                await ReprocessVideoJob.Schedule(scheduler, video, auto);

            return videos.Count;
        }

        /// <summary>
        /// Queues a subtitle refetch for every downloaded video in a subscription that is actually
        /// missing one. Returns (queued, alreadyComplete).
        ///
        /// The "is anything missing?" test reads the disk rather than the database — subtitles are
        /// discovered from sidecar files, not stored as a column — so this is O(downloaded videos) file
        /// enumerations, not O(library).
        /// </summary>
        public async Task<(int Queued, int AlreadyComplete)> ReprocessSubscription(
            UserAccount user, int subscriptionId, bool auto = false, int? limit = null)
        {
            var downloaded = dataContext.Videos.AsQueryable()
                .Where(v => v.SubscriptionId == subscriptionId)
                .Where(v => v.Subscription.UserId == user.Id)
                .Where(v => v.DownloadedPath != null)
                .Where(v => !v.SponsorsRemoved)   // freshly-fetched cues wouldn't line up with a cut file
                .ToList();

            int queued = 0, complete = 0;
            foreach (var video in downloaded)
            {
                var present = (await videoStorage.GetSubtitleFiles(video)).Select(s => s.Lang).ToList();
                bool needs = SubtitleNeeds.NeedsSubtitles(
                    present,
                    optionManager.GetForSubscription(Options.Ytdl_SubLang, video.SubscriptionId),
                    optionManager.GetForSubscription(Options.Ytdl_WriteSubtitles, video.SubscriptionId),
                    optionManager.GetForSubscription(Options.Ytdl_WriteAutoSub, video.SubscriptionId),
                    optionManager.GetForSubscription(Options.Ytdl_AllSubs, video.SubscriptionId));

                if (!needs)
                {
                    complete++;
                    continue;
                }

                if (limit.HasValue && queued >= limit.Value)
                    break;

                await ReprocessVideoJob.Schedule(scheduler, video, auto);
                queued++;
            }

            return (queued, complete);
        }

        /// <summary>
        /// Fetches full metadata for a video that was listed flat during sync (EnrichedAt == null) and
        /// persists it. No-op for already-enriched videos. Best-effort: logs and leaves it flat on error.
        /// </summary>
        public async Task EnsureEnriched(Video video)
        {
            if (video.EnrichedAt != null)
                return;

            var provider = await providerManager.FindForVideo(video).FirstOrDefaultAsync();
            if (provider == null)
                return;

            try
            {
                await provider.UpdateMetadata(new[] { video }, true, true);
                video.EnrichedAt = DateTimeOffset.UtcNow;
                await dataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Could not enrich metadata for video {0}", video);
            }
        }

        public async Task DeleteFiles(UserAccount user, int[] videoIds)
        {
            // This verifies that only user's videos are deleted
            var vids = dataContext.Videos.AsQueryable()
                .Where(v => videoIds.Contains(v.Id))
                .Where(v => v.Subscription.UserId == user.Id)
                .ToList();

            // Reverse: for subscriptions that opt in, mark the video watched so it isn't re-downloaded.
            bool changed = false;
            foreach (var video in vids)
            {
                if (optionManager.GetForSubscription(Options.Subscriptions_MarkDeletedAsWatched, video.SubscriptionId))
                {
                    video.IsWatched = true;
                    changed = true;
                }
            }
            if (changed)
                dataContext.SaveChanges();

            await DeleteFilesJob.Schedule(scheduler, vids.Select(v => v.Id).ToArray());
        }

        public async Task Add(UserAccount user, Uri url, int subscriptionId)
        {
            var sub = dataContext.Subscriptions.Find(subscriptionId);
            if (sub == null)
                throw new ArgumentException("Invalid subscription ID!");

            if (sub.UserId != user.Id)
                throw new UnauthorizedAccessException("Not authorized to modify subscription!");

            var video = new Video() 
            { 
                OriginalUrl = url.ToString(),
                Subscription = sub,
            };

            var provider = await providerManager.FindForVideo(video).FirstOrDefaultAsync();
            if (provider == null)
                throw new Exception("Invalid/unsupported URL");

            await provider.UpdateMetadata(Enumerable.Repeat(video, 1), true, true);
            dataContext.Videos.Add(video);
            await dataContext.SaveChangesAsync();
        }

        public async Task ValidateUrl(Uri url)
        {
            var video = new Video() { OriginalUrl = url.ToString() };
            bool found = await providerManager.FindForVideo(video).AnyAsync();

            if (!found)
                throw new Exception("Invalid/unsupported URL");
        }

        public void OnDownloadProgress(int videoId, float percent)
        {
            // TODO
        }
    }
}
