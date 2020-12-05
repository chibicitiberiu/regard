using MoreLinq;
using Regard.Backend.Providers;
using RegardBackend.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    public class ProviderManager
    {
        private readonly DataContext dataContext;
        private readonly Dictionary<string, ICompleteProvider> providers = new Dictionary<string, ICompleteProvider>();
        private readonly Dictionary<string, ISubscriptionProvider> subscriptionProviders = new Dictionary<string, ISubscriptionProvider>();
        private readonly Dictionary<string, IVideoProvider> videoProviders = new Dictionary<string, IVideoProvider>();

        public ProviderManager(/*DataContext dataContext, */
            IEnumerable<ICompleteProvider> completeProviders, 
            IEnumerable<ISubscriptionProvider> subscriptionProviders, 
            IEnumerable<IVideoProvider> videoProviders)
        {
            //this.dataContext = dataContext;
            completeProviders.ForEach(x => this.providers.Add(x.ProviderId, x));
            subscriptionProviders.ForEach(x => this.subscriptionProviders.Add(x.ProviderId, x));
            videoProviders.ForEach(x => this.videoProviders.Add(x.ProviderId, x));
        }
    }
}
