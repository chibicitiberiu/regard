using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Quartz;
using Regard.Backend.Common.Model;
using Regard.Backend.Common.Services;
using Regard.Backend.DB;
using Regard.Backend.Model;
using Regard.Backend.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Jobs
{
    /// <summary>
    /// Deletes a user account and everything it owns. Runs as a background job because the teardown
    /// must happen in a specific order: <c>Subscription.User</c> is <c>OnDelete(Restrict)</c>, so
    /// <see cref="UserManager{T}.DeleteAsync"/> throws while the user still owns any subscription.
    /// We therefore (1) delete the downloaded files, (2) remove the subscriptions (cascading their
    /// video rows), (3) remove the folders leaf-first, then (4) delete the account (cascading the
    /// remaining options/messages/job history). Doing it all in one job execution avoids any
    /// cross-job sequencing under the single Quartz worker.
    /// </summary>
    // Resume after a restart: no periodic backstop, and the ordered teardown re-checks the account and
    // each step is a plain delete, so re-running from the row finishes an interrupted account deletion.
    [ResumeAfterRestart]
    public class DeleteUserJob : JobBase
    {
        public static readonly string Data_TargetUserId = "TargetUserId";

        private readonly UserManager<UserAccount> userManager;
        private readonly SubscriptionManager subscriptionManager;
        private readonly IVideoStorageService videoStorage;

        public DeleteUserJob(ILogger<DeleteUserJob> log,
                             DataContext dataContext,
                             JobTrackerService jobTrackerService,
                             UserManager<UserAccount> userManager,
                             SubscriptionManager subscriptionManager,
                             IVideoStorageService videoStorage)
            : base(log, dataContext, jobTrackerService)
        {
            this.userManager = userManager;
            this.subscriptionManager = subscriptionManager;
            this.videoStorage = videoStorage;
        }

        /// <summary>
        /// Schedules a user deletion. The job is owned by <paramref name="requestingUserId"/> (the
        /// admin), NOT the target — the target's job history is cascade-deleted, so making the target
        /// the owner would delete this very job's row mid-run.
        /// </summary>
        public static Task<DateTimeOffset> Schedule(RegardScheduler scheduler, string requestingUserId, string targetUserId)
        {
            return scheduler.Schedule<DeleteUserJob>(
                name: "Delete user",
                userId: requestingUserId,
                jobData: new Dictionary<string, object>
                {
                    [Data_TargetUserId] = targetUserId,
                },
                retryCount: 0,
                retryIntervalSecs: 0);
        }

        protected override async Task ExecuteJob(IJobExecutionContext context)
        {
            var targetUserId = Job.JobData.TryGetValue(Data_TargetUserId, out var t) ? t?.ToString() : null;
            if (string.IsNullOrEmpty(targetUserId))
            {
                JobLog("Delete failed: no user id.", MessageSeverity.Error);
                return;
            }

            var user = await userManager.FindByIdAsync(targetUserId);
            if (user == null)
            {
                JobLog("Delete failed: user not found (already deleted?).", MessageSeverity.Warning);
                return;
            }

            JobLog($"Deleting user {user.UserName}…");

            // 1. Remove downloaded files off disk.
            ReportProgress(0f, "Removing downloaded files");
            var downloadedVideos = dataContext.Videos.AsQueryable()
                .Where(x => x.Subscription.UserId == user.Id)
                .Where(x => x.DownloadedPath != null)
                .ToList();
            int done = 0;
            foreach (var video in downloadedVideos)
            {
                try { await videoStorage.Delete(video); }
                catch (Exception ex) { JobLog($"Could not delete files for {video}: {ex.Message}", MessageSeverity.Warning); }
                done++;
                ReportProgress(downloadedVideos.Count > 0 ? 0.7f * done / downloadedVideos.Count : 0.7f, "Removing downloaded files");
            }

            // 2. Remove subscriptions (cascade-deletes their video rows + sub options/filters).
            ReportProgress(0.75f, "Removing subscriptions");
            var subIds = dataContext.Subscriptions.AsQueryable()
                .Where(x => x.UserId == user.Id)
                .Select(x => x.Id)
                .ToArray();
            if (subIds.Length > 0)
                await subscriptionManager.Delete(user, subIds, deleteFiles: false);

            // 3. Remove folders leaf-first (the folder self-parent FK is Restrict, so deleting a
            //    parent before its children would fail).
            ReportProgress(0.85f, "Removing folders");
            var folders = dataContext.SubscriptionFolders.AsQueryable()
                .Where(x => x.UserId == user.Id)
                .ToList();
            while (folders.Count > 0)
            {
                var parentIds = folders.Where(f => f.ParentId != null).Select(f => f.ParentId.Value).ToHashSet();
                var leaves = folders.Where(f => !parentIds.Contains(f.Id)).ToList();
                if (leaves.Count == 0)
                    leaves = folders; // safety net against an unexpected cycle
                dataContext.SubscriptionFolders.RemoveRange(leaves);
                dataContext.SaveChanges();
                folders = folders.Except(leaves).ToList();
            }

            // 4. Delete the account (cascade removes options/messages/job history).
            ReportProgress(0.95f, "Removing account");
            var result = await userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                var err = string.Join("; ", result.Errors.Select(e => e.Description));
                JobLog($"Failed to delete account: {err}", MessageSeverity.Error);
                throw new Exception(err);
            }

            ReportProgress(1f, "Done");
            JobLog($"Deleted user {user.UserName}: {downloadedVideos.Count} file set(s), {subIds.Length} subscription(s).");
        }
    }
}
