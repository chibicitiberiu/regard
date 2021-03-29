using Microsoft.Extensions.Logging;
using Quartz;
using Regard.Backend.Common.Services;
using Regard.Backend.DB;
using Regard.Backend.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Jobs
{
    public class YoutubeDLUpdateJob : JobBase
    {
        private readonly IYoutubeDlService ytdlService;

        public YoutubeDLUpdateJob(ILogger<YoutubeDLUpdateJob> logger,
                                  DataContext dataContext,
                                  IYoutubeDlService ytdlService) : base(logger, dataContext)
        {
            this.ytdlService = ytdlService;
        }

        protected override int RetryCount => 10;

        protected override TimeSpan RetryInterval => TimeSpan.FromMinutes(3 * (Attempt + 1));

        protected override async Task ExecuteJob(IJobExecutionContext context)
        {
            log.LogInformation("Updating youtube-dl...");
            try
            {
                await ytdlService.DownloadLatest();
                log.LogInformation("Youtube-dl is up to date ({0})", ytdlService.CurrentVersion);
            }
            catch(Exception ex)
            {
                log.LogError(ex, "Updating youtube-dl failed! Will retry again later.");
                throw;
            }
        }
    }
}
