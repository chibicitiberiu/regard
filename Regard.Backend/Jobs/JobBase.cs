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
    public abstract class JobBase : IJob
    {
        protected readonly ILogger log;
        protected readonly DataContext dataContext;

        public int Attempt { get; set; } = 0;

        protected abstract int RetryCount { get; }

        protected abstract TimeSpan RetryInterval { get; }

        public JobBase(ILogger log, DataContext dataContext)
        {
            this.log = log;
            this.dataContext = dataContext;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            if (context.MergedJobDataMap.ContainsKey("Attempt"))
                Attempt = context.MergedJobDataMap.GetInt("Attempt");

            try
            {
                await ExecuteJob(context);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "{0} failed with exception!", GetType().Name);
                if (Attempt < RetryCount)
                    await ScheduleRetry(context);
            }
        }

        private async Task ScheduleRetry(IJobExecutionContext context)
        {
            var scheduler = new RegardScheduler(log, context.Scheduler);
            await scheduler.ScheduleJobRetry(context.JobDetail, Attempt + 1, RetryInterval);
        }

        protected abstract Task ExecuteJob(IJobExecutionContext context);
    }
}
