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

        IAsyncEnumerable<Video> FetchVideos(Subscription subscription);

        Task<Subscription> CreateSubscription(Uri uri);
    }
}
