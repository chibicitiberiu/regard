using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoreLinq;
using Quartz;
using Regard.Backend.DB;
using Regard.Backend.Model;
using Regard.Backend.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Jobs
{
    public class DeleteFilesJob : JobBase
    {
        protected static readonly string Data_VideoIds = nameof(VideoIds);

        protected readonly IVideoStorageService videoStorage;
        protected readonly SubscriptionManager subscriptionManager;
        protected readonly VideoUpdateNotifier videoUpdateNotifier;
        protected readonly List<Video> videosToDelete = new List<Video>();

        public int[] VideoIds { get; set; }

        public DeleteFilesJob(IVideoStorageService videoStorage,
                              SubscriptionManager subscriptionManager,
                              JobTrackerService jobTrackerService,
                              ILogger<DeleteFilesJob> logger,
                              DataContext dataContext,
                              VideoUpdateNotifier videoUpdateNotifier)
            : base(logger, dataContext, jobTrackerService)
        {
            this.videoStorage = videoStorage;
            this.subscriptionManager = subscriptionManager;
            this.videoUpdateNotifier = videoUpdateNotifier;
        }

        public static Task Schedule(RegardScheduler scheduler, int[] videoIds)
        {
            return scheduler.Schedule<DeleteFilesJob>(
                name: "Delete files",
                jobData: new Dictionary<string, object>()
                {
                    { Data_VideoIds, videoIds }
                },
                retryCount: 3,
                retryIntervalSecs: 10 * 60);
        }

        /// <summary>
        /// Job data round-trips through JSON, so an id array comes back as a JArray, not int[].
        /// Read it defensively (cf. DownloadVideoJob's Convert.ToInt32 on the scalar id).
        /// </summary>
        protected static int[] ReadIntArray(object value) => value switch
        {
            int[] a => a,
            System.Collections.IEnumerable e => e.Cast<object>().Select(Convert.ToInt32).ToArray(),
            _ => Array.Empty<int>()
        };

        protected override async Task ExecuteJob(IJobExecutionContext context)
        {
            VideoIds = ReadIntArray(Job.JobData[Data_VideoIds]);
            LogBegin();

            videosToDelete.Clear();

            if (VideoIds != null)
            {
                videosToDelete.AddRange(dataContext.Videos.AsQueryable()
                    .Where(x => VideoIds.Contains(x.Id))
                    .Where(x => x.DownloadedPath != null));
            }

            AddAdditionalVideos();

            foreach (var video in videosToDelete)
                await DeleteVideo(video);

            await dataContext.SaveChangesAsync();

            // Push the now-not-downloaded state so cards drop their downloaded badge live.
            foreach (var video in videosToDelete)
            {
                var ownerId = dataContext.Subscriptions.AsQueryable()
                    .Where(s => s.Id == video.SubscriptionId)
                    .Select(s => s.UserId)
                    .FirstOrDefault();
                await videoUpdateNotifier.NotifyVideoUpdated(video, ownerId);
            }
        }

        protected virtual void LogBegin()
        {
            log.LogInformation("Delete files job started for videos {0}", VideoIds.Humanize());
        }

        protected virtual void AddAdditionalVideos()
        {
        }

        private async Task DeleteVideo(Video video)
        {
            try
            {
                await videoStorage.Delete(video);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Deleting downloaded files for video {0} failed!", video);
            }

            video.DownloadedPath = null;
            video.DownloadedSize = null;
        }
    }
}
