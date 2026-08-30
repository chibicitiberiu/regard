using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Regard.Backend.DB;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    /// <summary>
    /// Reschedules failed jobs that still have retries left. A SINGLETON hosted service that subscribes to
    /// the singleton <see cref="JobTrackerService.JobFailed"/> exactly once — the old subscription lived on
    /// the SCOPED RegardScheduler, so once the job pool is &gt; 1 every live scope was a handler, multiplying
    /// retries and touching disposed DataContexts. Each failure is handled in a fresh scope.
    /// </summary>
    public class JobRetryService : IHostedService
    {
        private readonly JobTrackerService jobTracker;
        private readonly ISchedulerFactory schedulerFactory;
        private readonly IServiceScopeFactory scopeFactory;
        private readonly ILogger<JobRetryService> log;

        public JobRetryService(JobTrackerService jobTracker,
                               ISchedulerFactory schedulerFactory,
                               IServiceScopeFactory scopeFactory,
                               ILogger<JobRetryService> log)
        {
            this.jobTracker = jobTracker;
            this.schedulerFactory = schedulerFactory;
            this.scopeFactory = scopeFactory;
            this.log = log;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            jobTracker.JobFailed += OnJobFailed;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            jobTracker.JobFailed -= OnJobFailed;
            return Task.CompletedTask;
        }

        private async void OnJobFailed(object sender, JobFailedEventArgs e)
        {
            // async void: guard so an unhandled exception can't crash the process.
            try
            {
                if (e.Job == null || e.Job.RetryCount <= 0)
                    return;

                using var scope = scopeFactory.CreateScope();
                var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

                // Work on the SAME JobInfo instance the running job holds, not a fresh copy.
                //
                // This handler is invoked synchronously from JobBase's catch block, and JobBase then
                // persists its own instance in the finally that follows. A copy loaded here would be
                // committed first and immediately overwritten by that stale instance — leaving
                // RetryCount pinned at its initial value, so a permanently failing job retried every
                // RetryInterval forever and the card always read "Retrying (1/3)". Mutating the shared
                // instance means JobBase's persist writes these values through instead of over them.
                var job = e.Job;
                if (job.RetryCount <= 0 || string.IsNullOrEmpty(job.Key))
                    return;

                job.RetryCount--;
                dataContext.Jobs.Update(job);
                dataContext.SaveChanges();

                var scheduler = await schedulerFactory.GetScheduler();
                var trigger = TriggerBuilder.Create()
                    .ForJob(JobKey.Create(job.Key))
                    .UsingJobData("JobId", job.Id)
                    .StartAt(DateTimeOffset.Now.AddSeconds(job.RetryInterval))
                    .Build();

                var nextRun = await scheduler.ScheduleJob(trigger);
                jobTracker.OnJobScheduled(job, nextRun);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to schedule job retry");
            }
        }
    }
}
