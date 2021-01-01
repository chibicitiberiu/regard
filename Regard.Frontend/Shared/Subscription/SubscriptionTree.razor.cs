using Humanizer;
using Microsoft.AspNetCore.Components;
using Regard.Common.API.Model;
using Regard.Common.API.Subscriptions;
using Regard.Frontend.Services;
using Regard.Frontend.Shared.Controls;
using Regard.Services;
using Regard.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Subscription
{
    public partial class SubscriptionTree
    {
        private TreeView<SubscriptionItemViewModelBase> treeView;
        private Dialog deleteDialog;
        private bool deleteDownloadedFiles = false;
        private bool deleteRecursive = false;
        private bool deleteFolder = false;
        private string deleteItemName = "";

        private readonly Dictionary<int, TreeViewNode<SubscriptionItemViewModelBase>> treeFolders = new Dictionary<int, TreeViewNode<SubscriptionItemViewModelBase>>();

        [Inject]
        protected AppState AppState { get; set; }

        [Inject]
        protected MessagingService Messaging { get; set; }

        [Inject]
        protected BackendService Backend { get; set; }

        [Parameter]
        public EventCallback<SubscriptionItemViewModelBase> SelectedItemChanged { get; set; }


        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();

            Messaging.SubscriptionCreated += Messaging_SubscriptionCreated;
            Messaging.SubscriptionUpdated += Messaging_SubscriptionUpdated;
            Messaging.SubscriptionsDeleted += Messaging_SubscriptionsDeleted;
            Messaging.SubscriptionFolderCreated += Messaging_SubscriptionFolderCreated;
            Messaging.SubscriptionFoldersDeleted += Messaging_SubscriptionFoldersDeleted;
            Messaging.SubscriptionFolderUpdated += Messaging_SubscriptionFolderUpdated;
            await Repopulate();
        }

        public async Task Repopulate()
        {
            var (folders, httpResp) = await Backend.SubscriptionFolderList(new SubscriptionFolderListRequest());
            httpResp.EnsureSuccessStatusCode();

            var (subs, httpRespSubs) = await Backend.SubscriptionList(new SubscriptionListRequest());
            httpRespSubs.EnsureSuccessStatusCode();

            BuildTree(folders.Data.Folders, subs.Data.Subscriptions);
        }

        private void BuildTree(ApiSubscriptionFolder[] folders, ApiSubscription[] subscriptions)
        {
            treeFolders.Clear();
            treeView.Root.Children.Clear();

            // Create and add nodes to dictionary
            foreach (var folder in folders)
            {
                var vmFolder = new SubscriptionFolderViewModel(folder);
                var tvFolder = new TreeViewNode<SubscriptionItemViewModelBase>(vmFolder);
                treeFolders.Add(folder.Id, tvFolder);
            }

            // Parent all the nodes
            foreach (var pair in treeFolders)
            {
                var parent = treeView.Root;

                if (pair.Value.Data.ParentId.HasValue)
                    parent = treeFolders[pair.Value.Data.ParentId.Value];

                parent.Children.Add(pair.Value);
            }
            
            // Create subscription leafs
            foreach (var sub in subscriptions)
            {
                var vmSub = new SubscriptionViewModel(sub);
                var tvSub = new TreeViewNode<SubscriptionItemViewModelBase>(vmSub);

                var parent = treeView.Root;
                if (sub.ParentFolderId.HasValue)
                    parent = treeFolders[sub.ParentFolderId.Value];

                parent.Children.Add(tvSub);
            }
        }

        private void Messaging_SubscriptionFolderUpdated(object sender, ApiSubscriptionFolder e)
        {
            Console.WriteLine($"Folder updated: {e.Id} {e.Name}");
        }

        private void Messaging_SubscriptionFoldersDeleted(object sender, int[] folderIds)
        {
            Console.WriteLine($"Folders deleted: {folderIds.Humanize(", ")}");

            foreach (var folderId in folderIds)
            {
                if (treeFolders.TryGetValue(folderId, out var folderNode))
                {
                    folderNode.Parent.Children.Remove(folderNode);
                    treeFolders.Remove(folderId);
                }
            }
        }

        private void Messaging_SubscriptionFolderCreated(object sender, ApiSubscriptionFolder e)
        {
            Console.WriteLine($"Folder created: {e.Id} {e.Name}");

            var parent = treeView.Root;
            if (e.ParentId)
                parent = treeFolders[e.ParentId.Value.Value];

            parent.Children.Add(new TreeViewNode<SubscriptionItemViewModelBase>(new SubscriptionFolderViewModel(e)));
        }

        private void Messaging_SubscriptionUpdated(object sender, ApiSubscription e)
        {
            Console.WriteLine($"Sub updated: {e.Id} {e.Name}");
        }

        private void Messaging_SubscriptionsDeleted(object sender, int[] subIds)
        {
            Console.WriteLine($"Subs deleted: {subIds.Humanize(", ")}");

            // Search recursively for the subIds
            int foundCount = 0;
            var queue = new Queue<TreeViewNode<SubscriptionItemViewModelBase>>();
            queue.Enqueue(treeView.Root);

            while (queue.Count > 0 && foundCount < subIds.Length)
            {
                var current = queue.Dequeue();
                if (current.Data is SubscriptionViewModel subVm 
                    && subIds.Contains(subVm.Subscription.Id))
                {
                    current.Parent.Children.Remove(current);
                    foundCount++;
                }

                foreach (var child in current.Children)
                    queue.Enqueue(child);
            }
        }

        private void Messaging_SubscriptionCreated(object sender, ApiSubscription e)
        {
            Console.WriteLine($"Sub created: {e.Id} {e.Name}");

            var parent = treeView.Root;
            if (e.ParentFolderId.HasValue)
                parent = treeFolders[e.ParentFolderId.Value];

            parent.Children.Add(new TreeViewNode<SubscriptionItemViewModelBase>(new SubscriptionViewModel(e)));
        }

        protected virtual async Task OnSelectedItemChanged(TreeViewNode<SubscriptionItemViewModelBase> item)
        {
            await SelectedItemChanged.InvokeAsync(item.Data);
        }

        protected virtual void OnExpandToggle(TreeViewNode<SubscriptionItemViewModelBase> item)
        {
            item.IsExpanded = !item.IsExpanded;
        }

        private string GetItemCssClass(TreeViewNode<SubscriptionItemViewModelBase> item)
        {
            return $"tree-subscription-menu {CssUtils.BoolToClass(!item.IsSelected, "v-hidden")}";
        }

        protected virtual void OnShowMenu(TreeViewNode<SubscriptionItemViewModelBase> item)
        {
            item.Data.IsContextMenuVisible = true;
        }

        protected virtual void OnEditItem(TreeViewNode<SubscriptionItemViewModelBase> item)
        {
            Console.WriteLine($"TODO: edit item {item.Data.Name}");
        }

        protected async Task OnDeleteItem(TreeViewNode<SubscriptionItemViewModelBase> item)
        {
            deleteRecursive = false;
            deleteDownloadedFiles = false;
            deleteItemName = item.Data.Name;

            if (item.Data is SubscriptionFolderViewModel folderVm)
            {
                deleteFolder = true;
                await deleteDialog.ShowDialog(async result =>
                {
                    if (result == DialogResult.Primary)
                    {
                        await Backend.SubscriptionFolderDelete(new SubscriptionFolderDeleteRequest()
                        {
                            Ids = new[] { folderVm.Folder.Id },
                            Recursive = deleteRecursive,
                            DeleteDownloadedFiles = deleteDownloadedFiles
                        });
                    }
                });
            }
            else if (item.Data is SubscriptionViewModel subVm)
            {
                deleteFolder = false;
                await deleteDialog.ShowDialog(async result =>
                {
                    if (result == DialogResult.Primary)
                    {
                        await Backend.SubscriptionDelete(new SubscriptionDeleteRequest()
                        {
                            Ids = new[] { subVm.Subscription.Id },
                            DeleteDownloadedFiles = deleteDownloadedFiles
                        });
                    }
                });
            }
        }

        protected virtual async Task OnSynchronizeItem(TreeViewNode<SubscriptionItemViewModelBase> item)
        {
            if (item.Data is SubscriptionFolderViewModel folderVm)
                await Backend.SubscriptionFolderSynchronize(new SubscriptionFolderSynchronizeRequest() { Id = folderVm.Folder.Id });

            if (item.Data is SubscriptionViewModel subVm)
                await Backend.SubscriptionSynchronize(new SubscriptionSynchronizeRequest() { Id = subVm.Subscription.Id });
        }
    }
}
