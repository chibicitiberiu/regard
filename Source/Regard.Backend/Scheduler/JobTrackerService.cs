using Microsoft.Extensions.DependencyInjection;
using Regard.Backend.Common.Model;
using Regard.Backend.DB;
using Regard.Backend.Logging;
using System;
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

        public event EventHandler<JobCreatedEventArgs> JobCreated;
        public event EventHandler<JobScheduledEventArgs> JobScheduled;
        public event EventHandler<JobStartedEventArgs> JobStarted;
        public event EventHandler<JobProgressEventArgs> JobProgress;
        public event EventHandler<JobCompletedEventArgs> JobCompleted;
        public event EventHandler<JobFailedEventArgs> JobFailed;
        public event EventHandler<JobCancelledEventArgs> JobCancelled;

        public JobTrackerService(IServiceScopeFactory scopeFactory, UserLogger userLogger)
        {
            this.scopeFactory = scopeFactory;
            this.userLogger = userLogger;
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
            dataContext.SaveChanges();

            JobScheduled?.Invoke(this, new JobScheduledEventArgs() { Job = job, NextRun = nextRun });
        }

        public void OnJobStarted(JobInfo job)
        {
            using var scope = scopeFactory.CreateScope();
            using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

            job.State = JobState.Running;
            job.Started = DateTimeOffset.UtcNow;
            dataContext.SaveChanges();

            userLogger.LogInfo("Job started", userId: job.UserId, jobId: job.Id);
            JobStarted?.Invoke(this, new JobStartedEventArgs() { Job = job });
        }

        public void OnJobProgress(long jobId, float progress, string detail = null)
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

            JobProgress?.Invoke(this, new JobProgressEventArgs() { Job = job, Progress = progress });
        }

        public void OnJobCompleted(JobInfo job, bool notifyUser = false)
        {
            using var scope = scopeFactory.CreateScope();
            using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

            job.State = JobState.Completed;
            job.Completed = DateTimeOffset.UtcNow;
            dataContext.SaveChanges();

            userLogger.LogInfo("Job completed", userId: job.UserId, jobId: job.Id);
            JobCompleted?.Invoke(this, new JobCompletedEventArgs() { Job = job });
        }

        public void OnJobCancelled(JobInfo job)
        {
            using var scope = scopeFactory.CreateScope();
            using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

            job.State = JobState.Cancelled;
            job.Completed = DateTimeOffset.UtcNow;
            dataContext.SaveChanges();

            userLogger.LogInfo($"{job.Name}: cancelled", userId: job.UserId, jobId: job.Id);
            JobCancelled?.Invoke(this, new JobCancelledEventArgs() { Job = job });
        }

        public void OnJobFailed(JobInfo job, string reason, string details = null, bool notifyUser = false)
        {
            using var scope = scopeFactory.CreateScope();
            using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

            job.State = JobState.Failed;
            job.Completed = DateTimeOffset.UtcNow;
            dataContext.SaveChanges();

            // LogError (not LogInfo) so failures surface as an error toast, even for background jobs.
            userLogger.LogError($"{job.Name}: {reason}", details, userId: job.UserId, jobId: job.Id);
            JobFailed?.Invoke(this, new JobFailedEventArgs() { Job = job, Reason = reason, Details = details });
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
