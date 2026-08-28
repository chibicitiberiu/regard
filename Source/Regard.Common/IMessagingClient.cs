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

        Task NotifyVideoUpdated(ApiVideo video);

        /// <summary>
        /// Pushed when a notification is created or updated in place (keyed by ApiNotification.Key).
        /// Drives the whole notification bell — live job progress and terminal outcomes alike.
        /// </summary>
        Task NotifyNotification(ApiNotification notification);

        /// <summary>Pushed when a notification is removed (dismissed, cleared, or a silent job finish).</summary>
        Task NotifyNotificationRemoved(string key);
    }
}
