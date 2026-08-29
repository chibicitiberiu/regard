using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quartz;
using Regard.Backend.Common.Providers;
using Regard.Backend.Common.Utils;
using Regard.Backend.Configuration;
using Regard.Backend.DB;
using Regard.Backend.Downloader;
using Regard.Backend.Metadata;
using Regard.Backend.Model;
using Regard.Backend.Services;
using Regard.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Jobs
{
    [DisallowConcurrentExecution]
    public class SynchronizeJob : JobBase
    {
        public static readonly string Data_FolderId = "FolderId";
        public static readonly string Data_SubscriptionId = "SubscriptionId";

        private readonly IConfiguration configuration;
        private readonly IOptionManager optionManager;
        private readonly IProviderManager providerManager;
        private readonly IVideoStorageService videoStorageService;
        private readonly IVideoDownloaderService videoDownloader;
        private readonly MetadataService metadataService;
        private readonly VideoUpdateNotifier videoUpdateNotifier;
        private RegardScheduler scheduler;

        public SynchronizeJob(ILogger<SynchronizeJob> log,
                              DataContext dataContext,
                              JobTrackerService jobTrackerService,
                              IConfiguration configuration,
                              IOptionManager optionManager,
                              IProviderManager providerManager,
                              IVideoStorageService videoStorageService,
                              IVideoDownloaderService videoDownloader,
                              MetadataService metadataService,
                              VideoUpdateNotifier videoUpdateNotifier,
                              RegardScheduler scheduler) : base(log, dataContext, jobTrackerService)
        {
            this.configuration = configuration;
            this.optionManager = optionManager;
            this.providerManager = providerManager;
            this.videoStorageService = videoStorageService;
            this.videoDownloader = videoDownloader;
            this.metadataService = metadataService;
            this.videoUpdateNotifier = videoUpdateNotifier;
            this.scheduler = scheduler;
        }

        public static Task<DateTimeOffset> ScheduleGlobal(RegardScheduler scheduler, string cron)
        {
            return scheduler.Schedule<SynchronizeJob>(
                cronSchedule: cron,
                name: $"Global synchronization",
                retryCount: 0,
                retryIntervalSecs: 0
            );
        }

        public static Task<DateTimeOffset> ScheduleGlobal(RegardScheduler scheduler)
        {
            return scheduler.Schedule<SynchronizeJob>(
                name: $"Global synchronization",
                retryCount: 0,
                retryIntervalSecs: 0
            );
        }

        public static Task<DateTimeOffset> Schedule(RegardScheduler scheduler, Subscription subscription)
        {
            return scheduler.Schedule<SynchronizeJob>(
                name: $"Synchronize subscription {subscription.Name}", 
                jobData: new Dictionary<string, object> { [Data_SubscriptionId] = subscription.Id },
                retryCount: 0,
                retryIntervalSecs: 0
            );
        }

        public static Task<DateTimeOffset> Schedule(RegardScheduler scheduler, SubscriptionFolder folder)
        {
            return scheduler.Schedule<SynchronizeJob>(
                name: $"Synchronize subscriptions in folder {folder.Name}",
                jobData: new Dictionary<string, object> { [Data_FolderId] = folder.Id },
                retryCount: 0,
                retryIntervalSecs: 0
            );
        }

        protected override async Task ExecuteJob(IJobExecutionContext context)
        {
            if (Job.JobData.TryGetValue(Data_SubscriptionId, out object subscriptionId))
            {
                var sub = dataContext.Subscriptions.Find(Convert.ToInt32(subscriptionId));
                if (sub != null)
                {
                    log.LogInformation($"Synchronization started for subscription {sub}.");
                    JobLog($"Synchronizing subscription {sub.Name}");
                    ReportProgress(0f, $"Syncing {sub.Name}");
                    await Synchronize(sub);
                }
            }

            else if (Job.JobData.TryGetValue(Data_FolderId, out object folderId))
            {
                var folder = dataContext.SubscriptionFolders.Find(Convert.ToInt32(folderId));
                if (folder != null)
                {
                    log.LogInformation($"Synchronization started for folder {folder}.");
                    JobLog($"Synchronizing folder {folder.Name}");
                    await SynchronizeAll(dataContext.GetSubscriptionsRecursive(folder).ToList());
                }
            }

            else
            {
                log.LogInformation($"Synchronization started.");
                JobLog("Global synchronization started");
                await SynchronizeAll(dataContext.Subscriptions.ToList());
            }

            JobLog("Synchronization finished");
            log.LogInformation("Synchronization finished.");
        }

        private async Task SynchronizeAll(IList<Subscription> subs)
        {
            for (int i = 0; i < subs.Count; i++)
            {
                var sub = subs[i];
                ReportProgress(subs.Count > 0 ? (float)i / subs.Count : 0f, $"Syncing {sub.Name}");
                JobLog($"[{i + 1}/{subs.Count}] {sub.Name}");
                await Synchronize(sub);
            }
        }

        private async Task Synchronize(Subscription sub)
        {
            try
            {
                if (sub.SubscriptionProviderId != null)
                {
                    await CheckForNewVideos(sub);
                }
                await CheckFiles(sub);
                await videoDownloader.ProcessDownloadRules(sub);

                if (configuration.GetValue<bool>("Metadata:Enabled"))
                    await WriteShowMetadata(sub);
            }
            catch (Exception ex)
            {
                log.LogError(ex, $"Synchronization failed for subscription {sub}");
                JobLog($"Synchronization failed for subscription {sub.Name}: {ex.Message}", Regard.Backend.Common.Model.MessageSeverity.Error);
            }
        }

        /// <summary>
        /// Refreshes the show-level tvshow.nfo + poster for a subscription. The show directory is
        /// taken from any already-downloaded video (skipped entirely if nothing is downloaded yet,
        /// so no empty directories are created and the template path isn't re-derived).
        /// </summary>
        private async Task WriteShowMetadata(Subscription sub)
        {
            var downloadedPath = dataContext.Videos.AsQueryable()
                .Where(v => v.SubscriptionId == sub.Id && v.DownloadedPath != null)
                .Select(v => v.DownloadedPath)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(downloadedPath))
                return;

            var showDir = Path.GetDirectoryName(downloadedPath);
            if (!string.IsNullOrEmpty(showDir))
                await metadataService.WriteShowMetadata(sub, showDir);
        }

        private async Task CheckForNewVideos(Subscription sub)
        {
            var subProvider = providerManager.Get<ISubscriptionProvider>(sub.SubscriptionProviderId);
            subProvider.VerifyNotNull($"Could not find subscription provider {sub.SubscriptionProviderId}");

            // FetchVideos now returns a fast flat listing (newest-first). Preserve that order — do NOT
            // sort by Published, which is only a placeholder until a video is enriched. Enrich the
            // newest few videos in full now; list the rest flat and enrich them lazily (on open/download).
            int eagerBudget = Math.Max(
                optionManager.GetGlobal(Options.Sync_EagerEnrichCount),
                optionManager.GetForSubscription(Options.Subscriptions_MaxCount, sub.Id));

            int newCount = 0;
            await foreach (var video in subProvider.FetchVideos(sub))
            {
                Video existingVideo = FindMatchingVideo(sub, video);

                if (existingVideo != null)
                {
                    MergeVideoInfo(existingVideo, video);
                    continue;
                }

                FillVideoDetails(sub, video);

                if (newCount < eagerBudget && await TryEnrich(video))
                {
                    video.EnrichedAt = DateTimeOffset.UtcNow;
                }
                else
                {
                    // Deferred (or enrichment failed): mark un-enriched and sort it below the enriched
                    // newest videos (MinValue) so "latest N" auto-download never picks a flat placeholder.
                    video.EnrichedAt = null;
                    video.Published = DateTimeOffset.MinValue;
                }

                // Store video
                dataContext.Videos.Add(video);
                log.LogInformation("New video {0}", video);
                JobLog($"New video: {video.Name}");
                await dataContext.SaveChangesAsync();
                newCount++;
            }

            // New tiles appeared — tell the owner's clients to refetch (one coarse push per sync run,
            // however many videos were added).
            if (newCount > 0)
                await videoUpdateNotifier.NotifyVideosChanged(sub.Id, sub.UserId);
        }

        /// <summary>
        /// Fetches full metadata for a single video via its provider. Returns false (and logs) on
        /// failure so the caller can list it flat instead of dropping it.
        /// </summary>
        private async Task<bool> TryEnrich(Video video)
        {
            try
            {
                var videoProvider = await providerManager.FindForVideo(video).FirstOrDefaultAsync();
                videoProvider.VerifyNotNull($"Could not find a video provider for video {video}");
                await videoProvider.UpdateMetadata(new[] { video }, true, true);
                return true;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Could not retrieve metadata for video {0}", video);
                return false;
            }
        }

        private void FillVideoDetails(Subscription sub, Video video)
        {
            // TODO: allow providers to set playlist indices

            var nextIndex = dataContext.Videos.AsQueryable()
                                .Where(x => x.SubscriptionId == sub.Id)
                                .Select(x => (int?)x.PlaylistIndex)
                                .Max();

            video.Subscription = sub;
            video.PlaylistIndex = (nextIndex ?? -1) + 1;
            video.IsWatched = false;
            video.Discovered = DateTimeOffset.UtcNow;

            if (video.Name != null)
            {
                video.Name = video.Name.Trim();
                int? maxLen = video.GetPropertyMaxLength("Name");
                if (maxLen.HasValue)
                    video.Name = video.Name.Truncate(maxLen.Value);
            }
        }

        private void MergeVideoInfo(Video existingVideo, Video fetchedVideo)
        {
            // TODO: merge data, if any extra details
        }

        private Video FindMatchingVideo(Subscription sub, Video video)
        {
            Video existingVideo = null;

            // Find matching video
            if (video.SubscriptionProviderId != null)
            {
                existingVideo = dataContext.Videos.AsQueryable()
                    .Where(x => x.SubscriptionId == sub.Id)
                    .Where(x => x.SubscriptionProviderId == video.SubscriptionProviderId)
                    .FirstOrDefault();
            }
            else if (video.VideoId != null)
            {
                existingVideo = dataContext.Videos.AsQueryable()
                    .Where(x => x.SubscriptionId == sub.Id)
                    .Where(x => x.VideoId == video.VideoId)
                    .FirstOrDefault();
            }
            if (existingVideo == null)
            {
                // The URL should always be provided, but it may not be 100% accurate
                existingVideo = dataContext.Videos.AsQueryable()
                    .Where(x => x.SubscriptionId == sub.Id)
                    .Where(x => x.OriginalUrl.ToLower() == video.OriginalUrl.ToLower())
                    .FirstOrDefault();
            }

            return existingVideo;
        }

        private async Task CheckFiles(Subscription sub)
        {
            var downloadedVideos = dataContext.Videos.AsQueryable()
                .Where(x => x.SubscriptionId == sub.Id)
                .Where(x => x.DownloadedPath != null)
                .ToList();
                
            foreach (var video in downloadedVideos)
            {
                if (!await videoStorageService.VerifyIsDownloaded(video))
                    await OnVideoDeleted(sub, video);

                if (!video.DownloadedSize.HasValue)
                    await OnMissingSize(video);
            }

            // TODO: error handling, show user the errors
        }

        private async Task OnVideoDeleted(Subscription sub, Video video)
        {
            log.LogInformation("Video file for {0} was deleted. Will clean up.", video);
            await videoStorageService.Delete(video);
            video.DownloadedPath = null;
            video.DownloadedSize = null;
            video.DeleteScheduledAt = null;   // file already gone; don't leave a stale "marked for deletion" badge

            if (optionManager.GetForSubscription(Options.Subscriptions_MarkDeletedAsWatched, sub.Id))
            {
                video.IsWatched = true;
                log.LogInformation("Deleted video {0} marked as watched.", video);
            }

            await dataContext.SaveChangesAsync();

            // The file vanished on disk — push the now-not-downloaded state so an open tile drops its badge.
            await videoUpdateNotifier.NotifyVideoUpdated(video, sub.UserId);
        }

        private async Task OnMissingSize(Video video)
        {
            video.DownloadedSize = await videoStorageService.CalculateSize(video);
            await dataContext.SaveChangesAsync();
        }
    }
}
