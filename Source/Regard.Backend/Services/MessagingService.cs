using Microsoft.AspNetCore.SignalR;
using Regard.Backend.Hubs;
using Regard.Backend.Model;
using Regard.Common;
using Regard.Common.API.Model;
using System;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    public class MessagingService : IDisposable
    {
        private readonly IHubContext<MessagingHub, IMessagingClient> messagingHub;
        private readonly SubscriptionManager subscriptionManager;
        private readonly VideoManager videoManager;
        private readonly JobTrackerService jobTracker;
        private readonly ApiModelFactory apiModelFactory;

        public MessagingService(IHubContext<MessagingHub, IMessagingClient> messagingHub,
                                SubscriptionManager subscriptionManager,
                                VideoManager videoManager,
                                JobTrackerService jobTracker,
                                ApiModelFactory apiModelFactory)
        {
            this.messagingHub = messagingHub;
            this.subscriptionManager = subscriptionManager;
            this.videoManager = videoManager;
            this.jobTracker = jobTracker;
            this.apiModelFactory = apiModelFactory;

            this.subscriptionManager.SubscriptionCreated += OnSubscriptionCreated;
            this.subscriptionManager.SubscriptionUpdated += OnSubscriptionUpdated;
            this.subscriptionManager.SubscriptionsDeleted += OnSubscriptionsDeleted;
            this.subscriptionManager.FolderCreated += OnFolderCreated;
            this.subscriptionManager.FolderUpdated += OnFolderUpdated;
            this.subscriptionManager.FoldersDeleted += OnFoldersDeleted;
            this.videoManager.VideoUpdated += OnVideoUpdated;
            this.jobTracker.JobScheduled += OnJobScheduled;
            this.jobTracker.JobStarted += OnJobStarted;
            this.jobTracker.JobProgress += OnJobProgress;
            this.jobTracker.JobCompleted += OnJobCompleted;
            this.jobTracker.JobFailed += OnJobFailed;
        }

        public void Dispose()
        {
            this.subscriptionManager.SubscriptionCreated -= OnSubscriptionCreated;
            this.subscriptionManager.SubscriptionUpdated -= OnSubscriptionUpdated;
            this.subscriptionManager.SubscriptionsDeleted -= OnSubscriptionsDeleted;
            this.subscriptionManager.FolderCreated -= OnFolderCreated;
            this.subscriptionManager.FolderUpdated -= OnFolderUpdated;
            this.subscriptionManager.FoldersDeleted -= OnFoldersDeleted;
            this.videoManager.VideoUpdated -= OnVideoUpdated;
            this.jobTracker.JobScheduled -= OnJobScheduled;
            this.jobTracker.JobStarted -= OnJobStarted;
            this.jobTracker.JobProgress -= OnJobProgress;
            this.jobTracker.JobCompleted -= OnJobCompleted;
            this.jobTracker.JobFailed -= OnJobFailed;
        }

        private IMessagingClient ForUser(UserAccount userAccount)
        {
            return messagingHub.Clients.User(userAccount.Id);
        }

        private async void OnSubscriptionCreated(object sender, SubscriptionCreatedEventArgs e)
        {
            var subscription = apiModelFactory.ToApi(e.Subscription);
            await ForUser(e.User).NotifySubscriptionCreated(subscription);
        }

        private async void OnSubscriptionUpdated(object sender, SubscriptionUpdatedEventArgs e)
        {
            var subscription = apiModelFactory.ToApi(e.Subscription);
            await ForUser(e.User).NotifySubscriptionUpdated(subscription);
        }

        private async void OnSubscriptionsDeleted(object sender, SubscriptionsDeletedEventArgs e)
        {
            await ForUser(e.User).NotifySubscriptionsDeleted(e.SubscriptionIds);
        }

        private async void OnFolderCreated(object sender, SubscriptionFolderCreatedEventArgs e)
        {
            var folder = apiModelFactory.ToApi(e.Folder);
            await ForUser(e.User).NotifySubscriptionFolderCreated(folder);
        }

        private async void OnFolderUpdated(object sender, SubscriptionFolderUpdatedEventArgs e)
        {
            var folder = apiModelFactory.ToApi(e.Folder);
            await ForUser(e.User).NotifySubscriptionFolderUpdated(folder);
        }

        private async void OnFoldersDeleted(object sender, SubscriptionFoldersDeletedEventArgs e)
        {
            await ForUser(e.User).NotifySubscriptionFoldersDeleted(e.FolderIds);
        }

        private async void OnVideoUpdated(object sender, VideoUpdatedEventArgs e)
        {
            var video = apiModelFactory.ToApi(e.Video);
            await ForUser(e.User).NotifyVideoUpdated(video);
        }

        private void OnJobScheduled(object sender, JobScheduledEventArgs e)
        {
            //throw new NotImplementedException();
        }

        private void OnJobStarted(object sender, JobStartedEventArgs e)
        {
            //throw new NotImplementedException();
        }

        private void OnJobProgress(object sender, JobProgressEventArgs e)
        {
            //throw new NotImplementedException();
        }

        private void OnJobCompleted(object sender, JobCompletedEventArgs e)
        {
            //throw new NotImplementedException();
        }

        private void OnJobFailed(object sender, JobFailedEventArgs e)
        {
            //throw new NotImplementedException();
        }
    }
}
