using Regard.Common.API.Model;
using Regard.Frontend.Shared.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Subscription
{
    public class SubscriptionViewModel : SubscriptionItemViewModelBase
    {
        public ApiSubscription Subscription { get; private set; }

        public override string Name => Subscription.Name;

        public override int? ParentId => Subscription.ParentFolderId;

        public override string ThumbnailUrl => Subscription.ThumbnailUrl;

        public override Icons PlaceholderIcon => Icons.Subscription;

        public override string SortKey => "1" + Name;

        public SubscriptionViewModel(ApiSubscription subscription)
        {
            Subscription = subscription;
        }
    }
}
