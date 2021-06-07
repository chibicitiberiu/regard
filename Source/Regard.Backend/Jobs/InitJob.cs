using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quartz;
using Regard.Backend.Common.Services;
using Regard.Backend.DB;
using Regard.Backend.Services;
using Regard.Backend.Thumbnails;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Jobs
{
    public class InitJob : IJob
    {
        private readonly ILogger log;
        private readonly IConfiguration configuration;
        private readonly DataContext dataContext;
        private readonly IProviderManager providerManager;
        private readonly IYoutubeDlService ytdlService;
        private readonly RegardScheduler scheduler;

        public InitJob(ILogger<InitJob> logger,
                       IConfiguration configuration,
                       DataContext dataContext,
                       IProviderManager providerManager,
                       IYoutubeDlService ytdlService,
                       RegardScheduler scheduler)
        {
            this.log = logger;
            this.configuration = configuration;
            this.dataContext = dataContext;
            this.providerManager = providerManager;
            this.ytdlService = ytdlService;
            this.scheduler = scheduler;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            log.LogInformation("Running initialization tasks...");

            // Initialize providers
            await providerManager.Initialize();

            await ytdlService.Initialize();

            // Create basic jobs
            await SynchronizeJob.ScheduleGlobal(scheduler, configuration["SynchronizationSchedule"]);
            await YoutubeDLUpdateJob.Schedule(scheduler, DateTimeOffset.Now.AddSeconds(10), TimeSpan.FromDays(1));
            await FetchThumbnailsJob.Schedule(scheduler, DateTimeOffset.Now.AddSeconds(30), TimeSpan.FromMinutes(30));
            log.LogInformation("Initialization tasks completed!");
        }
    }
}
