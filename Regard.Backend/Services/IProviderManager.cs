using Regard.Backend.Common.Providers;
using Regard.Backend.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    public interface IProviderManager
    {
        /// <summary>
        /// Initializes provider manager
        /// </summary>
        /// <returns></returns>
        Task Initialize();

        /// <summary>
        /// Gets a provider by provider ID which implements given interface
        /// </summary>
        /// <typeparam name="T">Interface</typeparam>
        /// <param name="providerId">Provider ID</param>
        /// <returns></returns>
        T Get<T>(string providerId) where T : class, IProvider;

        IAsyncEnumerable<IVideoProvider> FindForVideo(Video uri);

        IAsyncEnumerable<ISubscriptionProvider> FindFromSubscriptionUrl(Uri uri);
    }
}