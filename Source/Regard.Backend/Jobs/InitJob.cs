using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quartz;
using Regard.Backend.Common.Services;
using Regard.Backend.DB;
using Regard.Backend.Services;
using Regard.Backend.Thumbnails;
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
        private readonly RegardScheduler scheduler;
        private readonly JobTrackerService jobTracker;
        private readonly NotificationService notificationService;
        private readonly Configuration.IOptionManager optionManager;

        public InitJob(ILogger<InitJob> logger,
                       IConfiguration configuration,
                       DataContext dataContext,
                       IProviderManager providerManager,
                       IYoutubeDlService ytdlService,
                       RegardScheduler scheduler,
                       JobTrackerService jobTracker,
                       NotificationService notificationService,
                       Configuration.IOptionManager optionManager)
        {
            this.log = logger;
            this.configuration = configuration;
            this.dataContext = dataContext;
            this.providerManager = providerManager;
            this.ytdlService = ytdlService;
            this.scheduler = scheduler;
            this.jobTracker = jobTracker;
            this.notificationService = notificationService;
            this.optionManager = optionManager;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            log.LogInformation("Running initialization tasks...");

            // Prune old Job Log history so it doesn't grow unbounded (all jobs are tracked).
            try
            {
                int retention = optionManager.GetGlobal(Configuration.Options.Server_JobHistoryRetentionDays);
                int pruned = jobTracker.PruneOldJobs(retention);
                if (pruned > 0)
                    log.LogInformation("Pruned {0} old job(s) from history.", pruned);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to prune old jobs.");
            }

            // Notifications: drop any leftover "in progress" ones (a crash mid-job would strand them as
            // stale), then age out old ones (kept shorter than the Job Log so failures stay inspectable).
            try
            {
                int cleared = notificationService.ClearStaleOngoing();
                if (cleared > 0)
                    log.LogInformation("Cleared {0} stale in-progress notification(s).", cleared);

                int nRetention = optionManager.GetGlobal(Configuration.Options.Server_NotificationRetentionDays);
                int nPruned = notificationService.PruneOld(nRetention);
                if (nPruned > 0)
                    log.LogInformation("Pruned {0} old notification(s).", nPruned);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to prune notifications.");
            }

            // Initialize providers
            await providerManager.Initialize();

            await ytdlService.Initialize();

            // Create basic jobs
            await SynchronizeJob.ScheduleGlobal(scheduler, configuration["SynchronizationSchedule"]);
            await YoutubeDLUpdateJob.Schedule(scheduler, DateTimeOffset.Now.AddSeconds(10), TimeSpan.FromDays(1));
            await FetchThumbnailsJob.Schedule(scheduler, DateTimeOffset.Now.AddSeconds(30), TimeSpan.FromMinutes(30));
            await ProcessScheduledDeletionsJob.Schedule(scheduler, DateTimeOffset.Now.AddMinutes(1), TimeSpan.FromMinutes(5));

            // Jellyfin watched-sync (opt-in). Guard + validate the cron: RegardScheduler.Schedule
            // re-throws on an invalid/empty cron, which would fail the whole init.
            var jellyfinCron = configuration["Jellyfin:PollSchedule"];
            if (configuration.GetValue<bool>("Jellyfin:Enabled") && Quartz.CronExpression.IsValidExpression(jellyfinCron))
            {
                try { await JellyfinSyncJob.Schedule(scheduler, jellyfinCron); }
                catch (Exception ex) { log.LogError(ex, "Failed to schedule Jellyfin sync."); }
            }

            log.LogInformation("Initialization tasks completed!");
        }
    }
}
