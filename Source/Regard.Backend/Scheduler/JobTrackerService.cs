using Microsoft.Extensions.DependencyInjection;
using Regard.Backend.Common.Model;
using Regard.Backend.DB;
using Regard.Backend.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Regard.Backend.Services
{
    #region Event args

    public class JobCreatedEventArgs
    {
        public JobInfo Job { get; set; }
    }

    public class JobScheduledEventArgs
    {
        public JobInfo Job { get; set; }

        public DateTimeOffset? NextRun { get; set; }
    }

    public class JobStartedEventArgs
    {
        public JobInfo Job { get; set; }
    }

    public class JobProgressEventArgs
    {
        public JobInfo Job { get; set; }

        public float Progress { get; set; }
    }

    public class JobCompletedEventArgs
    {
        public JobInfo Job { get; set; }
    }

    public class JobFailedEventArgs
    {
        public JobInfo Job { get; set; }
        public string Reason { get; set; }
        public string Details { get; set; }
    }

    public class JobCancelledEventArgs
    {
        public JobInfo Job { get; set; }
    }

    #endregion

    public class JobTrackerService
    {
        private readonly IServiceScopeFactory scopeFactory;
        private readonly UserLogger userLogger;
        private readonly NotificationService notificationService;
        private readonly DownloadCancellationRegistry cancellationRegistry;

        /// <summary>
        /// Transient live progress/step/log for currently running jobs. JobInfo.Progress/Detail are
        /// [NotMapped] and the Log column is only persisted at completion, so this in-memory store is
        /// what lets the Job Log show live progress and streaming output for a running job. Keyed by
        /// job id; entries are removed when the job finishes.
        /// </summary>
        public sealed class JobLiveState
        {
            public float? Progress { get; set; }
            public string Detail { get; set; }
            public string Log { get; set; }
        }

        private readonly ConcurrentDictionary<long, JobLiveState> liveJobs = new();

        /// <summary>Live progress/step/log for a running job, or null if none is tracked.</summary>
        public JobLiveState GetLive(long jobId) => liveJobs.TryGetValue(jobId, out var s) ? s : null;

        public event EventHandler<JobCreatedEventArgs> JobCreated;
        public event EventHandler<JobScheduledEventArgs> JobScheduled;
        public event EventHandler<JobStartedEventArgs> JobStarted;
        public event EventHandler<JobProgressEventArgs> JobProgress;
        public event EventHandler<JobCompletedEventArgs> JobCompleted;
        public event EventHandler<JobFailedEventArgs> JobFailed;
        public event EventHandler<JobCancelledEventArgs> JobCancelled;

        public JobTrackerService(IServiceScopeFactory scopeFactory,
                                 UserLogger userLogger,
                                 NotificationService notificationService,
                                 DownloadCancellationRegistry cancellationRegistry)
        {
            this.scopeFactory = scopeFactory;
            this.userLogger = userLogger;
            this.notificationService = notificationService;
            this.cancellationRegistry = cancellationRegistry;
        }

        private static string NotificationKey(JobInfo job) => $"job:{job.Id}";

        /// <summary>
        /// Posts/updates the live "in progress" notification for an important job. Fire-and-forget: the
        /// notification pipeline must never block or throw into job execution.
        /// </summary>
        private void PostOngoing(JobInfo job, JobNotification spec)
        {
            if (job == null || !job.Notify)
                return;

            _ = notificationService.PostOrUpdate(
                job.UserId,
                NotificationKey(job),
                spec?.Title ?? job.Name,
                spec?.Text ?? job.Detail,
                NotificationSeverity.Info,
                job.Progress,
                ongoing: true,
                videoDbId: spec?.VideoDbId,
                jobId: job.Id,
                primaryAction: NotificationPrimaryAction.None,
                cancellable: cancellationRegistry.IsCancellable(job.Id));
        }

        public JobInfo CreateJob(string name,
                                 string userId = null,
                                 bool trackWhenScheduled = false,
                                 IDictionary<string, object> jobData = null,
                                 int retryCount = 0,
                                 int retryIntervalSecs = 600,
                                 bool notify = false)
        {
            using var scope = scopeFactory.CreateScope();
            using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

            var job = new JobInfo()
            {
                UserId = userId,
                Name = name,
                TrackWhenScheduled = trackWhenScheduled,
                Notify = notify,
                JobData = new Dictionary<string, object>(),
                RetryCount = retryCount,
                RetryInterval = retryIntervalSecs,
                State = JobState.Created,
                Created = DateTimeOffset.UtcNow,
            };

            if (jobData != null)
                job.JobData = new Dictionary<string, object>(jobData);

            // Remember the original retry budget so a failing job can show "Retry n/total".
            job.JobData["_initialRetry"] = retryCount;

            dataContext.Add(job);
            dataContext.SaveChanges();

            JobCreated?.Invoke(this, new JobCreatedEventArgs() { Job = job });

            return job;
        }

        public void OnJobScheduled(JobInfo job, DateTimeOffset? nextRun)
        {
            using var scope = scopeFactory.CreateScope();
            using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

            job.State = JobState.Scheduled;
            job.NextRun = nextRun;
            // Update(): this fresh scope doesn't track `job` (it was created in the caller's scope), so a
            // plain SaveChanges would persist nothing — including job.Key, without which ScheduleJobRetry
            // and any Quartz JobKey lookup throw "Name cannot be null".
            dataContext.Jobs.Update(job);
            dataContext.SaveChanges();

            JobScheduled?.Invoke(this, new JobScheduledEventArgs() { Job = job, NextRun = nextRun });
        }

        public void OnJobStarted(JobInfo job, JobNotification ongoing = null)
        {
            using var scope = scopeFactory.CreateScope();
            using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

            job.State = JobState.Running;
            job.Started = DateTimeOffset.UtcNow;
            // Update(): this fresh scope doesn't track `job`, so a plain SaveChanges would persist nothing
            // and the Job Log would keep showing "Scheduled" for a job that's actually running.
            dataContext.Jobs.Update(job);
            dataContext.SaveChanges();

            // Surface the job as a live "in progress" notification (important jobs only).
            PostOngoing(job, ongoing);
            JobStarted?.Invoke(this, new JobStartedEventArgs() { Job = job });
        }

        public void OnJobProgress(long jobId, float progress, string detail = null, JobNotification ongoing = null, string liveLog = null)
        {
            using var scope = scopeFactory.CreateScope();
            using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

            var job = dataContext.Jobs.Find(jobId);
            if (job == null)
                return;

            // Reloaded from the DB (a fresh instance), so the persisted State may lag behind the
            // in-memory Running set by JobBase — a progress tick means the job is running, so say so.
            job.State = JobState.Running;
            job.Progress = progress;
            job.Detail = detail;

            // Progress/Detail are [NotMapped] and the Log column only lands at completion, so stash the
            // live values here for the Job Log to read (see JobLiveState). No DB write — nothing to persist.
            var live = liveJobs.GetOrAdd(jobId, _ => new JobLiveState());
            live.Progress = progress;
            live.Detail = detail;
            if (liveLog != null)
                live.Log = liveLog;

            PostOngoing(job, ongoing);
            JobProgress?.Invoke(this, new JobProgressEventArgs() { Job = job, Progress = progress });
        }

        public void OnJobCompleted(JobInfo job, JobNotification success = null)
        {
            using var scope = scopeFactory.CreateScope();
            using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

            job.State = JobState.Completed;
            job.Completed = DateTimeOffset.UtcNow;
            // Update(): this fresh scope doesn't track `job`, so without it SaveChanges persisted
            // nothing and a finished job stayed "Running" in the database (OnJobStarted got this right;
            // the terminal handlers didn't).
            dataContext.Jobs.Update(job);
            dataContext.SaveChanges();
            liveJobs.TryRemove(job.Id, out _);

            // A finished important job either shows an informative terminal notification (when the job
            // supplied one — e.g. "Download complete", click to open the video) or silently clears its
            // in-progress notification (background/maintenance jobs).
            if (job.Notify)
            {
                if (success != null)
                    _ = notificationService.PostOrUpdate(
                        job.UserId, NotificationKey(job),
                        success.Title, success.Text, NotificationSeverity.Success,
                        progress: null, ongoing: false,
                        videoDbId: success.VideoDbId, jobId: job.Id,
                        primaryAction: success.PrimaryAction, cancellable: false);
                else
                    _ = notificationService.Remove(job.UserId, NotificationKey(job));
            }

            JobCompleted?.Invoke(this, new JobCompletedEventArgs() { Job = job });
        }

        public void OnJobCancelled(JobInfo job)
        {
            using var scope = scopeFactory.CreateScope();
            using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

            job.State = JobState.Cancelled;
            job.Completed = DateTimeOffset.UtcNow;
            dataContext.Jobs.Update(job);
            dataContext.SaveChanges();
            liveJobs.TryRemove(job.Id, out _);

            userLogger.LogInfo($"{job.Name}: cancelled", userId: job.UserId, jobId: job.Id);
            if (job.Notify)
                _ = notificationService.Remove(job.UserId, NotificationKey(job));
            JobCancelled?.Invoke(this, new JobCancelledEventArgs() { Job = job });
        }

        public void OnJobFailed(JobInfo job, string reason, string details = null, JobNotification failure = null)
        {
            using var scope = scopeFactory.CreateScope();
            using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

            job.State = JobState.Failed;
            job.Completed = DateTimeOffset.UtcNow;
            dataContext.Jobs.Update(job);
            dataContext.SaveChanges();
            liveJobs.TryRemove(job.Id, out _);

            // Keep a Message row for the (vestigial) Messages table; nothing displays it live anymore.
            userLogger.LogError($"{job.Name}: {reason}", details, userId: job.UserId, jobId: job.Id);

            // Emit the terminal failure notification ONLY when no retries remain. RetryCount is read
            // here, BEFORE RegardScheduler's JobFailed handler decrements it — so >0 means an attempt is
            // still pending (leave the in-progress notification up), and <=0 means this was the final
            // attempt. This is what stops a 3-retry download from firing up to 4 "failed" notifications.
            if (job.RetryCount <= 0)
            {
                _ = notificationService.PostOrUpdate(
                    job.UserId, NotificationKey(job),
                    failure?.Title ?? job.Name,
                    failure?.Text ?? reason,
                    NotificationSeverity.Error,
                    progress: null, ongoing: false,
                    videoDbId: failure?.VideoDbId, jobId: job.Id,
                    primaryAction: NotificationPrimaryAction.OpenLogs, cancellable: false);
            }
            else if (job.Notify)
            {
                // A retry is still pending — replace the "in progress" card with a live "Retrying (n/total)"
                // status so it doesn't look stuck. RetryCount is pre-decrement here, so it names the retry
                // about to be scheduled.
                int total = ReadInitialRetry(job);
                string label = total > 0 ? $"Retrying ({total - job.RetryCount + 1}/{total})" : "Retrying";
                _ = notificationService.PostOrUpdate(
                    job.UserId, NotificationKey(job),
                    label,
                    failure?.Text ?? job.Name,
                    NotificationSeverity.Warning,
                    progress: null, ongoing: true,
                    videoDbId: failure?.VideoDbId, jobId: job.Id,
                    primaryAction: NotificationPrimaryAction.OpenLogs, cancellable: false);
            }

            JobFailed?.Invoke(this, new JobFailedEventArgs() { Job = job, Reason = reason, Details = details });
        }

        /// <summary>
        /// Jobs left in a non-terminal state (Created/Scheduled/Running). After a restart these are
        /// orphans — Quartz's in-memory store dropped their triggers, so nothing will ever fire them.
        /// The startup reconciliation sweep (InitJob) resumes or abandons each one.
        /// </summary>
        public IReadOnlyList<JobInfo> GetOrphanedJobs()
        {
            using var scope = scopeFactory.CreateScope();
            using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

            return dataContext.Jobs
                .Where(j => j.State == JobState.Created
                         || j.State == JobState.Scheduled
                         || j.State == JobState.Running)
                .ToList();
        }

        /// <summary>
        /// Marks an orphaned job terminal (Cancelled) with a note, for jobs that shouldn't be resumed
        /// after a restart (a fresh periodic run covers them). Writes the row directly — it deliberately
        /// does NOT raise JobFailed, so this never triggers a retry.
        /// </summary>
        public void AbandonJob(JobInfo job, string reason)
        {
            using var scope = scopeFactory.CreateScope();
            using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

            job.State = JobState.Cancelled;
            job.Completed = DateTimeOffset.UtcNow;
            if (!string.IsNullOrEmpty(reason))
                job.Log = string.IsNullOrEmpty(job.Log) ? reason : job.Log + "\n" + reason;

            // Update(): this fresh scope doesn't track `job` (loaded in GetOrphanedJobs' disposed scope).
            dataContext.Jobs.Update(job);
            dataContext.SaveChanges();
            liveJobs.TryRemove(job.Id, out _);

            // Drop any leftover in-progress card (ClearStaleOngoing usually beat us to it; be safe).
            if (job.Notify)
                _ = notificationService.Remove(job.UserId, NotificationKey(job));
        }

        private static int ReadInitialRetry(JobInfo job)
        {
            try
            {
                if (job.JobData != null && job.JobData.TryGetValue("_initialRetry", out var v) && v != null)
                    return Convert.ToInt32(v);
            }
            catch { /* JobData round-trips through JSON; ignore odd shapes */ }
            return 0;
        }

        /// <summary>
        /// Deletes old finished jobs to keep the history bounded (all jobs are tracked). Completed
        /// jobs are pruned past <paramref name="retentionDays"/>; failed jobs are kept ~3x longer so
        /// problems stay visible. Linked messages cascade-delete (see DataContext Message→Job FK).
        /// </summary>
        public int PruneOldJobs(int retentionDays)
        {
            if (retentionDays <= 0)
                return 0;

            using var scope = scopeFactory.CreateScope();
            using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

            var completedCutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);
            var failedCutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays * 3);

            // Filter State server-side (translatable), then compare DateTimeOffset client-side — the
            // SQLite provider can't translate ordering comparisons on DateTimeOffset.
            var stale = dataContext.Jobs
                .Where(j => (j.State == JobState.Completed || j.State == JobState.Failed) && j.Completed != null)
                .AsEnumerable()
                .Where(j => (j.State == JobState.Completed && j.Completed < completedCutoff)
                         || (j.State == JobState.Failed && j.Completed < failedCutoff))
                .ToList();

            if (stale.Count == 0)
                return 0;

            dataContext.Jobs.RemoveRange(stale);
            dataContext.SaveChanges();
            return stale.Count;
        }
    }
}
