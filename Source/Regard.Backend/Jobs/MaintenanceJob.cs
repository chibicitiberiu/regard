using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl.Matchers;
using Regard.Backend.Common.Utils;
using Regard.Backend.Configuration;
using Regard.Backend.DB;
using Regard.Backend.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Jobs
{
    /// <summary>
    /// Nightly housekeeping: snapshot the database, prune the job history and notifications, and clear
    /// out old yt-dlp stdout captures.
    ///
    /// Silent by design — not in <c>RegardScheduler.NotifiableJobTypes</c>, so it never posts to the
    /// bell. That is worth being careful about: <c>JobTrackerService.OnJobFailed</c> posts a card on
    /// terminal failure *without* checking <c>job.Notify</c>, and a job scheduled with no user id
    /// broadcasts to every account on the server. A sweep that dies on a full disk at 3am would
    /// therefore wake up the whole install. So <see cref="ExecuteJob"/> catches everything: each step is
    /// wrapped, and so is the body around them, because the option reads and the trigger scan sit
    /// outside the individual steps.
    /// </summary>
    [DisallowConcurrentExecution]
    public class MaintenanceJob : JobBase
    {
        private readonly IOptionManager optionManager;
        private readonly DatabaseBackupService backupService;
        private readonly NotificationService notificationService;
        private readonly HostThrottle hostThrottle;
        private readonly ISchedulerFactory schedulerFactory;
        private readonly IConfiguration configuration;

        public MaintenanceJob(ILogger<MaintenanceJob> logger,
                              DataContext dataContext,
                              JobTrackerService jobTrackerService,
                              IOptionManager optionManager,
                              DatabaseBackupService backupService,
                              NotificationService notificationService,
                              HostThrottle hostThrottle,
                              ISchedulerFactory schedulerFactory,
                              IConfiguration configuration)
            : base(logger, dataContext, jobTrackerService)
        {
            this.optionManager = optionManager;
            this.backupService = backupService;
            this.notificationService = notificationService;
            this.hostThrottle = hostThrottle;
            this.schedulerFactory = schedulerFactory;
            this.configuration = configuration;
        }

        public static Task Schedule(RegardScheduler scheduler, DateTimeOffset start, TimeSpan interval)
        {
            return scheduler.Schedule<MaintenanceJob>(
                name: "Database maintenance",
                start: start,
                repeatInterval: interval,
                retryCount: 0);
        }

        protected override async Task ExecuteJob(IJobExecutionContext context)
        {
            try
            {
                if (!optionManager.GetGlobal(Options.Server_Maintenance_Enabled))
                {
                    JobLog("Maintenance is disabled; nothing to do.");
                    return;
                }

                await RunBackup();
                await PruneJobHistory();
                PruneNotifications();
                PruneYtdlLogs();

                // Only after the work, and only on a clean pass: this timestamp is what stops a server
                // that restarts more often than the interval from never sweeping at all, so recording it
                // for a run that failed would suppress the retry.
                optionManager.SetGlobal(Options.Server_Maintenance_LastRunUtc,
                    DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                // Never propagate. See the class comment: a thrown maintenance job notifies every user.
                log.LogError(ex, "Maintenance sweep failed.");
                JobLog($"Maintenance sweep failed: {ex.Message}", Common.Model.MessageSeverity.Error);
            }
        }

        // ------------------------------------------------------------------ steps

        private async Task RunBackup()
        {
            try
            {
                if (!optionManager.GetGlobal(Options.Server_Backup_Enabled))
                    return;

                var outcome = await backupService.CreateBackupAsync();
                JobLog($"Backup: {outcome.Describe()}");

                if (outcome.Succeeded)
                {
                    int removed = backupService.ApplyRetention(
                        optionManager.GetGlobal(Options.Server_Backup_KeepCount));
                    if (removed > 0)
                        JobLog($"Removed {removed} old snapshot(s).");
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Maintenance: backup step failed.");
                JobLog($"Backup step failed: {ex.Message}", Common.Model.MessageSeverity.Error);
            }
        }

        private async Task PruneJobHistory()
        {
            try
            {
                // Deferral is per-step, not per-job. Taking a snapshot is a read transaction and does
                // not block writers, so gating the whole sweep on activity would mean a busy server
                // never got backed up. This step is the one that writes: a bulk DELETE against SQLite
                // while downloads and syncs are hammering it is how you get the disk-I/O errors that
                // wedge every request. It costs nothing to do it on the next pass instead.
                if (BackgroundWorkGate.IsBusy(hostThrottle, dataContext, out string busy))
                {
                    log.LogDebug("Maintenance: job pruning deferred, {0}.", busy);
                    JobLog($"Skipped job pruning this pass: {busy}.");
                    return;
                }

                // Which rows are live rather than history. If this cannot be determined, skip the prune
                // entirely rather than guess — deleting a row a trigger still points at kills that
                // recurring job until the next restart.
                ISet<long> liveIds;
                try
                {
                    liveIds = await LiveJobIds();
                }
                catch (Exception ex)
                {
                    log.LogWarning(ex, "Maintenance: could not enumerate live triggers; skipping job prune.");
                    JobLog("Skipped job pruning: the live trigger set could not be read.",
                           Common.Model.MessageSeverity.Warning);
                    return;
                }

                int pruned = jobTrackerService.PruneOldJobs(
                    optionManager.GetGlobal(Options.Server_JobHistoryRetentionDays), liveIds);

                if (pruned > 0)
                    JobLog($"Pruned {pruned} old job(s) from the history.");
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Maintenance: job pruning failed.");
                JobLog($"Job pruning failed: {ex.Message}", Common.Model.MessageSeverity.Error);
            }
        }

        private void PruneNotifications()
        {
            try
            {
                int pruned = notificationService.PruneOld(
                    optionManager.GetGlobal(Options.Server_NotificationRetentionDays));
                if (pruned > 0)
                    JobLog($"Pruned {pruned} old notification(s).");
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Maintenance: notification pruning failed.");
                JobLog($"Notification pruning failed: {ex.Message}", Common.Model.MessageSeverity.Error);
            }
        }

        /// <summary>
        /// The one-file-per-invocation yt-dlp stdout captures. NLog does not write these, so its own
        /// retention never sees them and nothing else ever removed them.
        /// </summary>
        private void PruneYtdlLogs()
        {
            try
            {
                int retentionDays = optionManager.GetGlobal(Options.Server_Maintenance_YtdlLogRetentionDays);
                if (retentionDays <= 0)
                    return;

                var dataDirectory = configuration["DataDirectory"];
                if (string.IsNullOrEmpty(dataDirectory))
                    return;

                var directory = Path.Combine(dataDirectory, "Logs", "ytdl");
                if (!Directory.Exists(directory))
                    return;

                var files = new DirectoryInfo(directory)
                    .EnumerateFiles("*.txt")
                    .Select(f => (f.Name, (DateTimeOffset)new DateTimeOffset(f.LastWriteTimeUtc, TimeSpan.Zero)))
                    .ToList();

                var doomed = LogRetention.SelectForDeletion(files, DateTimeOffset.UtcNow, retentionDays);

                int removed = 0;
                long bytes = 0;
                foreach (var name in doomed)
                {
                    var path = Path.Combine(directory, name);
                    try
                    {
                        // Safe even if yt-dlp still holds it open: the writer appends per line and
                        // re-creates the file if it has gone.
                        long size = new FileInfo(path).Length;
                        File.Delete(path);
                        removed++;
                        bytes += size;
                    }
                    catch (Exception ex)
                    {
                        log.LogDebug(ex, "Could not delete {0}.", path);
                    }
                }

                if (removed > 0)
                    JobLog($"Removed {removed} yt-dlp log file(s), freeing {DatabaseBackupService.Bytes(bytes)}.");
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Maintenance: yt-dlp log cleanup failed.");
                JobLog($"yt-dlp log cleanup failed: {ex.Message}", Common.Model.MessageSeverity.Error);
            }
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// Every JobId a currently-scheduled Quartz trigger points at. Same trigger-to-JobId scan
        /// RegardScheduler.TryUnschedule uses. These rows are live state, not history, whatever their
        /// recorded state or age — a recurring job's row sits Completed between fires.
        /// </summary>
        private async Task<ISet<long>> LiveJobIds()
        {
            var live = new HashSet<long>();
            var quartz = await schedulerFactory.GetScheduler();

            foreach (var key in await quartz.GetTriggerKeys(GroupMatcher<TriggerKey>.AnyGroup()))
            {
                var trigger = await quartz.GetTrigger(key);
                if (trigger != null && trigger.JobDataMap.ContainsKey("JobId"))
                    live.Add(trigger.JobDataMap.GetLong("JobId"));
            }

            return live;
        }

        /// <summary>
        /// Whether the sweep is overdue, given when it last completed. Quartz's trigger store is
        /// in-memory, so InitJob re-schedules every recurring job at "now + N" on each boot — meaning a
        /// 24-hour interval on a machine that restarts daily would fire exactly never.
        /// </summary>
        public static bool IsOverdue(string lastRunUtc, DateTimeOffset nowUtc, TimeSpan interval)
        {
            if (string.IsNullOrWhiteSpace(lastRunUtc))
                return true;   // never run

            if (!DateTimeOffset.TryParse(lastRunUtc, CultureInfo.InvariantCulture,
                                         DateTimeStyles.RoundtripKind, out var last))
                return true;   // unparseable is treated as never run, not as "just ran"

            return nowUtc - last >= interval;
        }
    }
}
