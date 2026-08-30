using Regard.Common.API.Model;
using System.Collections.Generic;

namespace Regard.Common.API.Subscriptions
{
    public class SubscriptionFilterPreviewRequest
    {
        public int SubscriptionId { get; set; }

        public List<ApiSubscriptionFilter> Filters { get; set; } = new();

        /// <summary>
        /// The unsaved publish-date window from the edit form, "yyyy-MM-dd" or empty. Sent so the preview
        /// reflects what the user is about to save rather than what is currently stored.
        /// </summary>
        public string PublishedAfter { get; set; }

        public string PublishedBefore { get; set; }
    }
}
