using Regard.Backend.DB;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Regard.Backend.Common.Providers;
using System.Xml.Serialization;
using System.IO;
using Microsoft.Extensions.Logging;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Regard.Backend.Model;
using System.Text.Json;

namespace Regard.Backend.Services
{
    public class ProviderManager : IProviderManager
    {
        protected readonly ILogger log;
        protected readonly IServiceScopeFactory scopeFactory;

        protected readonly List<IProvider> pendingProviders = new List<IProvider>();
        protected readonly Dictionary<string, IProvider> providers = new Dictionary<string, IProvider>();

        public ProviderManager(ILogger<ProviderManager> logger,
                               IServiceScopeFactory scopeFactory,
                               IEnumerable<IProvider> providers)
        {
            this.log = logger;
            this.scopeFactory = scopeFactory;
            pendingProviders.AddRange(providers);
        }

        public T Get<T>(string providerId) where T : class, IProvider
        {
            if (providers.TryGetValue(providerId, out IProvider value))
                return value as T;

            return null;
        }

        public async Task Initialize()
        {
            log.LogInformation("Provider initialization started");

            for (int i = 0; i < pendingProviders.Count; i++)
            {
                var provider = pendingProviders[i];

                try
                {
                    var config = GetConfiguration(provider);
                    await provider.Configure(config);

                    log.LogInformation($"{provider.Name} provider initialized.");

                    // Move to "providers" map
                    providers.Add(provider.Id, provider);
                    pendingProviders.RemoveAt(i--);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, $"{provider.Name} provider initialization failed!");
                    // TODO: notify user
                }
            }

            log.LogInformation("Provider initialization completed!");
        }

        private object GetConfiguration(IProvider provider)
        {
            object config = null;
            using var scope = scopeFactory.CreateScope();
            using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

            var dbConfig = dataContext.ProviderConfigurations.Find(provider.Id);
            if (dbConfig != null && provider.ConfigurationType != null)
                config = JsonSerializer.Deserialize(dbConfig.Configuration, provider.ConfigurationType);

            return config;
        }

        public IAsyncEnumerable<ISubscriptionProvider> FindFromSubscriptionUrl(Uri uri)
        {
            return providers.Values
                .ToAsyncEnumerable()
                .Where(x => x is ISubscriptionProvider)
                .Cast<ISubscriptionProvider>()
                .WhereAwait(async x => await x.CanHandleSubscriptionUrl(uri));
        }

        public IAsyncEnumerable<IVideoProvider> FindForVideo(Video video)
        {
            return providers.Values
                .ToAsyncEnumerable()
                .Where(x => x is IVideoProvider)
                .Cast<IVideoProvider>()
                .WhereAwait(async x => await x.CanHandleVideo(video));
        }
    }
}
