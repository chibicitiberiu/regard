using Microsoft.Extensions.Logging;
using Quartz;
using Regard.Backend.Common.Services;
using Regard.Backend.DB;
using Regard.Backend.Services;
using System;
using System.Threading.Tasks;

namespace Regard.Backend.Jobs
{
    public class YoutubeDLUpdateJob : JobBase
    {
        private readonly IYoutubeDlService ytdlService;

        public YoutubeDLUpdateJob(ILogger<YoutubeDLUpdateJob> logger,
                                  DataContext dataContext,
                                  JobTrackerService jobTrackerService,
                                  IYoutubeDlService ytdlService) : base(logger, dataContext, jobTrackerService)
        {
            this.ytdlService = ytdlService;
        }

        public static Task<DateTimeOffset> Schedule(RegardScheduler scheduler, DateTimeOffset start, TimeSpan interval)
        {
            return scheduler.Schedule<YoutubeDLUpdateJob>(
                name: "Update youtube-dl",
                start: start,
                repeatInterval: interval,
                retryCount: 10,
                retryIntervalSecs: 10 * 60);
        }

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
