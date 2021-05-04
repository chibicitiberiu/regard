using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quartz;
using Regard.Backend.Common.Services;
using Regard.Backend.DB;
using Regard.Backend.Services;
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

        public InitJob(ILogger<InitJob> logger,
                       IConfiguration configuration,
                       DataContext dataContext,
                       IProviderManager providerManager,
                       IYoutubeDlService ytdlService)
        {
            this.log = logger;
            this.configuration = configuration;
            this.dataContext = dataContext;
            this.providerManager = providerManager;
            this.ytdlService = ytdlService;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            log.LogInformation("Running initialization tasks...");

            // Initialize providers
            await providerManager.Initialize();

            await ytdlService.Initialize();

            // Create basic jobs
            var scheduler = new RegardScheduler(log, context.Scheduler);
            await scheduler.ScheduleGlobalSynchronize(configuration["SynchronizationSchedule"]);
            await scheduler.ScheduleYoutubeDLUpdate(DateTimeOffset.Now.AddSeconds(10), TimeSpan.FromDays(1));
            await scheduler.ScheduleFetchThumbnails(DateTimeOffset.Now.AddSeconds(30), TimeSpan.FromMinutes(30));
            log.LogInformation("Initialization tasks completed!");
        }
    }
}
