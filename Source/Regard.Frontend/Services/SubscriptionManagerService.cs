using Microsoft.Extensions.DependencyInjection;
using Regard.Common.API.Model;
using Regard.Common.API.Subscriptions;
using Regard.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Services
{
    public class SubscriptionManagerService : IDisposable
    {
        protected readonly AppState appState;
        protected readonly MessagingService messaging;
        protected readonly IServiceProvider serviceProvider;

        private bool loaded = false;

        public SubscriptionManagerService(AppState appState,
                                          MessagingService messaging,
                                          IServiceProvider serviceProvider)
        {
            this.appState = appState;
            this.messaging = messaging;
            this.serviceProvider = serviceProvider;

            appState.RefreshRequested += AppState_RefreshRequested;
            messaging.SubscriptionCreated += Messaging_SubscriptionCreated;
            messaging.SubscriptionUpdated += Messaging_SubscriptionUpdated;
            messaging.SubscriptionsDeleted += Messaging_SubscriptionsDeleted;
            messaging.SubscriptionFolderCreated += Messaging_SubscriptionFolderCreated;
            messaging.SubscriptionFolderUpdated += Messaging_SubscriptionFolderUpdated;
            messaging.SubscriptionFoldersDeleted += Messaging_SubscriptionFoldersDeleted;
        }

        public void Dispose()
        {
            appState.RefreshRequested -= AppState_RefreshRequested;
            messaging.SubscriptionCreated -= Messaging_SubscriptionCreated;
            messaging.SubscriptionUpdated -= Messaging_SubscriptionUpdated;
            messaging.SubscriptionsDeleted -= Messaging_SubscriptionsDeleted;
            messaging.SubscriptionFolderCreated -= Messaging_SubscriptionFolderCreated;
            messaging.SubscriptionFolderUpdated -= Messaging_SubscriptionFolderUpdated;
            messaging.SubscriptionFoldersDeleted -= Messaging_SubscriptionFoldersDeleted;
        }

        public async Task Load(bool force = false)
        {
            if (loaded && !force)
                return;

            try
            {
                using var scope = serviceProvider.CreateScope();
                var backend = scope.ServiceProvider.GetRequiredService<BackendService>();

                var (folders, httpResp) = await backend.SubscriptionFolderList(new SubscriptionFolderListRequest());
                if (!httpResp.IsSuccessStatusCode || folders?.Data?.Folders == null)
                    return;   // transient failure -> leave loaded=false so a later refresh retries

                appState.Folders.Clear();
                appState.Folders.AddRange(folders.Data.Folders.Select(x => KeyValuePair.Create(x.Id, x)));

                var (subs, httpRespSubs) = await backend.SubscriptionList(new SubscriptionListRequest());
                if (!httpRespSubs.IsSuccessStatusCode || subs?.Data?.Subscriptions == null)
                    return;

                appState.Subscriptions.Clear();
                appState.Subscriptions.AddRange(subs.Data.Subscriptions.Select(x => KeyValuePair.Create(x.Id, x)));

                loaded = true;
            }
            catch (Exception ex)
            {
                // Non-fatal: a transient load failure must not crash the UI. Retry on next refresh.
                Console.Error.WriteLine("Failed to load subscriptions: " + ex.Message);
            }
        }

        private async void AppState_RefreshRequested(object sender, EventArgs e)
        {
            await Load(force: true);
        }

        public async Task SynchronizeAll()
        {
            using var scope = serviceProvider.CreateScope();
            var backend = scope.ServiceProvider.GetRequiredService<BackendService>();

            await backend.SubscriptionSynchronizeAll();
        }

        public async Task Synchronize(ApiSubscription subscription)
        {
            using var scope = serviceProvider.CreateScope();
            var backend = scope.ServiceProvider.GetRequiredService<BackendService>();

            await backend.SubscriptionSynchronize(new SubscriptionSynchronizeRequest() { Id = subscription.Id });
        }

        public async Task Synchronize(ApiSubscriptionFolder folder)
        {
            using var scope = serviceProvider.CreateScope();
            var backend = scope.ServiceProvider.GetRequiredService<BackendService>();

            await backend.SubscriptionFolderSynchronize(new SubscriptionFolderSynchronizeRequest() { Id = folder.Id });
        }

        /// <summary>Reparents a subscription (parent-only, preserves settings). Returns false on failure.</summary>
        public async Task<bool> Move(ApiSubscription subscription, int? parentFolderId)
        {
            using var scope = serviceProvider.CreateScope();
            var backend = scope.ServiceProvider.GetRequiredService<BackendService>();

            var (_, http) = await backend.SubscriptionMove(new SubscriptionMoveRequest()
            {
                Id = subscription.Id,
                ParentFolderId = parentFolderId,
            });
            // ParentFolderId is part of the change feed's allowlist, so the move arrives as a
            // NotifySubscriptionUpdated and the tree reparents the node in place. No reload needed.
            return http.IsSuccessStatusCode;
        }

        /// <summary>Reparents a folder (parent-only, preserves settings). Returns false on failure.</summary>
        public async Task<bool> Move(ApiSubscriptionFolder folder, int? parentFolderId)
        {
            using var scope = serviceProvider.CreateScope();
            var backend = scope.ServiceProvider.GetRequiredService<BackendService>();

            var (_, http) = await backend.SubscriptionFolderMove(new SubscriptionFolderMoveRequest()
            {
                Id = folder.Id,
                ParentFolderId = parentFolderId,
            });
            // Same as above: the folder's ParentId change is pushed, so the tree updates itself.
            return http.IsSuccessStatusCode;
        }

        public async Task Delete(ApiSubscription subscription, bool deleteDownloadedFiles)
        {
            using var scope = serviceProvider.CreateScope();
            var backend = scope.ServiceProvider.GetRequiredService<BackendService>();

            await backend.SubscriptionDelete(new SubscriptionDeleteRequest()
            {
                Ids = new[] { subscription.Id },
                DeleteDownloadedFiles = deleteDownloadedFiles
            });
        }

        public async Task Delete(ApiSubscriptionFolder folder, bool recursive, bool deleteDownloadedFiles)
        {
            using var scope = serviceProvider.CreateScope();
            var backend = scope.ServiceProvider.GetRequiredService<BackendService>();

            await backend.SubscriptionFolderDelete(new SubscriptionFolderDeleteRequest()
            {
                Ids = new[] { folder.Id },
                Recursive = recursive,
                DeleteDownloadedFiles = deleteDownloadedFiles
            });
        }

        private void Messaging_SubscriptionCreated(object sender, ApiSubscription subscription)
        {
            appState.Subscriptions[subscription.Id] = subscription;
        }

        private void Messaging_SubscriptionUpdated(object sender, ApiSubscription subscription)
        {
            appState.Subscriptions[subscription.Id] = subscription;
        }

        private void Messaging_SubscriptionsDeleted(object sender, int[] ids)
        {
            appState.Subscriptions.BeginBatch();
            foreach (var id in ids)
                appState.Subscriptions.Remove(id);
            appState.Subscriptions.EndBatch();
        }

        private void Messaging_SubscriptionFolderCreated(object sender, ApiSubscriptionFolder folder)
        {
            appState.Folders[folder.Id] = folder;
        }

        private void Messaging_SubscriptionFolderUpdated(object sender, ApiSubscriptionFolder folder)
        {
            appState.Folders[folder.Id] = folder;
        }

        private void Messaging_SubscriptionFoldersDeleted(object sender, int[] ids)
        {
            appState.Folders.BeginBatch();
            foreach (var id in ids)
                appState.Folders.Remove(id);
            appState.Folders.EndBatch();
        }
    }
}
