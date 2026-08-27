using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Regard.Backend.Common.Model;
using Regard.Backend.Hubs;
using Regard.Backend.Logging;
using Regard.Common;
using Regard.Common.API.Model;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    /// <summary>
    /// Bridges the (singleton) job + user-message event sources to SignalR clients. Subscribes to
    /// JobTrackerService job events and UserLogger.MessageCreated, maps to API DTOs, and pushes to
    /// the owning user (or all clients for ownerless system jobs). Only "important" jobs (Notify)
    /// reach the live bell; every job is still persisted for the Job Log, and every message is sent.
    /// </summary>
    public class JobNotificationBroadcaster : IHostedService
    {
        private readonly IHubContext<MessagingHub, IMessagingClient> hub;
        private readonly JobTrackerService jobTracker;
        private readonly UserLogger userLogger;
        private readonly ILogger<JobNotificationBroadcaster> log;

        public JobNotificationBroadcaster(IHubContext<MessagingHub, IMessagingClient> hub,
                                          JobTrackerService jobTracker,
                                          UserLogger userLogger,
                                          ILogger<JobNotificationBroadcaster> log)
        {
            this.hub = hub;
            this.jobTracker = jobTracker;
            this.userLogger = userLogger;
            this.log = log;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            jobTracker.JobScheduled += OnJobScheduled;
            jobTracker.JobStarted += OnJobStarted;
            jobTracker.JobProgress += OnJobProgress;
            jobTracker.JobCompleted += OnJobCompleted;
            jobTracker.JobFailed += OnJobFailed;
            userLogger.MessageCreated += OnMessageCreated;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            jobTracker.JobScheduled -= OnJobScheduled;
            jobTracker.JobStarted -= OnJobStarted;
            jobTracker.JobProgress -= OnJobProgress;
            jobTracker.JobCompleted -= OnJobCompleted;
            jobTracker.JobFailed -= OnJobFailed;
            userLogger.MessageCreated -= OnMessageCreated;
            return Task.CompletedTask;
        }

        private void OnJobScheduled(object sender, JobScheduledEventArgs e) => PushJob(e.Job);
        private void OnJobStarted(object sender, JobStartedEventArgs e) => PushJob(e.Job);
        private void OnJobProgress(object sender, JobProgressEventArgs e) => PushJob(e.Job);
        private void OnJobCompleted(object sender, JobCompletedEventArgs e) => PushJob(e.Job);
        private void OnJobFailed(object sender, JobFailedEventArgs e) => PushJob(e.Job);

        private void PushJob(JobInfo job)
        {
            // Only important jobs go to the bell. Map to a DTO synchronously (scalars only — never
            // touch the lazy User nav): the event may fire inside a DB scope that disposes right after.
            if (job == null || !job.Notify)
                return;

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
            };
            string userId = job.UserId;

            _ = SendAsync(userId, client => client.NotifyJobUpdated(dto), "job update");
        }

        private void OnMessageCreated(object sender, Message message)
        {
            if (message == null)
                return;

            var dto = new ApiMessage
            {
                Id = message.Id,
                Timestamp = message.Timestamp,
                Severity = (ApiMessageSeverity)(int)message.Severity,
                Message = message.Content,
                Details = message.Details,
                JobId = message.JobId,
            };
            string userId = message.UserId;

            _ = SendAsync(userId, client => client.NotifyMessage(dto), "message");
        }

        private async Task SendAsync(string userId, Func<IMessagingClient, Task> send, string what)
        {
            try
            {
                var target = userId != null ? hub.Clients.User(userId) : hub.Clients.All;
                await send(target);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to push {0} over SignalR", what);
            }
        }
    }
}
