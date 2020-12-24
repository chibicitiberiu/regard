using Microsoft.Extensions.Configuration;
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
    public class InitJob : IJob
    {
        private readonly ILogger log;
        private readonly IConfiguration configuration;
        private readonly DataContext dataContext;
        private readonly IProviderManager providerManager;

        public InitJob(ILogger<InitJob> logger,
                       IConfiguration configuration,
                       DataContext dataContext,
                       IProviderManager providerManager)
        {
            this.log = logger;
            this.configuration = configuration;
            this.dataContext = dataContext;
            this.providerManager = providerManager;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            log.LogInformation("Running initialization tasks...");

            // Initialize providers
            await providerManager.Initialize();

            // Create basic jobs
            await context.Scheduler.ScheduleJob(
                JobBuilder.Create<SynchronizeJob>()
                    .WithIdentity("Global synchronization")
                    .Build(),
                TriggerBuilder.Create()
                    //.WithCronSchedule(configuration["SynchronizationSchedule"])
                    .StartAt(DateTimeOffset.Now.AddSeconds(10))
                    .Build());

            log.LogInformation("Initialization tasks completed!");
        }
    }
}
