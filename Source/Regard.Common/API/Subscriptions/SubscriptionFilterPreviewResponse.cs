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

        /// <summary>
        /// False when the video is excluded by the publish-date window rather than by a title filter.
        /// Kept separate from <see cref="PassesFilters"/> so the preview can say which rule rejected a
        /// video — a single "excluded" colour for two different causes tells the user nothing.
        /// </summary>
        public bool PassesDateWindow { get; set; } = true;

        public bool InWindow { get; set; }
    }
}
