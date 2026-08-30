using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Regard.Backend.Common.Model;
using Regard.Backend.Hubs;
using Regard.Backend.Model;
using Regard.Backend.Services;
using Regard.Common;
using Regard.Common.API.Model;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Regard.Backend.Services.LiveUpdates
{
    /// <summary>
    /// Pushes job state to the owning user's clients so the Job Log updates without polling.
    ///
    /// This can't ride on the change feed: JobInfo.Progress and Detail are [NotMapped] (live values sit in
    /// JobTrackerService), so progress never touches the database at all. It's an event bridge instead —
    /// which is what the old, never-instantiated MessagingService was meant to be, done as a singleton so
    /// it can actually observe background jobs.
    /// </summary>
    public class JobPushBridge : IHostedService
    {
        // Progress fires per percent of every download; without a cap this becomes a firehose.
        private static readonly TimeSpan ProgressThrottle = TimeSpan.FromSeconds(1);

        private readonly JobTrackerService jobTracker;
        private readonly IHubContext<MessagingHub, IMessagingClient> hub;
        private readonly DownloadCancellationRegistry cancellationRegistry;
        private readonly ILogger<JobPushBridge> log;

        private readonly ConcurrentDictionary<long, DateTime> lastProgressSent = new ConcurrentDictionary<long, DateTime>();

        public JobPushBridge(JobTrackerService jobTracker,
                             IHubContext<MessagingHub, IMessagingClient> hub,
                             DownloadCancellationRegistry cancellationRegistry,
                             ILogger<JobPushBridge> log)
        {
            this.jobTracker = jobTracker;
            this.hub = hub;
            this.cancellationRegistry = cancellationRegistry;
            this.log = log;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            jobTracker.JobCreated += OnJobCreated;
            jobTracker.JobScheduled += OnJobScheduled;
            jobTracker.JobStarted += OnJobStarted;
            jobTracker.JobProgress += OnJobProgress;
            jobTracker.JobCompleted += OnJobCompleted;
            jobTracker.JobFailed += OnJobFailed;
            jobTracker.JobCancelled += OnJobCancelled;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            jobTracker.JobCreated -= OnJobCreated;
            jobTracker.JobScheduled -= OnJobScheduled;
            jobTracker.JobStarted -= OnJobStarted;
            jobTracker.JobProgress -= OnJobProgress;
            jobTracker.JobCompleted -= OnJobCompleted;
            jobTracker.JobFailed -= OnJobFailed;
            jobTracker.JobCancelled -= OnJobCancelled;
            return Task.CompletedTask;
        }

        private void OnJobCreated(object sender, JobCreatedEventArgs e) => Push(e.Job);
        private void OnJobScheduled(object sender, JobScheduledEventArgs e) => Push(e.Job);
        private void OnJobStarted(object sender, JobStartedEventArgs e) => Push(e.Job);
        private void OnJobCompleted(object sender, JobCompletedEventArgs e) => Push(e.Job, terminal: true);
        private void OnJobFailed(object sender, JobFailedEventArgs e) => Push(e.Job, terminal: true);
        private void OnJobCancelled(object sender, JobCancelledEventArgs e) => Push(e.Job, terminal: true);

        private void OnJobProgress(object sender, JobProgressEventArgs e)
        {
            var now = DateTime.UtcNow;
            var last = lastProgressSent.GetOrAdd(e.Job.Id, DateTime.MinValue);
            if (now - last < ProgressThrottle)
                return;
            lastProgressSent[e.Job.Id] = now;
            Push(e.Job);
        }

        private void Push(JobInfo job, bool terminal = false)
        {
            if (job == null)
                return;

            if (terminal)
                lastProgressSent.TryRemove(job.Id, out _);

            try
            {
                // Ownerless ("system") jobs go to everyone, which is exactly what JobsController.VisibleJobs
                // already allows: a non-admin sees `j.UserId == user.Id || j.UserId == null`. So this
                // mirrors the existing authorization rather than widening it. In practice almost every job
                // is ownerless today, because RegardScheduler's userId argument is rarely supplied.
                var target = string.IsNullOrEmpty(job.UserId)
                    ? hub.Clients.All
                    : hub.Clients.User(job.UserId);

                _ = target.NotifyJobUpdated(ToApi(job));
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Failed to push job {0}", job.Id);
            }
        }

        private ApiJobInfo ToApi(JobInfo job)
        {
            var dto = new ApiJobInfo
            {
                Id = job.Id,
                Name = job.Name,
                State = (ApiJobState)(int)job.State,
                Detail = job.Detail,
                Progress = job.Progress,
                Created = job.Created,
                Started = job.Started,
                Completed = job.Completed,
                NextRun = job.NextRun,
            };

            // Progress/Detail are [NotMapped]; a running job's real values live in the tracker.
            if (job.State == JobState.Running && jobTracker.GetLive(job.Id) is JobTrackerService.JobLiveState live)
            {
                dto.Progress = live.Progress;
                dto.Detail = live.Detail;
                dto.Cancellable = cancellationRegistry.IsCancellable(job.Id);
            }

            return dto;
        }
    }
}
