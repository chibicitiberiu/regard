using Regard.Common.API.Model;
using System;

namespace Regard.Frontend.Utils
{
    /// <summary>
    /// Makes the relative media URLs the backend returns (e.g. "thumbs/s1/thumb.png?v=…") absolute
    /// against the backend origin. In dev the frontend is served from :5000 and the backend from :9585,
    /// so a relative thumbnail resolves against the wrong host and 404s.
    ///
    /// This used to be copy-pasted into four components, and every new consumer of a pushed DTO had to
    /// remember it (one that didn't shipped broken icons). Applied centrally instead: once where DTOs
    /// enter from HTTP (BackendService) and once where they arrive over SignalR (MessagingService).
    /// Idempotent, so applying it twice is harmless.
    /// </summary>
    public static class ApiUrlUtils
    {
        public static ApiVideo Absolutize(this ApiVideo video, Uri backendBase)
        {
            if (video?.ThumbnailUrl != null && !video.ThumbnailUrl.IsAbsoluteUri && backendBase != null)
                video.ThumbnailUrl = new Uri(backendBase, video.ThumbnailUrl);
            return video;
        }

        public static ApiSubscription Absolutize(this ApiSubscription subscription, Uri backendBase)
        {
            if (subscription?.ThumbnailUrl != null && !subscription.ThumbnailUrl.IsAbsoluteUri && backendBase != null)
                subscription.ThumbnailUrl = new Uri(backendBase, subscription.ThumbnailUrl);
            return subscription;
        }
    }
}
