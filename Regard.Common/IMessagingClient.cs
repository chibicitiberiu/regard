using Regard.Common.API.Model;
using Regard.Common.API.Subscriptions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Regard.Common
{
    public interface IMessagingClient
    {
        Task ShowToast(string toast);

        Task NotifySubscriptionCreated(ApiSubscription subscription);

        Task NotifySubscriptionUpdated(ApiSubscription subscription);

        Task NotifySubscriptionsDeleted(int[] ids);

        Task NotifySubscriptionFolderCreated(ApiSubscriptionFolder folder);

        Task NotifySubscriptionFolderUpdated(ApiSubscriptionFolder folder);

        Task NotifySubscriptionFoldersDeleted(int[] ids);
    }
}
