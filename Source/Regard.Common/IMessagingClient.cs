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
        /// Pushed when an "important" job (download/sync) changes state or progresses. Drives the
        /// live job list in the notification bell.
        /// </summary>
        Task NotifyJobUpdated(ApiJobInfo job);

        /// <summary>
        /// Pushed for a user-facing message (info/warning/error, incl. job failures). Drives the
        /// recent-messages list and toasts.
        /// </summary>
        Task NotifyMessage(ApiMessage message);
    }
}
