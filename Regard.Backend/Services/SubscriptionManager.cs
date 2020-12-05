using MoreLinq;
using Regard.Backend.Model;
using Regard.Backend.Providers;
using RegardBackend.DB;
using RegardBackend.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    public class SubscriptionManager 
    {
        private readonly DataContext dataContext;
        private readonly Dictionary<string, ICompleteProvider> providers = new Dictionary<string, ICompleteProvider>();
        private readonly Dictionary<string, ISubscriptionProvider> subscriptionProviders = new Dictionary<string, ISubscriptionProvider>();
        private readonly Dictionary<string, IVideoProvider> videoProviders = new Dictionary<string, IVideoProvider>();

        public SubscriptionManager(DataContext dataContext,
            IEnumerable<ICompleteProvider> completeProviders,
            IEnumerable<ISubscriptionProvider> subscriptionProviders,
            IEnumerable<IVideoProvider> videoProviders)
        {
            this.dataContext = dataContext;
            completeProviders.ForEach(x => this.providers.Add(x.ProviderId, x));
            subscriptionProviders.ForEach(x => this.subscriptionProviders.Add(x.ProviderId, x));
            videoProviders.ForEach(x => this.videoProviders.Add(x.ProviderId, x));
        }

        public async Task<string> TestUrl(Uri uri)
        {
            foreach (var provider in providers.Values)
            {
                if (await provider.CanHandleSubscriptionUrl(uri))
                    return provider.ProviderId;
            }
            foreach (var provider in subscriptionProviders.Values)
            {
                if (await provider.CanHandleUrl(uri))
                    return provider.ProviderId;
            }

            throw new ArgumentException("Unsupported service or URL format!");
        }

        public async Task<Subscription> Create(Uri uri, UserAccount userAccount, int? parentFolderId)
        {
            // Verify parent folder ID exists
            SubscriptionFolder parent = null;
            if (parentFolderId.HasValue)
            {
                parent = dataContext.SubscriptionFolders.Find(parentFolderId.Value);
                if (parent == null)
                    throw new Exception("Parent folder not found!");
            }

            // Create subscription
            Subscription sub = await providers.Values
                .ToAsyncEnumerable()
                .WhereAwait(async x => await x.CanHandleSubscriptionUrl(uri))
                .SelectAwait(async x => await x.CreateSubscription(uri))
                .FirstOrDefaultAsync();
            
            if (sub == null)
            {
                sub = await subscriptionProviders.Values
                    .ToAsyncEnumerable()
                    .WhereAwait(async x => await x.CanHandleUrl(uri))
                    .SelectAwait(async x => await x.CreateSubscription(uri))
                    .FirstOrDefaultAsync();
            }
            
            sub.User = userAccount;
            sub.ParentFolder = parent;
            dataContext.Subscriptions.Add(sub);
            await dataContext.SaveChangesAsync();

            return sub;
        }

        public IQueryable<Subscription> GetAll(UserAccount userAccount, int? parentFolderId)
        {
            return dataContext.Subscriptions.AsQueryable()
                .Where(x => x.UserId == userAccount.Id && x.ParentFolderId == parentFolderId);
        }
    }
}
