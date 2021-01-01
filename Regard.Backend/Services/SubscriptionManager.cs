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
        private readonly RegardScheduler scheduler;

        public SubscriptionManager(DataContext dataContext,
                                   IPreferencesManager preferencesManager,
                                   IProviderManager providerManager,
                                   MessagingService messaging,
                                   RegardScheduler scheduler)
        {
            this.dataContext = dataContext;
            this.preferencesManager = preferencesManager;
            this.providerManager = providerManager;
            this.messaging = messaging;
            this.scheduler = scheduler;
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

            // Start a sync job
            await scheduler.ScheduleSynchronizeSubscription(sub.Id);

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

        public SubscriptionFolder FindFolder(int id)
        {
            return dataContext.SubscriptionFolders.Find(id);
        }

        public IQueryable<Subscription> GetSubscriptionsRecursive(SubscriptionFolder root)
        {
            return dataContext.GetSubscriptionsRecursive(root);
        }

        public async Task DeleteSubscriptions(UserAccount userAccount, int[] ids, bool deleteFiles)
        {
            if (deleteFiles)
                await scheduler.ScheduleDeleteSubscriptionFiles(ids, true);
            else
                await DeleteSubscriptionsInternal(userAccount, ids);
        }

        public async Task DeleteSubscriptionsInternal(UserAccount userAccount, int[] ids)
        {
            var itemsToDelete = dataContext.Subscriptions.AsQueryable()
                                .Where(x => x.UserId == userAccount.Id)
                                .Where(x => ids.Contains(x.Id));

            await DeleteSubscriptionsInternal(userAccount, itemsToDelete);
        }

        public async Task DeleteSubscriptionsInternal(UserAccount userAccount, IQueryable<Subscription> subs)
        {
            var deletedIds = subs.Select(x => x.Id).ToArray();

            dataContext.Subscriptions.RemoveRange(subs);
            await dataContext.SaveChangesAsync();
            await messaging.NotifySubscriptionsDeleted(userAccount, deletedIds);
        }

        public async Task DeleteSubscriptionFolders(UserAccount userAccount, int[] ids, bool recursive, bool deleteFiles)
        {
            if (recursive)
            {
                if (deleteFiles)
                    await scheduler.ScheduleDeleteSubscriptionFolderFiles(ids, true);
                else
                    await DeleteSubscriptionFoldersInternal(userAccount, ids);
            }
            else
            {
                // Reparent subscriptions and folders (move them to the parent)
                var folders = dataContext.SubscriptionFolders.AsQueryable()
                    .Where(x => x.UserId == userAccount.Id)
                    .Where(x => ids.Contains(x.Id))
                    .ToArray();

                foreach (var folder in folders)
                {
                    await dataContext.SubscriptionFolders.AsQueryable()
                        .Where(x => x.ParentId.HasValue && x.ParentId.Value == folder.Id)
                        .ToAsyncEnumerable()
                        .ForEachAwaitAsync(async x => 
                        {
                            x.ParentId = folder.ParentId;
                            await messaging.NotifySubscriptionFolderUpdated(userAccount, x.ToApi());
                        });

                    await dataContext.Subscriptions.AsQueryable()
                        .Where(x => x.ParentFolderId.HasValue && x.ParentFolderId.Value == folder.Id)
                        .ToAsyncEnumerable()
                        .ForEachAwaitAsync(async x =>
                        {
                            x.ParentFolderId = folder.ParentId;
                            await messaging.NotifySubscriptionUpdated(userAccount, x.ToApi());
                        });
                }

                // Delete folders
                var foldersToDelete = dataContext.SubscriptionFolders.AsQueryable()
                    .Where(x => ids.Contains(x.Id));

                dataContext.SubscriptionFolders.RemoveRange(foldersToDelete);
                await dataContext.SaveChangesAsync();
                await messaging.NotifySubscriptionsFoldersDeleted(userAccount, ids);
            }
        }

        public async Task DeleteSubscriptionFoldersInternal(UserAccount userAccount, int[] ids)
        {
            var folders = dataContext.SubscriptionFolders.AsQueryable()
                .Where(x => x.UserId == userAccount.Id)
                .Where(x => ids.Contains(x.Id))
                .ToArray();

            foreach (var folder in folders)
            {
                var subsToDelete = dataContext.GetSubscriptionsRecursive(folder);
                await DeleteSubscriptionsInternal(userAccount, subsToDelete);
            }

            var foldersToDelete = folders.SelectMany(dataContext.GetFoldersRecursive).ToArray();
            dataContext.SubscriptionFolders.RemoveRange(foldersToDelete);
            await dataContext.SaveChangesAsync();
            await messaging.NotifySubscriptionsFoldersDeleted(userAccount, foldersToDelete.Select(x => x.Id).ToArray());
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

        public async Task SynchronizeSubscription(int subscriptionId)
        {
            await scheduler.ScheduleSynchronizeSubscription(subscriptionId);
        }

        public async Task SynchronizeFolder(int folderId)
        {
            await scheduler.ScheduleSynchronizeFolder(folderId);
        }

        public async Task SynchronizeAll()
        {
            await scheduler.ScheduleGlobalSynchronizeNow();
        }
    }
}
