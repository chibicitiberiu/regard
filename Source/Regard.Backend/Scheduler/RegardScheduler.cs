using Microsoft.Extensions.Logging;
using Quartz;
using Regard.Backend.Common.Model;
using Regard.Backend.DB;
using Regard.Backend.Jobs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    public class RegardScheduler : IDisposable
    {
        private readonly ILogger log;
        private readonly ISchedulerFactory schedulerFactory;
        private readonly JobTrackerService jobTrackerService;
        private readonly DataContext dataContext;

        private IScheduler quartz;

        public RegardScheduler(ILogger<RegardScheduler> log,
                               ISchedulerFactory schedulerFactory,
                               JobTrackerService jobTrackerService,
                               DataContext dataContext)
        {
            this.log = log;
            this.schedulerFactory = schedulerFactory;
            this.jobTrackerService = jobTrackerService;
            this.dataContext = dataContext;
            jobTrackerService.JobFailed += JobTrackerService_JobFailed;
        }

        public void Dispose()
        {
            jobTrackerService.JobFailed -= JobTrackerService_JobFailed;
        }

        private async Task GetQuartz()
        {
            if (quartz == null)
                quartz = await schedulerFactory.GetScheduler();
        }

        public async Task<DateTimeOffset> Schedule<TJob>(Action<TriggerBuilder> triggerBuilder,
                                                         string name,
                                                         string userId = null,
                                                         bool trackWhenScheduled = false,
                                                         IDictionary<string, object> jobData = null,
                                                         int retryCount = 0,
                                                         int retryIntervalSecs = 600) where TJob : JobBase
        {
            var job = jobTrackerService.CreateJob(name, userId, trackWhenScheduled, jobData, retryCount, retryIntervalSecs);

            try
            {
                await GetQuartz();

                // Create quartz job
                var jobKey = JobKey.Create(typeof(TJob).Name);
                job.Key = jobKey.Name;

                if (!await quartz.CheckExists(jobKey))
                {
                    await quartz.AddJob(JobBuilder.Create<TJob>()
                        .WithIdentity(typeof(TJob).Name)
                        .StoreDurably(true)
                        .Build(), true);
                }

                // Create job data map
                var builder = TriggerBuilder.Create()
                    .ForJob(jobKey)
                    .UsingJobData("JobId", job.Id);

                triggerBuilder(builder);

                // Create trigger
                var nextRun = await quartz.ScheduleJob(builder.Build());
                jobTrackerService.OnJobScheduled(job, nextRun);

                return nextRun;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error creating job");
                jobTrackerService.OnJobFailed(job, "Job creation failed", "Check logs for more information.");
                throw;
            }
        }

        /// <summary>
        /// Schedules tracked job for immediate execution
        /// </summary>
        /// <typeparam name="TJob">Job type</typeparam>
        /// <param name="name">User friendly job name</param>
        /// <param name="jobData">Dictionary containing data to be passed to job</param>
        /// <param name="retryCount">How many times the job will be attempted again on failure</param>
        /// <param name="retryIntervalSecs">How long to wait until trying again</param>
        /// <returns></returns>
        public Task<DateTimeOffset> Schedule<TJob>(string name,
                                                   string userId = null,
                                                   IDictionary<string, object> jobData = null,
                                                   int retryCount = 0,
                                                   int retryIntervalSecs = 600) where TJob : JobBase
        {
            return Schedule<TJob>(triggerBuilder: tb => tb.StartNow(),
                                  name: name,
                                  userId: userId,
                                  trackWhenScheduled: true,
                                  jobData: jobData,
                                  retryCount: retryCount,
                                  retryIntervalSecs: retryIntervalSecs);
        }

        /// <summary>
        /// Schedules tracked job execution based on a cron expression
        /// </summary>
        /// <typeparam name="TJob">Job type</typeparam>
        /// <param name="cronSchedule">Cron expression</param>
        /// <param name="name">User friendly job name</param>
        /// <param name="jobData">Dictionary containing data to be passed to job</param>
        /// <param name="retryCount">How many times the job will be attempted again on failure</param>
        /// <param name="retryIntervalSecs">How long to wait until trying again</param>
        /// <returns></returns>
        public Task<DateTimeOffset> Schedule<TJob>(string cronSchedule,
                                                   string name,
                                                   string userId = null,
                                                   IDictionary<string, object> jobData = null,
                                                   int retryCount = 0,
                                                   int retryIntervalSecs = 600) where TJob : JobBase
        {
            return Schedule<TJob>(triggerBuilder: tb => tb.WithCronSchedule(cronSchedule),
                                  name: name,
                                  userId: userId,
                                  jobData: jobData,
                                  retryCount: retryCount,
                                  retryIntervalSecs: retryIntervalSecs);
        }

        /// <summary>
        /// Schedules tracked job execution based on a cron expression
        /// </summary>
        /// <typeparam name="TJob">Job type</typeparam>
        /// <param name="start">When job will be executed</param>
        /// <param name="name">User friendly job name</param>
        /// <param name="jobData">Dictionary containing data to be passed to job</param>
        /// <param name="retryCount">How many times the job will be attempted again on failure</param>
        /// <param name="retryIntervalSecs">How long to wait until trying again</param>
        /// <returns></returns>
        public Task<DateTimeOffset> Schedule<TJob>(DateTimeOffset start,
                                                   TimeSpan repeatInterval,
                                                   string name,
                                                   string userId = null,
                                                   IDictionary<string, object> jobData = null,
                                                   int retryCount = 0,
                                                   int retryIntervalSecs = 600) where TJob : JobBase
        {
            return Schedule<TJob>(triggerBuilder: builder => builder.WithSimpleSchedule(sched => sched.WithInterval(repeatInterval).RepeatForever())
                                                                    .StartAt(start),
                                  name: name,
                                  userId: userId,
                                  jobData: jobData,
                                  retryCount: retryCount,
                                  retryIntervalSecs: retryIntervalSecs);
        }

        private async void JobTrackerService_JobFailed(object sender, JobFailedEventArgs e)
        {
            if (e.Job.RetryCount > 0)
                await ScheduleJobRetry(e.Job);
        }

        private async Task ScheduleJobRetry(JobInfo job)
        {
            job.RetryCount--;
            dataContext.SaveChanges();

            try
            {
                await GetQuartz();

                var jobKey = JobKey.Create(job.Key);

                var trigger = TriggerBuilder.Create()
                    .ForJob(jobKey)
                    .UsingJobData("JobId", job.Id)
                    .StartAt(DateTimeOffset.Now.AddSeconds(job.RetryInterval))
                    .Build();

                var nextRun = await quartz.ScheduleJob(trigger);
                jobTrackerService.OnJobScheduled(job, nextRun);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error creating job");
                jobTrackerService.OnJobFailed(job, "Job creation failed", "Check logs for more information.");
            }
        }
    }
}
