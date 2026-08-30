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
using Regard.Backend.Thumbnails;

namespace Regard.Backend.Services
{
    // The domain events that used to live here (SubscriptionCreated/Updated/Deleted and the folder
    // equivalents) were removed: their only subscriber was a bridge that was never instantiated, so they
    // fired into nothing for years. Live updates now come from the EF change feed
    // (Services/LiveUpdates/ChangeFeedInterceptor), which observes the SaveChanges calls below and so
    // cannot be forgotten by a new code path.


    public class SubscriptionManager 
    {
        private readonly DataContext dataContext;
        private readonly IOptionManager optionManager;
        private readonly IProviderManager providerManager;
        private readonly RegardScheduler scheduler;
        private readonly IVideoStorageService videoStorageService;

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
                                               int? parentFolderId,
                                               bool allowDuplicate = false,
                                               bool autoDownload = true,
                                               bool scheduleThumbnailFetch = true)
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

            // Same channel/playlist resolves to the same (provider, SubscriptionId) regardless of
            // which URL form was pasted, so that's the reliable duplicate key. Duplicates are a
            // valid case (e.g. same source, different filters), so this only warns unless allowed.
            if (!allowDuplicate && sub.SubscriptionId != null)
            {
                var existing = dataContext.Subscriptions.AsQueryable()
                    .Where(x => x.UserId == userAccount.Id)
                    .Where(x => x.SubscriptionProviderId == sub.SubscriptionProviderId)
                    .Where(x => x.SubscriptionId == sub.SubscriptionId)
                    .FirstOrDefault();
                if (existing != null)
                    throw new DuplicateSubscriptionException(existing.Name);
            }

            sub.User = userAccount;
            sub.ParentFolder = parent;
            dataContext.Subscriptions.Add(sub);
            dataContext.SaveChanges();

            // AutoDownload defaults to true globally, so only persist a per-subscription override when
            // the user opted out — and before the create-time sync, so ProcessDownloadRules honors it.
            if (!autoDownload)
                optionManager.SetForSubscription(Options.Subscriptions_AutoDownload, sub.Id, false);


            // Cache the channel avatar now so it's served locally within seconds (bulk imports do this
            // once at the end instead of per-subscription).
            if (scheduleThumbnailFetch)
                await ScheduleThumbnailFetch();

            // Start a sync job
            await SynchronizeSubscription(sub);
            return sub;
        }

        /// <summary>
        /// Creates a subscription without contacting the network, and defers everything that does to
        /// <see cref="ResolveSubscriptionJob"/>: provider resolution, the real name/description/artwork,
        /// and the first sync.
        ///
        /// Why this exists as a separate method rather than a change to <see cref="Create"/>: the
        /// synchronous path runs two full yt-dlp extractions of the same URL plus a blocking HTML scrape,
        /// each preceded by throttle pacing that is shared with background syncs — which is how "Create"
        /// came to block the UI for ~3 minutes. <see cref="ImportSubscriptionsJob"/> still wants the
        /// synchronous contract (it reports per-feed progress and catches duplicates per feed), so
        /// <see cref="Create"/> stays as it is.
        ///
        /// The row is inserted with a placeholder name because Subscription.Name is [Required]; the live
        /// change feed pushes the real name the moment the job resolves it, and the tree re-sorts itself.
        /// </summary>
        public async Task<Subscription> CreateDeferred(UserAccount userAccount,
                                                       Uri uri,
                                                       int? parentFolderId,
                                                       bool allowDuplicate = false,
                                                       bool autoDownload = true)
        {
            SubscriptionFolder parent = null;
            if (parentFolderId.HasValue)
            {
                parent = dataContext.SubscriptionFolders.Find(parentFolderId.Value);
                if (parent == null)
                    throw new Exception("Parent folder not found!");
            }

            // Normalize the way the providers do, so the stored OriginalUrl matches what a later sync
            // will actually fetch, and so the duplicate check below compares like with like.
            var normalized = NormalizeSubscriptionUrl(uri);

            // Cheap duplicate check: catches re-pasting a link you already subscribed to, which is the
            // common case and the only one that can be answered synchronously. The authoritative check
            // keys on the provider's own id and can only run after extraction, so the job repeats it.
            if (!allowDuplicate)
            {
                var url = normalized.ToString();
                var existing = dataContext.Subscriptions.AsQueryable()
                    .Where(x => x.UserId == userAccount.Id)
                    .Where(x => x.OriginalUrl == url)
                    .FirstOrDefault();
                if (existing != null)
                    throw new DuplicateSubscriptionException(existing.Name);
            }

            // Free, string-only provider hint (no network). When it can't tell — anything that isn't
            // YouTube — the job resolves the provider itself.
            var hinted = providerManager.HintProviderFor(normalized);

            var sub = new Subscription()
            {
                Name = PlaceholderName(normalized),
                OriginalUrl = normalized.ToString(),
                SubscriptionProviderId = hinted,
                User = userAccount,
                ParentFolder = parent,
            };

            dataContext.Subscriptions.Add(sub);
            dataContext.SaveChanges();

            if (!autoDownload)
                optionManager.SetForSubscription(Options.Subscriptions_AutoDownload, sub.Id, false);

            await ResolveSubscriptionJob.Schedule(scheduler, sub, allowDuplicate);
            return sub;
        }

        /// <summary>
        /// A stand-in name until the provider tells us the real one. Derived from the URL (a @handle or
        /// the last meaningful path segment) so the tree row lands near its eventual alphabetical
        /// position rather than jumping from "https://…".
        /// </summary>
        public static string PlaceholderName(Uri uri)
        {
            var segments = uri.Segments
                .Select(s => s.Trim('/'))
                .Where(s => s.Length > 0 && !s.Equals("videos", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var handle = segments.LastOrDefault(s => s.StartsWith("@", StringComparison.Ordinal));
            var candidate = handle ?? segments.LastOrDefault();

            if (!string.IsNullOrWhiteSpace(candidate))
                return Uri.UnescapeDataString(candidate).TrimStart('@');

            return uri.Host;
        }

        /// <summary>Applies the provider's own URL fixups so stored URLs and comparisons are consistent.</summary>
        private static Uri NormalizeSubscriptionUrl(Uri uri)
        {
            try
            {
                return Regard.Backend.Providers.YouTubeDL.YouTubeUrlHelper.FixYouTubeChannelUri(uri);
            }
            catch
            {
                return uri;   // non-YouTube or unparseable: keep as pasted
            }
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

        }

        /// <summary>
        /// Reparents a subscription and nothing else. Kept separate from <see cref="Update"/> so a
        /// drag-and-drop / "move to folder" never runs through the full-replace edit path (which would
        /// clear the subscription's name and every unset option).
        /// </summary>
        public void MoveSubscription(UserAccount user, int subscriptionId, int? newParentFolderId)
        {
            var subscription = Get(user, subscriptionId);
            if (subscription == null)
                throw new ArgumentException("Subscription not found");

            // Name is unchanged, but re-validate against the destination folder to reject a collision.
            ValidateName(subscription.Name, newParentFolderId, subscriptionId);
            subscription.ParentFolderId = newParentFolderId;

            dataContext.SaveChanges();

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

            }
        }

        /// <summary>
        /// Returns the user's folder named <paramref name="name"/> under <paramref name="parentFolderId"/>
        /// (case-insensitive), creating it if absent. Used by import to mirror OPML folder groups.
        /// </summary>
        public SubscriptionFolder GetOrCreateFolder(UserAccount user, string name, int? parentFolderId)
        {
            var existing = dataContext.SubscriptionFolders.AsQueryable()
                .Where(x => x.UserId == user.Id)
                .Where(x => x.ParentId == parentFolderId)
                .Where(x => x.Name.ToUpper() == name.ToUpper())
                .FirstOrDefault();
            if (existing != null)
                return existing;

            var folder = new SubscriptionFolder()
            {
                User = user,
                ParentId = parentFolderId,
                Name = name,
            };
            dataContext.SubscriptionFolders.Add(folder);
            dataContext.SaveChanges();

            return folder;
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
                        });

                    dataContext.Subscriptions.AsQueryable()
                        .Where(x => x.ParentFolderId.HasValue && x.ParentFolderId.Value == folder.Id)
                        .ForEach(x =>
                        {
                            x.ParentFolderId = folder.ParentId;
                        });
                }

                // Delete folders
                var foldersToDelete = dataContext.SubscriptionFolders.AsQueryable()
                    .Where(x => ids.Contains(x.Id));

                dataContext.SubscriptionFolders.RemoveRange(foldersToDelete);
                dataContext.SaveChanges();

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

            // Prevent cycles: the new parent must not be the folder itself or one of its
            // descendants. Walk up the prospective parent's ancestor chain — if we reach this
            // folder, the move would form a loop that breaks tree rendering and recursive queries.
            for (int? ancestorId = newParentFolderId; ancestorId.HasValue;)
            {
                if (ancestorId.Value == folderId)
                    throw new ArgumentException("A folder can't be moved into itself or one of its subfolders.");

                ancestorId = dataContext.SubscriptionFolders.AsQueryable()
                    .Where(x => x.Id == ancestorId.Value && x.UserId == user.Id)
                    .Select(x => x.ParentId)
                    .FirstOrDefault();
            }

            folder.Name = newName;
            folder.ParentId = newParentFolderId;
            ValidateFolderName(folder.Name, folder.ParentId, folderId);

            dataContext.SaveChanges();

        }

        /// <summary>
        /// Reparents a folder and nothing else (name and every option preserved). Runs the same
        /// cycle guard as <see cref="UpdateFolder"/>. Kept separate from the full-replace edit path.
        /// </summary>
        public void MoveFolder(UserAccount user, int folderId, int? newParentFolderId)
        {
            var folder = GetFolder(user, folderId);
            if (folder == null)
                throw new ArgumentException("Folder not found");

            // Prevent cycles: the new parent must not be the folder itself or one of its descendants.
            for (int? ancestorId = newParentFolderId; ancestorId.HasValue;)
            {
                if (ancestorId.Value == folderId)
                    throw new ArgumentException("A folder can't be moved into itself or one of its subfolders.");

                ancestorId = dataContext.SubscriptionFolders.AsQueryable()
                    .Where(x => x.Id == ancestorId.Value && x.UserId == user.Id)
                    .Select(x => x.ParentId)
                    .FirstOrDefault();
            }

            // Name unchanged; re-validate against the destination to reject a collision.
            ValidateFolderName(folder.Name, newParentFolderId, folderId);
            folder.ParentId = newParentFolderId;

            dataContext.SaveChanges();

        }

        public IQueryable<SubscriptionFolder> GetAllFolders(UserAccount userAccount)
        {
            return dataContext.SubscriptionFolders.AsQueryable()
                .Where(x => x.UserId == userAccount.Id);
        }

        /// <summary>Kicks off a one-off pass to cache any pending (still-remote) thumbnails now.</summary>
        public Task ScheduleThumbnailFetch()
        {
            return FetchThumbnailsJob.ScheduleNow(scheduler);
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
