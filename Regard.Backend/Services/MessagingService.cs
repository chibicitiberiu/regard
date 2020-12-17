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

        public async Task NotifySubscriptionDeleted(UserAccount userAccount, ApiSubscription subscription)
        {
            await ForUser(userAccount).NotifySubscriptionDeleted(subscription);
        }

        public async Task NotifySubscriptionFolderCreated(UserAccount userAccount, ApiSubscriptionFolder newFolder)
        {
            await ForUser(userAccount).NotifySubscriptionFolderCreated(newFolder);
        }
    }
}
