using Microsoft.EntityFrameworkCore;
using Regard.Backend.Common.Model;
using Regard.Backend.DB;
using Regard.Backend.Jobs;
using System.Linq;

namespace Regard.Backend.Services
{
    /// <summary>
    /// "Is something more important happening right now?"
    ///
    /// Shared by the background jobs that are allowed to stand down: the metadata refresh and the
    /// housekeeping sweep. Extracted from RefreshMetadataJob, which had it as a private method closing
    /// over its own fields, so there was no way to reuse it without duplicating the logic — and a
    /// second copy of "what counts as busy" would drift.
    ///
    /// Note this deliberately does not use HostThrottle.HasDownloadPressure: that takes a hosting domain,
    /// and a maintenance job has no URL to derive one from. Walking GetStatus() answers the question for
    /// every host at once.
    /// </summary>
    public static class BackgroundWorkGate
    {
        public static bool IsBusy(HostThrottle hostThrottle, DataContext dataContext, out string reason)
        {
            if (hostThrottle != null)
            {
                foreach (var status in hostThrottle.GetStatus())
                {
                    if (status.InFlight > 0 || status.Queued > 0)
                    {
                        reason = $"{status.Host} has {status.InFlight} download(s) in flight and {status.Queued} queued";
                        return true;
                    }
                }
            }

            // Only Running counts. A recurring sync's row sits in Scheduled between runs, so testing for
            // Scheduled would defer forever.
            bool syncing = dataContext != null && dataContext.Jobs.AsQueryable()
                .Any(j => j.Key == nameof(SynchronizeJob) && j.State == JobState.Running);
            if (syncing)
            {
                reason = "a subscription sync is running";
                return true;
            }

            reason = null;
            return false;
        }
    }
}
