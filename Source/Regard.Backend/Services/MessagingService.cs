using Microsoft.AspNetCore.SignalR;
using Regard.Backend.Hubs;
using Regard.Backend.Model;
using Regard.Common;
using Regard.Common.API.Model;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    public class MessagingService
    {
        private readonly IHubContext<MessagingHub, IMessagingClient> messagingHub;

        public MessagingService(IHubContext<MessagingHub, IMessagingClient> messagingHub)
        {
            this.messagingHub = messagingHub;
        }

        private IMessagingClient ForUser(UserAccount userAccount)
        {
            return messagingHub.Clients.User(userAccount.Id);
        }

        public async Task NotifySubscriptionCreated(UserAccount userAccount, ApiSubscription subscription)
        {
            await ForUser(userAccount).NotifySubscriptionCreated(subscription);
        }
        public async Task NotifySubscriptionUpdated(UserAccount userAccount, ApiSubscription subscription)
        {
            await ForUser(userAccount).NotifySubscriptionUpdated(subscription);
        }

        public async Task NotifySubscriptionsDeleted(UserAccount userAccount, int[] subscriptionIds)
        {
            await ForUser(userAccount).NotifySubscriptionsDeleted(subscriptionIds);
        }

        public async Task NotifySubscriptionFolderCreated(UserAccount userAccount, ApiSubscriptionFolder newFolder)
        {
            await ForUser(userAccount).NotifySubscriptionFolderCreated(newFolder);
        }

        public async Task NotifySubscriptionFolderUpdated(UserAccount userAccount, ApiSubscriptionFolder folder)
        {
            await ForUser(userAccount).NotifySubscriptionFolderUpdated(folder);
        }

        public async Task NotifySubscriptionsFoldersDeleted(UserAccount userAccount, int[] folderIds)
        {
            await ForUser(userAccount).NotifySubscriptionFoldersDeleted(folderIds);
        }

        public async Task NotifyVideoUpdated(UserAccount userAccount, ApiVideo video)
        {
            await ForUser(userAccount).NotifyVideoUpdated(video);
        }
    }
}
