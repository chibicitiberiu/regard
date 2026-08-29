using Microsoft.Extensions.Logging;
using Quartz;
using Regard.Backend.Configuration;
using Regard.Backend.DB;
using Regard.Backend.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Jobs
{
    /// <summary>
    /// Periodic sweep: deletes videos whose grace-period deletion time (<see cref="Model.Video.DeleteScheduledAt"/>)
    /// has passed. Reuses <see cref="DeleteWatchedFilesJob"/> (delete files + refill the download window).
    /// Silent by design — not in <c>RegardScheduler.NotifiableJobTypes</c>, so it never surfaces in the bell.
    /// </summary>
    [DisallowConcurrentExecution]
    public class ProcessScheduledDeletionsJob : JobBase
    {
        private readonly RegardScheduler scheduler;
        private readonly IOptionManager optionManager;

        public ProcessScheduledDeletionsJob(ILogger<ProcessScheduledDeletionsJob> logger,
                                            DataContext dataContext,
                                            JobTrackerService jobTrackerService,
                                            RegardScheduler scheduler,
                                            IOptionManager optionManager)
            : base(logger, dataContext, jobTrackerService)
        {
            this.scheduler = scheduler;
            this.optionManager = optionManager;
        }

        public static Task Schedule(RegardScheduler scheduler, DateTimeOffset start, TimeSpan interval)
        {
            return scheduler.Schedule<ProcessScheduledDeletionsJob>(
                name: "Process scheduled deletions",
                start: start,
                repeatInterval: interval,
                retryCount: 0);
        }

        protected override async Task ExecuteJob(IJobExecutionContext context)
        {
            var now = DateTimeOffset.Now;

            // SQLite can't translate DateTimeOffset ordering comparisons in SQL (cf.
            // JobTrackerService.PruneOldJobs), so filter the translatable predicate server-side, then
            // compare the offset client-side.
            var due = dataContext.Videos.AsQueryable()
                .Where(v => v.DeleteScheduledAt != null && v.DownloadedPath != null)
                .AsEnumerable()
                .Where(v => v.DeleteScheduledAt <= now)
                .ToList();

            if (due.Count == 0)
                return;

            log.LogInformation("Processing {0} scheduled deletion(s).", due.Count);

            // Keep an unwatched (e.g. manually marked) video from being immediately re-downloaded once its
            // slot frees: apply the MarkDeletedAsWatched reverse rule first, exactly like manual DeleteFiles.
            bool changed = false;
            foreach (var v in due)
            {
                if (!v.IsWatched
                    && optionManager.GetForSubscription(Options.Subscriptions_MarkDeletedAsWatched, v.SubscriptionId))
                {
                    v.IsWatched = true;
                    changed = true;
                }
            }
            if (changed)
                await dataContext.SaveChangesAsync();

            await DeleteWatchedFilesJob.Schedule(scheduler, due.Select(v => v.Id).ToArray());
        }
    }
}
