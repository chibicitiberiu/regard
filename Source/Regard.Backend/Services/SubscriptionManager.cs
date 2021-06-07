using MoreLinq;
using Regard.Backend.Model;
using Regard.Common.API.Model;
using Regard.Backend.DB;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Regard.Backend.Configuration;
using Regard.Backend.Jobs;

namespace Regard.Backend.Services
{
    #region Events

    public class SubscriptionCreatedEventArgs
    {
        /// <summary>
        /// User account which initiated this operation
        /// </summary>
        public UserAccount User { get; set; }

        /// <summary>
        /// Subscription created
        /// </summary>
        public Subscription Subscription { get; set; }
    }

    public class SubscriptionUpdatedEventArgs
    {
        /// <summary>
        /// User account which initiated this operation
        /// </summary>
        public UserAccount User { get; set; }

        /// <summary>
        /// Subscription added
        /// </summary>
        public Subscription Subscription { get; set; }
    }

    public class SubscriptionsDeletedEventArgs
    {
        /// <summary>
        /// User account which initiated this operation
        /// </summary>
        public UserAccount User { get; set; }

        /// <summary>
        /// Subscription IDs that were deleted
        /// </summary>
        public int[] SubscriptionIds { get; set; }
    }

    public class SubscriptionFolderCreatedEventArgs
    {
        /// <summary>
        /// User account which initiated this operation
        /// </summary>
        public UserAccount User { get; set; }

        /// <summary>
        /// Folder created
        /// </summary>
        public SubscriptionFolder Folder { get; set; }
    }

    public class SubscriptionFolderUpdatedEventArgs
    {
        /// <summary>
        /// User account which initiated this operation
        /// </summary>
        public UserAccount User { get; set; }

        /// <summary>
        /// Folder updated
        /// </summary>
        public SubscriptionFolder Folder { get; set; }
    }

    public class SubscriptionFoldersDeletedEventArgs
    {
        /// <summary>
        /// User account which initiated this operation
        /// </summary>
        public UserAccount User { get; set; }

        /// <summary>
        /// Subscription folder IDs that were deleted
        /// </summary>
        public int[] FolderIds { get; set; }
    }

    #endregion

    public class SubscriptionManager 
    {
        private readonly DataContext dataContext;
        private readonly IOptionManager optionManager;
        private readonly IProviderManager providerManager;
        private readonly RegardScheduler scheduler;
        private readonly IVideoStorageService videoStorageService;

        public event EventHandler<SubscriptionCreatedEventArgs> SubscriptionCreated;
        public event EventHandler<SubscriptionUpdatedEventArgs> SubscriptionUpdated;
        public event EventHandler<SubscriptionsDeletedEventArgs> SubscriptionsDeleted;
        public event EventHandler<SubscriptionFolderCreatedEventArgs> FolderCreated;
        public event EventHandler<SubscriptionFolderUpdatedEventArgs> FolderUpdated;
        public event EventHandler<SubscriptionFoldersDeletedEventArgs> FoldersDeleted;

        public SubscriptionManager(DataContext dataContext,
                                   IOptionManager optionManager,
                                   IProviderManager providerManager,
                                   RegardScheduler scheduler,
                                   IVideoStorageService videoStorageService)
        {
            this.dataContext = dataContext;
            this.optionManager = optionManager;
            this.providerManager = providerManager;
            this.scheduler = scheduler;
            this.videoStorageService = videoStorageService;
        }

        public async Task<string> TestUrl(Uri uri)
        {
            var provider = await providerManager.FindFromSubscriptionUrl(uri).FirstOrDefaultAsync();

            if (provider == null)
                throw new ArgumentException("Unsupported service or URL format!");
            
            return provider.Id;
        }

        public void ValidateName(string name, int? parentFolderId, int? subscriptionId = null)
        {
            // Check if name is valid
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be empty!");

            // Check if name is unique
            var query = dataContext.Subscriptions.AsQueryable()
                .Where(x => x.ParentFolderId == parentFolderId)
                .Where(x => x.Name.ToLower() == name.ToLower());

            if (subscriptionId.HasValue)
                query = query.Where(x => x.Id != subscriptionId.Value);

            if (query.Any())
                throw new ArgumentException("Another subscription with the same name already exists in this folder!");
        }

        public async Task<Subscription> Create(UserAccount userAccount,
                                               Uri uri,
                                               int? parentFolderId)
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
            dataContext.SaveChanges();

            SubscriptionCreated?.Invoke(this, new SubscriptionCreatedEventArgs() { User = userAccount, Subscription = sub });

            // Start a sync job
            await SynchronizeSubscription(sub);
            return sub;
        }

        public Subscription CreateEmpty(UserAccount userAccount,
                                        string name,
                                        int? parentFolderId)
        {
            // Verify parent folder ID exists
            SubscriptionFolder parent = null;
            if (parentFolderId.HasValue)
            {
                parent = dataContext.SubscriptionFolders.Find(parentFolderId.Value);
                if (parent == null)
                    throw new Exception("Parent folder not found!");
            }

            // Verify name is unique
            ValidateName(name, parentFolderId);

            // Create subscription
            Subscription sub = new()
            {
                Name = name,
                ParentFolder = parent,
                User = userAccount,
            };
            dataContext.Subscriptions.Add(sub);
            dataContext.SaveChanges();

            SubscriptionCreated?.Invoke(this, new SubscriptionCreatedEventArgs() { User = userAccount, Subscription = sub });
            return sub;
        }

        public Subscription Get(UserAccount user, int subscriptionId)
        {
            return dataContext.Subscriptions.AsQueryable()
                .Where(x => x.Id == subscriptionId)
                .Where(x => x.UserId == user.Id)
                .FirstOrDefault();
        }

        public IQueryable<Subscription> GetAll(UserAccount userAccount)
        {
            return dataContext.Subscriptions.AsQueryable()
                .Where(x => x.UserId == userAccount.Id);
        }

        public void Update(UserAccount user,
                           int subscriptionId,
                           string newName,
                           string newDescription,
                           int? newParentFolderId)
        {
            var subscription = Get(user, subscriptionId);
            if (subscription == null)
                throw new ArgumentException("Subscription not found");

            subscription.Name = newName;
            subscription.Description = newDescription;
            subscription.ParentFolderId = newParentFolderId;
            ValidateName(subscription.Name, subscription.ParentFolderId, subscriptionId);

            dataContext.SaveChanges();

            SubscriptionUpdated?.Invoke(this, new SubscriptionUpdatedEventArgs() { User = user, Subscription = subscription });
        }

        public async Task Delete(UserAccount userAccount,
                                 int[] ids,
                                 bool deleteFiles)
        {
            if (deleteFiles)
                await DeleteSubscriptionFilesJob.Schedule(scheduler, ids, true);
            else
                DeleteInternal(userAccount, ids);
        }

        public void DeleteInternal(UserAccount userAccount,
                                   int[] ids)
        {
            var itemsToDelete = dataContext.Subscriptions.AsQueryable()
                                .Where(x => x.UserId == userAccount.Id)
                                .Where(x => ids.Contains(x.Id));

            DeleteInternal(userAccount, itemsToDelete);
        }

        public void DeleteInternal(UserAccount userAccount,
                                   IQueryable<Subscription> subs)
        {
            var deletedIds = subs.Select(x => x.Id).ToArray();

            dataContext.Subscriptions.RemoveRange(subs);
            dataContext.SaveChanges();
            
            SubscriptionsDeleted?.Invoke(this, new SubscriptionsDeletedEventArgs() { User = userAccount, SubscriptionIds = deletedIds });
        }

        public bool GetConfigAutoDownload(int subscriptionId)
        {
            return optionManager.GetForSubscription(Options.Subscriptions_AutoDownload, subscriptionId);
        }

        public bool? GetConfigAutoDownloadNoResolve(int subscriptionId)
        {
            if (optionManager.GetForSubscriptionNoResolve(Options.Subscriptions_AutoDownload, subscriptionId, out var value))
                return value;
            return null;
        }

        public void CreateFolder(UserAccount user,
                                 string name,
                                 ParentId parentId)
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
                dataContext.SaveChanges();

                FolderCreated?.Invoke(this, new SubscriptionFolderCreatedEventArgs() { User = user, Folder = newFolder });
            }
        }

        public SubscriptionFolder GetFolder(UserAccount user, int id)
        {
            return dataContext.SubscriptionFolders.AsQueryable()
                .Where(x => x.Id == id)
                .Where(x => x.UserId == user.Id)
                .FirstOrDefault();
        }

        public IQueryable<Subscription> GetSubscriptionsRecursive(SubscriptionFolder root)
        {
            return dataContext.GetSubscriptionsRecursive(root);
        }

        public async Task DeleteFolders(UserAccount userAccount,
                                        int[] ids,
                                        bool recursive,
                                        bool deleteFiles)
        {
            if (recursive)
            {
                if (deleteFiles)
                    await DeleteSubscriptionFolderFilesJob.Schedule(scheduler, ids, true);
                else
                    DeleteFoldersInternal(userAccount, ids);
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
                    dataContext.SubscriptionFolders.AsQueryable()
                        .Where(x => x.ParentId.HasValue && x.ParentId.Value == folder.Id)
                        .ForEach(x => 
                        {
                            x.ParentId = folder.ParentId;
                            FolderUpdated?.Invoke(this, new SubscriptionFolderUpdatedEventArgs() { User = userAccount, Folder = x });
                        });

                    dataContext.Subscriptions.AsQueryable()
                        .Where(x => x.ParentFolderId.HasValue && x.ParentFolderId.Value == folder.Id)
                        .ForEach(x =>
                        {
                            x.ParentFolderId = folder.ParentId;
                            SubscriptionUpdated?.Invoke(this, new SubscriptionUpdatedEventArgs() { User = userAccount, Subscription = x });
                        });
                }

                // Delete folders
                var foldersToDelete = dataContext.SubscriptionFolders.AsQueryable()
                    .Where(x => ids.Contains(x.Id));

                dataContext.SubscriptionFolders.RemoveRange(foldersToDelete);
                dataContext.SaveChanges();

                FoldersDeleted?.Invoke(this, new SubscriptionFoldersDeletedEventArgs() { User = userAccount, FolderIds = ids });
            }
        }

        public void DeleteFoldersInternal(UserAccount userAccount,
                                          int[] ids)
        {
            var folders = dataContext.SubscriptionFolders.AsQueryable()
                .Where(x => x.UserId == userAccount.Id)
                .Where(x => ids.Contains(x.Id))
                .ToArray();

            foreach (var folder in folders)
            {
                var subsToDelete = dataContext.GetSubscriptionsRecursive(folder);
                DeleteInternal(userAccount, subsToDelete);
            }

            var foldersToDelete = folders.SelectMany(dataContext.GetFoldersRecursive).ToArray();
            dataContext.SubscriptionFolders.RemoveRange(foldersToDelete);
            dataContext.SaveChanges();

            FoldersDeleted?.Invoke(this, new SubscriptionFoldersDeletedEventArgs() 
            {
                User = userAccount, 
                FolderIds = foldersToDelete.Select(x => x.Id).ToArray() 
            });
        }

        public void ValidateFolderName(string name, int? parentFolderId, int? folderId = null)
        {
            // Check if name is valid
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be empty!");

            // Check if name is unique
            var query = dataContext.SubscriptionFolders.AsQueryable()
                .Where(x => x.ParentId == parentFolderId)
                .Where(x => x.Name.ToLower() == name.ToLower());

            if (folderId.HasValue)
                query = query.Where(x => x.Id != folderId.Value);

            if (query.Any())
                throw new ArgumentException("Another folder with the same name already exists in this folder!");
        }

        public void UpdateFolder(UserAccount user,
                                 int folderId,
                                 string newName,
                                 int? newParentFolderId)
        {
            var folder = GetFolder(user, folderId);
            if (folder == null)
                throw new ArgumentException("Folder not found");

            folder.Name = newName;
            folder.ParentId = newParentFolderId;
            ValidateFolderName(folder.Name, folder.ParentId, folderId);

            dataContext.SaveChanges();

            FolderUpdated?.Invoke(this, new SubscriptionFolderUpdatedEventArgs() { User = user, Folder = folder });
        }

        public IQueryable<SubscriptionFolder> GetAllFolders(UserAccount userAccount)
        {
            return dataContext.SubscriptionFolders.AsQueryable()
                .Where(x => x.UserId == userAccount.Id);
        }

        public Task SynchronizeSubscription(Subscription subscription)
        {
            return SynchronizeJob.Schedule(scheduler, subscription);
        }

        public Task SynchronizeFolder(SubscriptionFolder folder)
        {
            return SynchronizeJob.Schedule(scheduler, folder);
        }

        public Task SynchronizeAll()
        {
            return SynchronizeJob.ScheduleGlobal(scheduler);
        }

        public long Statistic_DiskUsage(int subscriptionId)
        {
            return dataContext.Videos.AsQueryable()
                .Where(x => x.SubscriptionId == subscriptionId)
                .Sum(x => x.DownloadedSize) ?? 0;
        }

        public int Statistic_WatchedVideoCount(int subscriptionId)
        {
            return dataContext.Videos.AsQueryable()
                .Where(x => x.SubscriptionId == subscriptionId)
                .Where(x => x.IsWatched)
                .Count();
        }

        public int Statistic_TotalVideoCount(int subscriptionId)
        {
            return dataContext.Videos.AsQueryable()
                .Where(x => x.SubscriptionId == subscriptionId)
                .Count();
        }

        public int Statistic_DownloadedVideoCount(int subscriptionId)
        {
            return dataContext.Videos.AsQueryable()
                .Where(x => x.SubscriptionId == subscriptionId)
                .Where(x => x.DownloadedPath != null)
                .Count();
        }
    }
}
