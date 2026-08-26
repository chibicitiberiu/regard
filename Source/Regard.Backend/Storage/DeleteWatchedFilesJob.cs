using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoreLinq;
using Quartz;
using Regard.Backend.DB;
using Regard.Backend.Downloader;
using Regard.Backend.Model;
using Regard.Backend.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Jobs
{
    /// <summary>
    /// Deletes the downloaded files for the given videos (like <see cref="DeleteFilesJob"/>),
    /// then refills each affected subscription's download window. Used by the
    /// "mark watched -> delete -> refill" loop.
    /// </summary>
    public class DeleteWatchedFilesJob : DeleteFilesJob
    {
        private readonly IVideoDownloaderService videoDownloader;

        public DeleteWatchedFilesJob(IVideoStorageService videoStorage,
                                     SubscriptionManager subscriptionManager,
                                     JobTrackerService jobTrackerService,
                                     ILogger<DeleteFilesJob> logger,
                                     DataContext dataContext,
                                     IVideoDownloaderService videoDownloader)
            : base(videoStorage, subscriptionManager, jobTrackerService, logger, dataContext)
        {
            this.videoDownloader = videoDownloader;
        }

        public static new Task Schedule(RegardScheduler scheduler, int[] videoIds)
        {
            return scheduler.Schedule<DeleteWatchedFilesJob>(
                name: "Delete watched files",
                jobData: new Dictionary<string, object>()
                {
                    { Data_VideoIds, videoIds }
                },
                retryCount: 3,
                retryIntervalSecs: 10 * 60);
        }

        protected override async Task ExecuteJob(IJobExecutionContext context)
        {
            // Base deletes the files and nulls DownloadedPath/DownloadedSize.
            await base.ExecuteJob(context);

            // Refill the download window for each affected subscription (a slot just freed).
            var subIds = videosToDelete.Select(v => v.SubscriptionId).Distinct().ToArray();
            foreach (var subId in subIds)
            {
                var sub = dataContext.Subscriptions.Find(subId);
                if (sub != null)
                    await videoDownloader.ProcessDownloadRules(sub);
            }
        }

        protected override void LogBegin()
        {
            log.LogInformation("Delete watched files job started for videos {0}", VideoIds.Humanize());
        }
    }
}
