using Regard.Backend.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Providers
{
    public interface ISubscriptionProvider
    {
        /// <summary>
        /// Provider ID
        /// </summary>
        string ProviderId { get; }

        /// <summary>
        /// User friendly name
        /// </summary>
        string Name { get; }

        bool IsInitialized { get; }

        Type ConfigurationType { get; }

        void Configure(object config);

        void Unconfigure();

        Task<bool> CanHandleUrl(Uri uri);

        IAsyncEnumerable<Uri> FetchVideos(Subscription subscription);

        Task<Subscription> CreateSubscription(Uri uri);
    }
}
