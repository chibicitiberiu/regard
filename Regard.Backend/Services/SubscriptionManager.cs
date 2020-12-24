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
using Regard.Backend.Common.Providers;
using Regard.Model;

namespace Regard.Backend.Services
{
    public class SubscriptionManager 
    {
        private readonly DataContext dataContext;
        private readonly IPreferencesManager preferencesManager;
        private readonly IProviderManager providerManager;
        private readonly MessagingService messaging;

        public SubscriptionManager(DataContext dataContext,
                                   IPreferencesManager preferencesManager,
                                   IProviderManager providerManager,
                                   MessagingService messaging)
        {
            this.dataContext = dataContext;
            this.preferencesManager = preferencesManager;
            this.providerManager = providerManager;
            this.messaging = messaging;
        }

        public async Task<string> TestUrl(Uri uri)
        {
            var provider = await providerManager.FindFromSubscriptionUrl(uri).FirstOrDefaultAsync();

            if (provider == null)
                throw new ArgumentException("Unsupported service or URL format!");
            
            return provider.Id;
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
            var provider = await providerManager.FindFromSubscriptionUrl(uri).FirstOrDefaultAsync();
            if (provider == null)
                throw new Exception("Could not find a subscription provider that can handle this URL!");

            Subscription sub = await provider.CreateSubscription(uri);
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

        private async Task<T> GetOption<T>(Subscription sub,
                                           Func<Subscription, T?> optGetter,
                                           Func<SubscriptionFolder, T?> folderOptGetter,
                                           PreferenceDefinition<T> preference) where T : struct
        {
            // Get from subscription
            T? value = optGetter(sub);
            if (value.HasValue)
                return value.Value;

            // Get from subscription folder
            var folder = sub.ParentFolder;
            while (folder != null)
            {
                value = folderOptGetter(folder);
                if (value.HasValue)
                    return value.Value;
            }

            // Get from preference
            return await preferencesManager.Get(preference);
        }

        private async Task<T> GetOption<T>(Subscription sub,
                                           Func<Subscription, T> optGetter,
                                           Func<SubscriptionFolder, T> folderOptGetter,
                                           PreferenceDefinition<T> preference) where T : class
        {
            // Get from subscription
            T value = optGetter(sub);
            if (value != null)
                return value;

            // Get from subscription folder
            var folder = sub.ParentFolder;
            while (folder != null)
            {
                value = folderOptGetter(folder);
                if (value != null)
                    return value;
            }

            // Get from preference
            return await preferencesManager.Get(preference);
        }

        public Task<bool> GetOption_AutoDownload(Subscription subscription)
        {
            return GetOption(subscription,
                sub => sub.AutoDownload,
                folder => folder.AutoDownload,
                Preferences.Download_AutoDownload);
        }

        public Task<VideoOrder> GetOption_DownloadOrder(Subscription subscription)
        {
            return GetOption(subscription,
                sub => sub.DownloadOrder,
                folder => folder.DownloadOrder,
                Preferences.Download_Order);
        }

        public Task<int> GetOption_DownloadMaxCount(Subscription subscription)
        {
            return GetOption(subscription,
                sub => sub.DownloadMaxCount,
                folder => folder.DownloadMaxCount,
                Preferences.Download_DefaultMaxCount);
        }
    }
}
