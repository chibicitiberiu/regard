using Microsoft.Extensions.DependencyInjection;
using Regard.Backend.Common.Model;
using Regard.Backend.DB;
using Regard.Backend.Logging;
using System;
using System.Collections.Generic;

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
                                 int retryIntervalSecs = 600)
        {
            using var scope = scopeFactory.CreateScope();
            using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

            var job = new JobInfo()
            {
                UserId = userId,
                Name = name,
                TrackWhenScheduled = trackWhenScheduled,
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

        public void OnJobProgress(long jobId, float progress)
        {
            using var scope = scopeFactory.CreateScope();
            using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

            var job = dataContext.Jobs.Find(jobId);
            job.Progress = progress;

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

        public void OnJobFailed(JobInfo job, string reason, string details = null, bool notifyUser = false)
        {
            using var scope = scopeFactory.CreateScope();
            using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

            job.State = JobState.Failed;
            job.Completed = DateTimeOffset.UtcNow;
            dataContext.SaveChanges();

            userLogger.LogInfo("Job failed", reason, userId: job.UserId, jobId: job.Id);
            JobFailed?.Invoke(this, new JobFailedEventArgs() { Job = job, Reason = reason, Details = details });
        }
    }
}
