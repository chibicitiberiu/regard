using MoreLinq;
using Regard.Backend.Common.Utils;
using Regard.Backend.Model;
using Regard.Backend.Providers;
using Regard.Common.API.Model;
using Regard.Backend.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Regard.Backend.Services
{
    public class SubscriptionManager 
    {
        private readonly DataContext dataContext;
        private readonly Dictionary<string, ICompleteProvider> providers = new Dictionary<string, ICompleteProvider>();
        private readonly Dictionary<string, ISubscriptionProvider> subscriptionProviders = new Dictionary<string, ISubscriptionProvider>();
        private readonly Dictionary<string, IVideoProvider> videoProviders = new Dictionary<string, IVideoProvider>();
        private readonly MessagingService messaging;

        public SubscriptionManager(DataContext dataContext,
            IEnumerable<ICompleteProvider> completeProviders,
            IEnumerable<ISubscriptionProvider> subscriptionProviders,
            IEnumerable<IVideoProvider> videoProviders,
            MessagingService messaging)
        {
            this.dataContext = dataContext;
            completeProviders.ForEach(x => this.providers.Add(x.ProviderId, x));
            subscriptionProviders.ForEach(x => this.subscriptionProviders.Add(x.ProviderId, x));
            videoProviders.ForEach(x => this.videoProviders.Add(x.ProviderId, x));
            this.messaging = messaging;
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

            // Find subscription provider and create subscription
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
            await messaging.NotifySubscriptionCreated(userAccount, sub.ToApi());

            return sub;
        }

        public async Task CreateFolder(UserAccount user, string name, ParentId parentId)
        {
            // Verify if any folder exists
            bool alreadyExists = dataContext.SubscriptionFolders.AsQueryable()
                .Where(x => x.UserId == user.Id)
                .Where(x => x.ParentId == parentId)
                .Where(x => x.Name.ToUpper() == name.ToUpper())
                .Any();

            if (!alreadyExists)
            {
                var newFolder = new SubscriptionFolder()
                {
                    User = user,
                    ParentId = parentId,
                    Name = name
                };
                dataContext.SubscriptionFolders.Add(newFolder);
                await dataContext.SaveChangesAsync();
                await messaging.NotifySubscriptionFolderCreated(user, newFolder.ToApi());
            }
        }

        public IQueryable<Subscription> GetAll(UserAccount userAccount)
        {
            return dataContext.Subscriptions.AsQueryable()
                .Where(x => x.UserId == userAccount.Id);
        }

        public async Task DeleteSubscriptions(UserAccount userAccount, int[] ids)
        {
            var itemsToDelete = dataContext.Subscriptions.AsQueryable()
                .Where(x => x.UserId == userAccount.Id)
                .Where(x => ids.Contains(x.Id));

            // TODO: delete videos, also delete videos from disk
            foreach (var item in itemsToDelete)
                await messaging.NotifySubscriptionDeleted(userAccount, item.ToApi());

            dataContext.Subscriptions.RemoveRange(itemsToDelete);
        }

        public IQueryable<SubscriptionFolder> GetAllFolders(UserAccount userAccount)
        {
            return dataContext.SubscriptionFolders.AsQueryable()
                .Where(x => x.UserId == userAccount.Id);
        }
    }
}
