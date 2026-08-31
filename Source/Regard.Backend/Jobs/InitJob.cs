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

            // Reconcile jobs stranded by the restart. Quartz's trigger store is in-memory, so any job left
            // non-terminal (Created/Scheduled/Running) has no trigger to fire it — it would sit "Scheduled"
            // forever. Resume the types that opt in ([ResumeAfterRestart]: downloads, imports, deletions,
            // account deletion) from their persisted row; abandon the rest (the recurring/maintenance jobs
            // re-scheduled fresh just below cover them anyway). Runs BEFORE the recurring re-schedule so a
            // previous run's stale recurring row is abandoned before its replacement is created.
            try
            {
                var orphans = jobTracker.GetOrphanedJobs();
                if (orphans.Count > 0)
                {
                    int resumed = 0, abandoned = 0;
                    foreach (var job in orphans)
                    {
                        try
                        {
                            if (await scheduler.TryResume(job))
                                resumed++;
                            else
                            {
                                jobTracker.AbandonJob(job, "Interrupted by server restart; not resumed.");
                                abandoned++;
                            }
                        }
                        catch (Exception ex)
                        {
                            log.LogError(ex, "Failed to resume job {0} ({1}); abandoning it.", job.Id, job.Name);
                            try
                            {
                                jobTracker.AbandonJob(job, "Interrupted by server restart; resume failed.");
                                abandoned++;
                            }
                            catch (Exception ex2)
                            {
                                log.LogError(ex2, "Failed to abandon job {0}.", job.Id);
                            }
                        }
                    }
                    log.LogInformation("Reconciled {0} orphaned job(s) after restart: {1} resumed, {2} abandoned.",
                        orphans.Count, resumed, abandoned);
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to reconcile orphaned jobs after restart.");
            }

            // Create basic jobs
            await SynchronizeJob.ScheduleGlobal(scheduler, configuration["SynchronizationSchedule"]);
            await YoutubeDLUpdateJob.Schedule(scheduler, DateTimeOffset.Now.AddSeconds(10), TimeSpan.FromDays(1));
            await FetchThumbnailsJob.Schedule(scheduler, DateTimeOffset.Now.AddSeconds(30), TimeSpan.FromMinutes(30));
            await ProcessScheduledDeletionsJob.Schedule(scheduler, DateTimeOffset.Now.AddMinutes(1), TimeSpan.FromMinutes(5));

            // Background metadata refresh. Started well after boot so it never competes with the
            // restart sweep re-queueing interrupted downloads, and it defers itself whenever a download
            // or sync is active. The master switch is checked in the job, not here, so toggling the
            // option takes effect without a restart.
            int refreshMinutes = Math.Max(5, optionManager.GetGlobal(Configuration.Options.Server_MetadataRefresh_IntervalMinutes));
            await RefreshMetadataJob.Schedule(scheduler,
                                              DateTimeOffset.Now.AddMinutes(2),
                                              TimeSpan.FromMinutes(refreshMinutes));

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
