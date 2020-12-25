using Microsoft.Extensions.Logging;
using Quartz;
using Regard.Backend.DB;
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
            var retryJob = context.JobDetail.GetJobBuilder()
                .WithIdentity($"{context.JobDetail.Key.Name}-{Attempt + 1}", context.JobDetail.Key.Group)
                .UsingJobData("Attempt", Attempt + 1)
                .Build();

            var retryTrigger = TriggerBuilder.Create()
                .StartAt(DateTimeOffset.Now.Add(RetryInterval))
                .Build();

            await context.Scheduler.ScheduleJob(retryJob, retryTrigger);
        }

        protected abstract Task ExecuteJob(IJobExecutionContext context);
    }
}
