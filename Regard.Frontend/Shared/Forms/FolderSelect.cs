using Microsoft.AspNetCore.Components;
using Regard.Common.API.Model;
using Regard.Common.API.Subscriptions;
using Regard.Frontend.Services;
using Regard.Frontend.Shared.Controls;
using Regard.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Forms
{
    public class FolderSelectViewModel
    {
        public int? Id { get; set; }

        public string DisplayName { get; set; }
    }

    public class FolderSelect : RgInputSelect<FolderSelectViewModel, int?>
    {
        protected ObservableCollection<FolderSelectViewModel> Folders { get; } = new ObservableCollection<FolderSelectViewModel>();

        protected Dictionary<int, FolderSelectViewModel> FoldersDict { get; } = new Dictionary<int, FolderSelectViewModel>();

        [Inject] protected MessagingService Messaging { get; set; }

        [Inject] protected BackendService Backend { get; set; }

        //public FolderSelect()
        //{
        //}

        protected override async Task OnInitializedAsync()
        {
            KeyFunc = folder => folder.Id;
            DisplayTextFunc = folder => folder.DisplayName;
            ShowDefaultOption = false;
            ItemsSource = Folders;

            await base.OnInitializedAsync();

            // Subscribe to changes
            Messaging.SubscriptionFolderCreated += Messaging_SubscriptionFolderCreated;
            Messaging.SubscriptionFoldersDeleted += Messaging_SubscriptionFolderDeleted;
            Messaging.SubscriptionFolderUpdated += Messaging_SubscriptionFolderUpdated;

            // Populate folder list
            var (resp, httpResp) = await Backend.SubscriptionFolderList(new SubscriptionFolderListRequest());
            httpResp.EnsureSuccessStatusCode();
            RepopulateFolders(resp.Data.Folders);
        }

        private void RepopulateFolders(ApiSubscriptionFolder[] folders)
        {
            Folders.Clear();
            Folders.Add(new FolderSelectViewModel() { Id = null, DisplayName = "<none>" });
            this.FoldersDict.Clear();

            var queue = new Queue<ApiSubscriptionFolder>(folders);
            var pushedItems = new HashSet<int>();

            foreach (var item in folders)
                Console.WriteLine($">>> {item.Id} > {item.Name}");

            while (queue.Count > 0)
            {
                var item = queue.Dequeue();

                // Root level - just add it to the dict
                if (!item.ParentId)
                {
                    FoldersDict.Add(item.Id, new FolderSelectViewModel() { Id = item.Id, DisplayName = item.Name });
                }
                // The parent was already added to the dict, use it to build the display name
                else if (FoldersDict.TryGetValue(item.ParentId.Value.Value, out FolderSelectViewModel parent))
                {
                    FoldersDict.Add(item.Id, new FolderSelectViewModel()
                    {
                        Id = item.Id,
                        DisplayName = $"{parent.DisplayName}\\{item.Name}"
                    });
                }
                // Parent wasn't added to the dict yet, put back in the queue
                else if (!pushedItems.Contains(item.Id))
                {
                    // Push to end of queue
                    queue.Enqueue(item);

                    // Keep track of what folders we already pushed, to avoid an infinite loop
                    pushedItems.Add(item.Id);
                }
                // Orphaned folder, we did not get its parent :(
                else
                {
                    Debug.Fail("Orphan folder found!", $"Folder {item.Id} is orphaned, parent {item.ParentId} not found!");
                }
            }

            // Put folders to observable collection
            foreach (var folder in FoldersDict.Values.OrderBy(x => x.DisplayName))
                Folders.Add(folder);

            foreach (var item in Folders)
                Console.WriteLine($"{item.Id} -> {item.DisplayName}");
        }

        private void Messaging_SubscriptionFolderUpdated(object sender, ApiSubscriptionFolder e)
        {
            // TODO
        }

        private void Messaging_SubscriptionFolderDeleted(object sender, int[] folderIds)
        {
            foreach (var id in folderIds)
            {
                if (FoldersDict.TryGetValue(id, out FolderSelectViewModel vmFolder))
                {
                    FoldersDict.Remove(id);
                    Folders.Remove(vmFolder);
                }
            }
        }

        private void Messaging_SubscriptionFolderCreated(object sender, ApiSubscriptionFolder e)
        {
            if (!e.ParentId)
            {
                var vmFolder = new FolderSelectViewModel() { Id = e.Id, DisplayName = e.Name };
                FoldersDict.Add(e.Id, vmFolder);
                Folders.Add(vmFolder);
            }
        }

        public void Dispose()
        {
            Messaging.SubscriptionFolderCreated -= Messaging_SubscriptionFolderCreated;
            Messaging.SubscriptionFoldersDeleted -= Messaging_SubscriptionFolderDeleted;
            Messaging.SubscriptionFolderUpdated -= Messaging_SubscriptionFolderUpdated;
        }
    }
}
