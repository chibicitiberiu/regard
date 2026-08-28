using MoreLinq;
using Microsoft.Extensions.Logging;
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
    #region Events

    public class VideoUpdatedEventArgs
    { 
        /// <summary>
        /// User who initiated the operation
        /// </summary>
        public UserAccount User { get; set; }

        /// <summary>
        /// Updated video
        /// </summary>
        public Video Video { get; set; }
    }


    #endregion

    public class VideoManager
    {
        private readonly DataContext dataContext;
        private readonly RegardScheduler scheduler;
        private readonly IProviderManager providerManager;
        private readonly IOptionManager optionManager;
        private readonly ILogger<VideoManager> log;

        public event EventHandler<VideoUpdatedEventArgs> VideoUpdated;

        public VideoManager(DataContext dataContext,
                            RegardScheduler scheduler,
                            IProviderManager providerManager,
                            IOptionManager optionManager,
                            ILogger<VideoManager> log)
        {
            this.dataContext = dataContext;
            this.scheduler = scheduler;
            this.providerManager = providerManager;
            this.optionManager = optionManager;
            this.log = log;
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

        public void Update(UserAccount user, int[] videoIds, Action<Video> updateMethod)
        {
            var vids = dataContext.Videos.AsQueryable()
                .Where(v => videoIds.Contains(v.Id))
                .Where(v => v.Subscription.UserId == user.Id);
                
            vids.ForEach(updateMethod);
            dataContext.SaveChanges();

            if (VideoUpdated != null)
            {
                foreach (var video in vids)
                    VideoUpdated.Invoke(this, new VideoUpdatedEventArgs() { User = user, Video = video });
            }
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

            // Forward auto-delete: delete downloaded files (and refill) for subs that opt in.
            var toDelete = dataContext.Videos.AsQueryable()
                .Where(v => videoIds.Contains(v.Id))
                .Where(v => v.Subscription.UserId == user.Id)
                .Where(v => v.DownloadedPath != null)
                .ToList()
                .Where(v => optionManager.GetForSubscription(Options.Subscriptions_DeleteWatched, v.SubscriptionId))
                .Select(v => v.Id)
                .ToArray();

            if (toDelete.Length > 0)
                await DeleteWatchedFilesJob.Schedule(scheduler, toDelete);
        }

        public async Task Download(UserAccount user, int[] videoIds)
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
                await DownloadVideoJob.Schedule(scheduler, video);
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
            // TODO: send notification
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
