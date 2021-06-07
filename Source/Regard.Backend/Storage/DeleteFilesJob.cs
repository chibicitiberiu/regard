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
        protected readonly List<Video> videosToDelete = new List<Video>();

        public int[] VideoIds { get; set; }

        public DeleteFilesJob(IVideoStorageService videoStorage,
                              SubscriptionManager subscriptionManager,
                              JobTrackerService jobTrackerService,
                              ILogger<DeleteFilesJob> logger,
                              DataContext dataContext)
            : base(logger, dataContext, jobTrackerService)
        {
            this.videoStorage = videoStorage;
            this.subscriptionManager = subscriptionManager;
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

        protected override async Task ExecuteJob(IJobExecutionContext context)
        {
            VideoIds = (int[])Job.JobData[Data_VideoIds];
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
