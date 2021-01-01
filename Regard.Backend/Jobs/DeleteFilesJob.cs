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
        protected readonly IVideoStorageService videoStorage;
        protected readonly SubscriptionManager subscriptionManager;
        protected readonly List<Video> videosToDelete = new List<Video>();

        protected override int RetryCount => 3;

        protected override TimeSpan RetryInterval => TimeSpan.FromMinutes(10);

        public int[] VideoIds { get; set; }

        public DeleteFilesJob(IVideoStorageService videoStorage,
                              SubscriptionManager subscriptionManager,
                              ILogger<DeleteFilesJob> logger,
                              DataContext dataContext)
            : base(logger, dataContext)
        {
            this.videoStorage = videoStorage;
            this.subscriptionManager = subscriptionManager;
        }

        protected override async Task ExecuteJob(IJobExecutionContext context)
        {
            VideoIds = (int[])context.MergedJobDataMap.Get("VideoIds");
            
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
