using System.Collections.Generic;

namespace Regard.Common.API.Subscriptions
{
    public class SubscriptionFilterPreviewResponse
    {
        public List<FilterPreviewItem> Videos { get; set; } = new();

        public bool Truncated { get; set; }
    }

    public class FilterPreviewItem
    {
        public string Name { get; set; }

        public bool IsDownloaded { get; set; }

        public bool IsWatched { get; set; }

        public bool PassesFilters { get; set; }

        public bool InWindow { get; set; }
    }
}
