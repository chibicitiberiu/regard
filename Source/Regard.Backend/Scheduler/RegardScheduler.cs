using Microsoft.Extensions.Logging;
using Quartz;
using Regard.Backend.Common.Model;
using Regard.Backend.DB;
using Regard.Backend.Downloader;
using Regard.Backend.Jobs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    public class RegardScheduler
    {
        private readonly ILogger log;
        private readonly ISchedulerFactory schedulerFactory;
        private readonly JobTrackerService jobTrackerService;

        private IScheduler quartz;

        // Job types that opt in to being re-enqueued after a restart, via [ResumeAfterRestart], discovered
        // once. Keyed by type name — which is exactly what JobInfo.Key stores (see Schedule below) and the
        // durable Quartz JobKey. Anything not here is abandoned by the startup reconciliation sweep.
        private static readonly Dictionary<string, Type> resumableJobTypes =
            typeof(JobBase).Assembly.GetTypes()
                .Where(t => !t.IsAbstract
                         && typeof(JobBase).IsAssignableFrom(t)
                         && t.GetCustomAttribute<ResumeAfterRestartAttribute>(inherit: true) != null)
                .ToDictionary(t => t.Name, t => t);

        // "Important" job types that surface live in the notification bell. Everything else is
        // tracked + logged but stays out of the bell. Flip a type in/out here to change the policy.
        private static readonly HashSet<Type> NotifiableJobTypes = new HashSet<Type>()
        {
            typeof(DownloadVideoJob),
            typeof(SynchronizeJob),
            typeof(ImportSubscriptionsJob),
            typeof(DeleteUserJob),
            // A user-clicked "Fetch subtitles" should report back. The job itself returns null
            // notifications when the background sweep created it, so unattended runs stay silent.
            typeof(ReprocessVideoJob),
        };

        public RegardScheduler(ILogger<RegardScheduler> log,
                               ISchedulerFactory schedulerFactory,
                               JobTrackerService jobTrackerService)
        {
            this.log = log;
            this.schedulerFactory = schedulerFactory;
            this.jobTrackerService = jobTrackerService;
            // Failed-job retries are handled by the singleton JobRetryService (this scheduler is scoped;
            // subscribing here would multiply handlers once the job pool is > 1).
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
            bool notify = NotifiableJobTypes.Contains(typeof(TJob));
            var job = jobTrackerService.CreateJob(name, userId, trackWhenScheduled, jobData, retryCount, retryIntervalSecs, notify);

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

        /// <summary>
        /// Re-enqueues an orphaned job (left non-terminal by a restart, since Quartz uses an in-memory
        /// store) from its persisted row — reusing the same JobInfo, so its history/RetryCount/JobData
        /// carry over. Only types opted in via <see cref="ResumeAfterRestartAttribute"/> are resumed;
        /// returns false for anything else (unmarked type, unknown/null Key) so the caller can abandon it.
        /// </summary>
        public async Task<bool> TryResume(JobInfo job)
        {
            if (job.Key == null || !resumableJobTypes.TryGetValue(job.Key, out var jobType))
                return false;

            await GetQuartz();

            // Rebuild the durable job (the in-memory store dropped it on restart), then fire it now. The
            // trigger carries only JobId; JobBase.Execute reloads the payload from JobInfo.JobData.
            var jobKey = JobKey.Create(job.Key);
            if (!await quartz.CheckExists(jobKey))
            {
                await quartz.AddJob(JobBuilder.Create(jobType)
                    .WithIdentity(jobKey)
                    .StoreDurably(true)
                    .Build(), true);
            }

            var trigger = TriggerBuilder.Create()
                .ForJob(jobKey)
                .UsingJobData("JobId", job.Id)
                .StartNow()
                .Build();

            var nextRun = await quartz.ScheduleJob(trigger);
            jobTrackerService.OnJobScheduled(job, nextRun);
            return true;
        }

        /// <summary>
        /// Removes the pending trigger for a job that hasn't started yet — a download waiting on the host
        /// throttle, or one waiting out its retry interval. Returns true if a trigger was actually removed.
        ///
        /// Triggers have to be found by scanning rather than addressed directly: every job of a type
        /// shares one durable JobKey (the type name), and the deferred/retry triggers are built with
        /// auto-generated names that are never persisted. What they *do* all carry is the JobId in their
        /// data map, which is enough to pick out the right one.
        ///
        /// Removing the last trigger is safe: the jobs are registered with StoreDurably(true), so the job
        /// itself survives and can be scheduled again later.
        /// </summary>
        public async Task<bool> TryUnschedule(JobInfo job)
        {
            // A legacy row with no Key can't be addressed — JobKey.Create(null) throws.
            if (job == null || string.IsNullOrEmpty(job.Key))
                return false;

            await GetQuartz();

            var jobKey = JobKey.Create(job.Key);
            if (!await quartz.CheckExists(jobKey))
                return false;

            bool removed = false;
            foreach (var trigger in await quartz.GetTriggersOfJob(jobKey))
            {
                if (!trigger.JobDataMap.ContainsKey("JobId"))
                    continue;
                if (trigger.JobDataMap.GetLong("JobId") != job.Id)
                    continue;

                if (await quartz.UnscheduleJob(trigger.Key))
                {
                    log.LogInformation("Unscheduled pending trigger for job {0}", job.Id);
                    removed = true;
                }
            }

            return removed;
        }
    }
}
