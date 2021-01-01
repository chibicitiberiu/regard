using Quartz;
using Regard.Backend.Jobs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    public class RegardScheduler
    {
        private IScheduler quartz;
        private readonly ISchedulerFactory schedulerFactory;

        public RegardScheduler(IScheduler quartz)
        {
            this.quartz = quartz;
        }

        public RegardScheduler(ISchedulerFactory schedulerFactory)
        {
            this.schedulerFactory = schedulerFactory;
        }

        private async Task GetQuartz()
        {
            if (quartz == null)
                quartz = await schedulerFactory.GetScheduler();
        }

        public async Task ScheduleGlobalSynchronize(string cronSchedule)
        {
            await GetQuartz();

            await quartz.ScheduleJob(
                JobBuilder.Create<SynchronizeJob>()
                    .WithIdentity("Global synchronization")
                    .Build(),
                TriggerBuilder.Create()
                    .WithCronSchedule(cronSchedule)
                    .Build());
        }

        public async Task ScheduleGlobalSynchronizeNow()
        {
            await GetQuartz();

            var job = JobBuilder.Create<SynchronizeJob>()
                    .WithIdentity("Global synchronize now")
                    .Build();

            var trigger = TriggerBuilder.Create()
                    .StartNow()
                    .Build();

            if (!await quartz.CheckExists(job.Key))
                await quartz.ScheduleJob(job, trigger);
        }

        public async Task ScheduleSynchronizeSubscription(int subscriptionId)
        {
            await GetQuartz();

            var job = JobBuilder.Create<SynchronizeJob>()
                    .WithIdentity($"Synchronize subscription {subscriptionId}")
                    .UsingJobData("SubscriptionId", subscriptionId)
                    .Build();
            var trigger = TriggerBuilder.Create()
                    .StartNow()
                    .Build();

            if (!await quartz.CheckExists(job.Key))
                await quartz.ScheduleJob(job, trigger);
        }

        public async Task ScheduleSynchronizeFolder(int folderId)
        {
            await GetQuartz();

            var job = JobBuilder.Create<SynchronizeJob>()
                    .WithIdentity($"Synchronize folder {folderId}")
                    .UsingJobData("FolderId", folderId)
                    .Build();

            var trigger = TriggerBuilder.Create()
                    .StartNow()
                    .Build();

            if (!await quartz.CheckExists(job.Key))
                await quartz.ScheduleJob(job, trigger);
        }

        public async Task ScheduleYoutubeDLUpdate(DateTimeOffset start, TimeSpan interval)
        {
            await GetQuartz();

            await quartz.ScheduleJob(
                JobBuilder.Create<YoutubeDLUpdateJob>()
                    .WithIdentity("YoutubeDL update")
                    .Build(),
                TriggerBuilder.Create()
                    .WithSimpleSchedule(sched => sched.WithInterval(interval).RepeatForever())
                    .StartAt(start)
                    .Build());
        }

        public async Task ScheduleJobRetry(IJobDetail jobDetail, int attempt, TimeSpan retryInterval)
        {
            await GetQuartz();

            var retryJob = jobDetail.GetJobBuilder()
                .WithIdentity($"{jobDetail.Key.Name}-{attempt}", jobDetail.Key.Group)
                .UsingJobData("Attempt", attempt)
                .Build();

            var retryTrigger = TriggerBuilder.Create()
                .StartAt(DateTimeOffset.Now.Add(retryInterval))
                .Build();

            await quartz.ScheduleJob(retryJob, retryTrigger);
        }

        public async Task ScheduleDownloadVideo(int videoId)
        {
            await GetQuartz();

            var job = JobBuilder.Create<DownloadVideoJob>()
                    .WithIdentity($"Download video {videoId}")
                    .UsingJobData("VideoId", videoId)
                    .Build();

            var trigger = TriggerBuilder.Create()
                    .StartNow()
                    .Build();

            if (!await quartz.CheckExists(job.Key))
                await quartz.ScheduleJob(job, trigger);
        }

        public async Task ScheduleDeleteFiles(int[] videoIds)
        {
            await GetQuartz();

            var jobDataMap = new JobDataMap
            {
                { "VideoIds", videoIds },
            };

            await quartz.ScheduleJob(
                JobBuilder.Create<DeleteFilesJob>()
                    .WithIdentity($"Delete files {Guid.NewGuid()}")
                    .UsingJobData(jobDataMap)
                    .Build(),
                TriggerBuilder.Create()
                    .StartNow()
                    .Build());
        }

        public async Task ScheduleDeleteSubscriptionFiles(int[] subscriptionIds, bool deleteSubscriptions)
        {
            await GetQuartz();

            var jobDataMap = new JobDataMap
            {
                { "SubscriptionIds", subscriptionIds },
                { "DeleteSubscriptions", deleteSubscriptions }
            };

            await quartz.ScheduleJob(
                JobBuilder.Create<DeleteSubscriptionFilesJob>()
                    .WithIdentity($"Delete subscription files {Guid.NewGuid()}")
                    .UsingJobData(jobDataMap)
                    .Build(),
                TriggerBuilder.Create()
                    .StartNow()
                    .Build());
        }
        public async Task ScheduleDeleteSubscriptionFolderFiles(int[] subscriptionFolderIds, bool deleteFolders)
        {
            await GetQuartz();

            var jobDataMap = new JobDataMap
            {
                { "SubscriptionFolderIds", subscriptionFolderIds },
                { "DeleteFolders", deleteFolders }
            };

            await quartz.ScheduleJob(
                JobBuilder.Create<DeleteSubscriptionFolderFilesJob>()
                    .WithIdentity($"Delete subscription folder files {Guid.NewGuid()}")
                    .UsingJobData(jobDataMap)
                    .Build(),
                TriggerBuilder.Create()
                    .StartNow()
                    .Build());
        }
    }
}
