using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;
using Regard.Backend.Common.Model;
using Regard.Backend.Common.Providers;
using Regard.Backend.Common.Utils;
using Regard.Backend.Configuration;
using Regard.Backend.DB;
using Regard.Backend.Model;
using Regard.Backend.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Jobs
{
    /// <summary>
    /// Keeps view counts, like counts and titles from going stale, and backfills the like ratio that
    /// "Highest rated" sorting depends on.
    ///
    /// Two things shape this job, and both are about spending a very small budget well:
    ///
    /// **It is the lowest-priority thing in the system.** yt-dlp extractions and downloads share the
    /// throttle's per-host pacing floor, so every extraction this job makes pushes a waiting download
    /// further out — and the hour/day caps would not stop it, because they only count downloads. So it
    /// stands down entirely while a download is running or queued, and stops mid-batch if one appears.
    ///
    /// **Not every video deserves the same attention.** Metadata moves fast for a few days after
    /// publication and then barely at all, so <see cref="RefreshSchedule"/> gives each video its own
    /// interval from its age, and among the videos that are due the newest go first.
    ///
    /// Deliberately absent from RegardScheduler.NotifiableJobTypes: this is background maintenance, like
    /// ProcessScheduledDeletionsJob, and has no business in the notification bell.
    /// </summary>
    [DisallowConcurrentExecution]
    public class RefreshMetadataJob : JobBase
    {
        private readonly IOptionManager optionManager;
        private readonly IProviderManager providerManager;
        private readonly HostThrottle hostThrottle;
        private readonly ReturnYouTubeDislikeClient rydClient;
        private readonly VideoManager videoManager;
        private readonly RegardScheduler scheduler;

        /// <summary>Self-pacing between RYD calls. Their published limit is 100/min; this sits well under.</summary>
        private static readonly TimeSpan RydDelay = TimeSpan.FromMilliseconds(750);

        public RefreshMetadataJob(ILogger<RefreshMetadataJob> log,
                                  DataContext dataContext,
                                  JobTrackerService jobTrackerService,
                                  IOptionManager optionManager,
                                  IProviderManager providerManager,
                                  HostThrottle hostThrottle,
                                  ReturnYouTubeDislikeClient rydClient,
                                  VideoManager videoManager,
                                  RegardScheduler scheduler) : base(log, dataContext, jobTrackerService)
        {
            this.optionManager = optionManager;
            this.providerManager = providerManager;
            this.hostThrottle = hostThrottle;
            this.rydClient = rydClient;
            this.videoManager = videoManager;
            this.scheduler = scheduler;
        }

        public static Task<DateTimeOffset> Schedule(RegardScheduler scheduler, DateTimeOffset start, TimeSpan interval)
        {
            return scheduler.Schedule<RefreshMetadataJob>(
                name: "Refresh video metadata",
                start: start,
                repeatInterval: interval,
                retryCount: 0);
        }

        /// <summary>
        /// Stand down while real work is happening. Returning a time reschedules the trigger and frees
        /// the Quartz worker (there are only three), with no notification and nothing dropped.
        /// </summary>
        protected override Task<DateTimeOffset?> ShouldDefer(IJobExecutionContext context)
        {
            if (!optionManager.GetGlobal(Options.Server_MetadataRefresh_Enabled))
                return Task.FromResult<DateTimeOffset?>(DateTimeOffset.UtcNow.AddHours(1));

            if (IsBusy(out string reason))
            {
                log.LogDebug("Metadata refresh deferred: {0}", reason);
                return Task.FromResult<DateTimeOffset?>(DateTimeOffset.UtcNow.AddMinutes(10));
            }

            return Task.FromResult<DateTimeOffset?>(null);
        }

        /// <summary>
        /// True when something more important is using the extraction budget: a download in flight or
        /// waiting on any host, or a sync actually running. A sync's recurring row sits in Scheduled
        /// between runs, so only Running counts — testing Scheduled would defer forever.
        /// </summary>
        private bool IsBusy(out string reason)
        {
            foreach (var status in hostThrottle.GetStatus())
            {
                if (status.InFlight > 0 || status.Queued > 0)
                {
                    reason = $"{status.Host} has {status.InFlight} download(s) in flight and {status.Queued} queued";
                    return true;
                }
            }

            bool syncing = dataContext.Jobs.AsQueryable()
                .Any(j => j.Key == nameof(SynchronizeJob) && j.State == JobState.Running);
            if (syncing)
            {
                reason = "a subscription sync is running";
                return true;
            }

            reason = null;
            return false;
        }

        protected override async Task ExecuteJob(IJobExecutionContext context)
        {
            var now = DateTimeOffset.UtcNow;

            int rydDone = await RefreshRatings(now);
            int metaDone = await RefreshMetadata(now);
            int swept = await SweepMissingSubtitles();

            string summary = $"Refreshed {metaDone} video(s), {rydDone} rating(s)"
                           + (swept > 0 ? $", queued {swept} subtitle fetch(es)." : ".");
            log.LogInformation(summary);
            if (metaDone > 0 || rydDone > 0 || swept > 0)
                JobLog(summary);
        }

        /// <summary>
        /// The cheap half. Return YouTube Dislike is a plain HTTP GET against a different host, outside
        /// the yt-dlp throttle entirely, so it can cover far more videos per run.
        ///
        /// Videos with no rating at all go first. That is the backfill that makes the "Highest rated"
        /// sort mean something: Video.Rating is only ever set by RYD, and until now only for videos whose
        /// watch page someone happened to open, so a handful of arbitrary rows floated to the top while
        /// everything else tied at null.
        /// </summary>
        private async Task<int> RefreshRatings(DateTimeOffset now)
        {
            int budget = optionManager.GetGlobal(Options.Server_MetadataRefresh_RydBatchSize);
            if (budget <= 0 || !optionManager.GetGlobal(Options.ReturnYouTubeDislike_Enabled))
                return 0;

            // DateTimeOffset can't be ordered by SQLite, so the ordering happens in memory.
            var candidates = dataContext.Videos.AsQueryable()
                .Where(v => v.EnrichedAt != null)
                .AsEnumerable()
                .Where(v => VideoEmbedHelper.IsYouTube(v))
                .OrderBy(v => v.Rating.HasValue ? 1 : 0)          // never-rated first
                .ThenByDescending(v => v.Published)               // then newest
                .Take(budget)
                .ToList();

            int done = 0;
            foreach (var video in candidates)
            {
                try
                {
                    var votes = await rydClient.GetVotes(video.VideoId);
                    if (votes == null)
                        continue;   // 429 or a miss; try again next run

                    // votes.Rating is YouTube's legacy 1..5 star average and must NOT be stored here:
                    // Video.Rating is a 0..1 liked ratio and the watch page multiplies it by 5.
                    float? ratio = ProviderHelpers.CalculateRating(votes.Likes, votes.Dislikes);
                    await videoManager.SetVotes(video, votes.Likes, ratio);
                    done++;
                }
                catch (Exception ex)
                {
                    log.LogWarning(ex, "Could not refresh ratings for video {0}", video.Id);
                }

                await Task.Delay(RydDelay);
            }

            return done;
        }

        /// <summary>
        /// The expensive half: one paced yt-dlp extraction per video, so the batch is small.
        ///
        /// Un-enriched videos are excluded on purpose. They have never been fetched at all, and their
        /// Published is a MinValue placeholder that sync assigns as a sort marker — so the age curve says
        /// nothing about them, and letting them in would spend the whole budget enriching back-catalogue
        /// instead of refreshing what the user actually looks at. Enrichment already happens lazily when
        /// a video is opened or downloaded.
        /// </summary>
        private async Task<int> RefreshMetadata(DateTimeOffset now)
        {
            int budget = optionManager.GetGlobal(Options.Server_MetadataRefresh_BatchSize);
            if (budget <= 0)
                return 0;

            var due = dataContext.Videos
                .Include(v => v.Subscription)      // not lazy-loaded; needed for the owner's cookie jar
                .Where(v => v.EnrichedAt != null)
                .AsEnumerable()
                .Where(v => RefreshSchedule.IsDue(v.Published, v.LastUpdated, now))
                .OrderByDescending(v => v.Published)
                .Take(budget)
                .ToList();

            int done = 0;
            foreach (var video in due)
            {
                // Re-check between videos: a download queued while we were working must not keep waiting
                // behind the rest of this batch.
                if (IsBusy(out string reason))
                {
                    JobLog($"Stopping early after {done} video(s) — {reason}.");
                    break;
                }

                // Shared with the user-initiated RefreshVideoMetadataJob so the two can't drift; it
                // swallows and logs its own failures, which is what keeps one bad video from abandoning
                // the rest of the batch (the provider's own loop has no per-item guard).
                if (await videoManager.RefreshMetadataNow(video))
                    done++;
            }

            return done;
        }

        /// <summary>
        /// Queues subtitle refetches for downloaded videos that have none — the unattended counterpart to
        /// the "Fetch subtitles" action. Bounded per run because each becomes its own Quartz job.
        /// </summary>
        private async Task<int> SweepMissingSubtitles()
        {
            int budget = optionManager.GetGlobal(Options.Server_MetadataRefresh_SubtitleSweepSize);
            if (budget <= 0)
                return 0;

            var subscriptions = dataContext.Subscriptions
                .Include(s => s.User)
                .AsEnumerable()
                .ToList();

            int queued = 0;
            foreach (var sub in subscriptions)
            {
                if (queued >= budget)
                    break;
                if (sub.User == null)
                    continue;

                try
                {
                    var (q, _) = await videoManager.ReprocessSubscription(
                        sub.User, sub.Id, auto: true, limit: budget - queued);
                    queued += q;
                }
                catch (Exception ex)
                {
                    log.LogWarning(ex, "Subtitle sweep failed for subscription {0}", sub.Id);
                }
            }

            return queued;
        }
    }
}
