using Regard.Backend.Common.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Regard.Backend.Common.Utils
{
    /// <summary>
    /// Which rows in the job history are safe to delete.
    ///
    /// This was inline in <c>JobTrackerService.PruneOldJobs</c> and only ever ran at boot, where being
    /// slightly wrong was harmless: the recurring jobs were re-created immediately afterwards. Running
    /// the same sweep on a live server is not harmless, because of how recurring jobs are stored.
    ///
    /// A recurring job gets ONE JobInfo row at schedule time. Every subsequent fire reuses it, and
    /// between fires it sits in <see cref="JobState.Completed"/> — indistinguishable, by state and age,
    /// from a finished one-shot job. Delete it and the next fire looks up its JobId, finds nothing, and
    /// throws "Invalid job ID"; that recurring job is then dead until the process restarts, with only a
    /// Quartz-level error to show for it. The daily yt-dlp update job would be the first casualty.
    ///
    /// The obvious guard — skip rows whose Key names a recurring job type — does not work. Key is the
    /// bare type name, so a sync a user started by hand and the nightly sync carry the same one; that
    /// rule would pin roughly half the table forever and grow it by a few rows on every restart. So the
    /// caller passes the set of JobIds that a live Quartz trigger actually points at, and those are
    /// excluded. That is exact, and it protects retry-pending and throttle-deferred rows for free.
    /// </summary>
    public static class JobPruneFilter
    {
        /// <summary>
        /// Failed jobs are kept this many times longer than completed ones, so a problem stays visible
        /// well after the routine successes around it have aged out.
        /// </summary>
        public const int FailedRetentionMultiplier = 3;

        public interface IPrunableRow
        {
            long Id { get; }
            JobState State { get; }
            DateTimeOffset? Completed { get; }
        }

        /// <summary>
        /// Returns the ids to delete. <paramref name="retentionDays"/> of zero or less disables pruning
        /// entirely rather than deleting everything — the destructive reading of "0" is not the useful
        /// one, and this value comes from a number an admin types into a box.
        /// </summary>
        /// <param name="rows">Candidate rows. Only terminal states are ever considered.</param>
        /// <param name="protectedIds">
        /// Ids a live Quartz trigger points at. Never deleted, whatever their age.
        /// </param>
        public static IReadOnlyList<long> SelectPrunable<T>(IEnumerable<T> rows,
                                                            DateTimeOffset nowUtc,
                                                            int retentionDays,
                                                            ISet<long> protectedIds = null)
            where T : IPrunableRow
        {
            if (rows == null || retentionDays <= 0)
                return Array.Empty<long>();

            var completedCutoff = nowUtc.AddDays(-retentionDays);
            var failedCutoff = nowUtc.AddDays(-retentionDays * (double)FailedRetentionMultiplier);

            return rows
                .Where(r => protectedIds == null || !protectedIds.Contains(r.Id))
                .Where(r => IsExpired(r, completedCutoff, failedCutoff))
                .Select(r => r.Id)
                .ToList();
        }

        private static bool IsExpired(IPrunableRow row, DateTimeOffset completedCutoff, DateTimeOffset failedCutoff)
        {
            // A terminal row with no completion time has nothing to measure age against. Leave it:
            // deleting on "we don't know how old it is" is the wrong default for history.
            if (row.Completed == null)
                return false;

            switch (row.State)
            {
                case JobState.Completed:
                    return row.Completed < completedCutoff;

                case JobState.Failed:
                    return row.Completed < failedCutoff;

                // Cancelled rows were left out of the original prune, so they accumulated without
                // limit — a fifth of the job table on the dev install, growing by a handful every
                // restart because the boot reconciliation sweep cancels whatever the previous run left
                // stranded. They are the least interesting rows in the history, so they age out on the
                // same clock as completed ones.
                case JobState.Cancelled:
                    return row.Completed < completedCutoff;

                // Created / Scheduled / Running are not terminal. A Running row might belong to a job
                // executing right now, and a Scheduled one to a trigger that has not fired yet.
                default:
                    return false;
            }
        }
    }
}
