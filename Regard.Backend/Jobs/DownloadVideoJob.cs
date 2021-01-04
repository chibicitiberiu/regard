using Microsoft.Extensions.Logging;
using Quartz;
using Regard.Backend.DB;
using Regard.Backend.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Jobs
{
    public class DownloadVideoJob : JobBase
    {
        protected readonly YoutubeDLService ytdlService;
        bool shouldRetry = false;

        protected override int RetryCount => (shouldRetry ? 3 : 0);

        protected override TimeSpan RetryInterval => TimeSpan.FromMinutes(15);

        public int VideoId { get; set; }

        public DownloadVideoJob(ILogger<DownloadVideoJob> logger,
                                DataContext dataContext, 
                                YoutubeDLService ytdlService) : base(logger, dataContext)
        {
            this.ytdlService = ytdlService;
        }

        protected override async Task ExecuteJob(IJobExecutionContext context)
        {
            VideoId = context.MergedJobDataMap.GetInt("VideoId");
            shouldRetry = false;

            var video = dataContext.Videos.Find(VideoId);
            if (video == null)
                throw new ArgumentException($"Download failed - invalid video id {VideoId}.");

            if (video.DownloadedPath != null)
                throw new ArgumentException($"Download failed - video {VideoId} is already downloaded!");

            var opts = ResolveDownloadOptions();
            shouldRetry = true;

            await ytdlService.UsingYoutubeDL(async ytdl =>
            {

            });
        }

        private IReadOnlyDictionary<string, string> ResolveDownloadOptions()
        {
            var opts = new Dictionary<string, string>();

            return opts;
        }
    }
}
