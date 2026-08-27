using Regard.Backend.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Common.Providers
{
    public interface ISubscriptionProvider : IProvider
    {
        Task<bool> CanHandleSubscriptionUrl(Uri uri);

        /// <summary>
        /// Cheap, synchronous fast-path hint. Returns true when this provider is an obvious
        /// handler for the URL (e.g. yt-dlp for youtube.com), so the dispatcher probes it
        /// before generic providers like RSS that would otherwise fetch-and-fail to rule
        /// themselves out. This only affects probe ordering; <see cref="CanHandleSubscriptionUrl"/>
        /// is still the authority. Default: no opinion.
        /// </summary>
        bool CanHandleSubscriptionUrlHint(Uri uri) => false;

        IAsyncEnumerable<Video> FetchVideos(Subscription subscription);

        Task<Subscription> CreateSubscription(Uri uri);
    }
}
