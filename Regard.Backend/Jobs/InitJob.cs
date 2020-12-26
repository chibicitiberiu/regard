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

            // Make sure data directory exist
            var dataDirectory = configuration["DataDirectory"];
            Directory.CreateDirectory(dataDirectory);

            // Initialize providers
            await providerManager.Initialize();

            await ytdlService.Initialize();

            // Create basic jobs
            await context.Scheduler.ScheduleJob(
                JobBuilder.Create<SynchronizeJob>()
                    .WithIdentity("Global synchronization")
                    .Build(),
                TriggerBuilder.Create()
                    //.WithCronSchedule(configuration["SynchronizationSchedule"])
                    .StartAt(DateTimeOffset.Now.AddSeconds(5))
                    .Build());

            await context.Scheduler.ScheduleJob(
                JobBuilder.Create<YoutubeDLUpdateJob>()
                    .WithIdentity("YoutubeDL update")
                    .Build(),
                TriggerBuilder.Create()
                    .WithSimpleSchedule(sched => sched.WithInterval(TimeSpan.FromDays(1)).RepeatForever())
                    .StartAt(DateTimeOffset.Now.AddSeconds(10))
                    .Build());

            log.LogInformation("Initialization tasks completed!");
        }
    }
}
