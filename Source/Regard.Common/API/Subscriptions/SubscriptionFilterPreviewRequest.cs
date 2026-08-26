using Regard.Common.API.Model;
using System.Collections.Generic;

namespace Regard.Common.API.Subscriptions
{
    public class SubscriptionFilterPreviewRequest
    {
        public int SubscriptionId { get; set; }

        public List<ApiSubscriptionFilter> Filters { get; set; } = new();
    }
}
