using Regard.Common.API.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Subscription
{
    public class SubscriptionViewModel : ISubscriptionItemViewModel
    {
        public ApiSubscription Subscription { get; private set; }

        public string Name => Subscription.Name;

        public int? ParentId => Subscription.ParentFolderId;

        public SubscriptionViewModel(ApiSubscription subscription)
        {
            Subscription = subscription;
        }
    }
}
