using Humanizer;
using Microsoft.Extensions.Logging;
using Quartz;
using Regard.Backend.Downloader;
using Regard.Backend.Jobs;
using Regard.Backend.Thumbnails;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    public class RegardScheduler
    {
        private ILogger log;
        private IScheduler quartz;
        private readonly ISchedulerFactory schedulerFactory;

        public event Action<int> ScheduledVideoDownload;

        public RegardScheduler(ILogger log, IScheduler quartz)
        {
            this.log = log;
            this.quartz = quartz;
        }

        public RegardScheduler(ILogger<RegardScheduler> log, ISchedulerFactory schedulerFactory)
        {
            this.log = log;
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

            log.LogInformation("Scheduled global synchronization job (schedule {0}).", cronSchedule);
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
            {
                await quartz.ScheduleJob(job, trigger);
                log.LogInformation("Scheduled global synchronization job.");
            }
            else log.LogInformation("Did not schedule global synchronization job - already scheduled.");
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
            {
                await quartz.ScheduleJob(job, trigger);
                log.LogInformation("Scheduled synchronization job for subscription {0}.", subscriptionId);
            }
            else log.LogInformation("Did not schedule synchronization job for subscription {0} - already scheduled.", subscriptionId);
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
            {
                await quartz.ScheduleJob(job, trigger);
                log.LogInformation("Scheduled synchronization job for folder {0}.", folderId);
            }
            else log.LogInformation("Did not Schedulee synchronization job for folder {0} - already scheduled.", folderId);
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

            log.LogInformation("Scheduled youtube-dl update job, interval {0} starting at {1}.", interval, start);
        }

        public async Task ScheduleFetchThumbnails(DateTimeOffset start, TimeSpan interval)
        {
            await GetQuartz();

            await quartz.ScheduleJob(
                JobBuilder.Create<FetchThumbnailsJob>()
                    .WithIdentity("Fetch thumbnails")
                    .Build(),
                TriggerBuilder.Create()
                    .WithSimpleSchedule(sched => sched.WithInterval(interval).RepeatForever())
                    .StartAt(start)
                    .Build());

            log.LogInformation("Scheduled fetch thumbnails job, interval {0} starting at {1}.", interval, start);
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

            try
            {
                await quartz.ScheduleJob(retryJob, retryTrigger);
            } 
            catch (ObjectAlreadyExistsException)
            {
                // NOOP
            }

            log.LogInformation($"Scheduled attempt #{attempt} for job {jobDetail.Key.Name}, which will be done in {retryInterval}.");
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
            {
                await quartz.ScheduleJob(job, trigger);
                log.LogInformation("Scheduled download job for video {0}.", videoId);
                ScheduledVideoDownload?.Invoke(videoId);
            }
            else log.LogInformation("Did not schedule download job for video {0} - already scheduled.", videoId);
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

            log.LogInformation("Scheduled delete files job for videos {0}.", videoIds.Humanize());
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

            log.LogInformation("Scheduled delete files job for subscriptions {0}, will {1}delete subscriptions.", 
                subscriptionIds.Humanize(), deleteSubscriptions ? "" : "not ");
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

            log.LogInformation("Scheduled delete files job for folders {0}, will {1}delete folders.",
                subscriptionFolderIds.Humanize(), deleteFolders ? "" : "not ");
        }
    }
}
