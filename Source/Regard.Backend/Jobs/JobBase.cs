using Microsoft.Extensions.Logging;
using Quartz;
using Regard.Backend.Common.Model;
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
        protected readonly JobTrackerService jobTrackerService;

        protected JobInfo Job { get; set; }

        public JobBase(ILogger log,
                       DataContext dataContext,
                       JobTrackerService jobTrackerService)
        {
            this.log = log;
            this.dataContext = dataContext;
            this.jobTrackerService = jobTrackerService;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            if (context.MergedJobDataMap.ContainsKey("JobId"))
                Job = dataContext.Jobs.Find(context.MergedJobDataMap.GetLong("JobId"));

            if (Job == null)
                throw new ArgumentException("Invalid job ID");

            jobTrackerService.OnJobStarted(Job);

            try
            {
                await ExecuteJob(context);
                jobTrackerService.OnJobCompleted(Job);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "{0} failed with exception!", GetType().Name);
                jobTrackerService.OnJobFailed(Job, ex.Message);
            }
        }

        protected abstract Task ExecuteJob(IJobExecutionContext context);
    }
}
