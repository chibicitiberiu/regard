using Regard.Backend.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Providers
{
    public interface ICompleteProvider
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

        Task<bool> CanHandleSubscriptionUrl(Uri uri);

        Task<bool> CanHandleVideoUrl(Uri uri);

        Task<Subscription> CreateSubscription(Uri uri);

        IAsyncEnumerable<Video> FetchVideos(Subscription subscription);

        IAsyncEnumerable<Video> FetchVideos(IEnumerable<Uri> videos);

        Task UpdateMetadata(IEnumerable<Video> videos, bool updateMetadata, bool updateStatistics);
    }
}
