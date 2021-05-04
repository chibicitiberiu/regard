using Humanizer;
using Microsoft.AspNetCore.Components;
using Regard.Common.API.Model;
using Regard.Common.API.Subscriptions;
using Regard.Common.Utils.Collections;
using Regard.Frontend.Services;
using Regard.Frontend.Shared.Controls;
using Regard.Services;
using Regard.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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

        [Inject] protected AppState AppState { get; set; }

        [Inject] protected SubscriptionManagerService SubscriptionManager { get; set; }

        [Inject] protected AppController AppController { get; set; }

        [Parameter] public EventCallback<SubscriptionItemViewModelBase> SelectedItemChanged { get; set; }

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            await SubscriptionManager.Load();

            RebuildTree();

            AppState.Folders.DictionaryChanged += Folders_DictionaryChanged;
            AppState.Subscriptions.DictionaryChanged += Subscriptions_DictionaryChanged;
        }

        private void RebuildTree()
        {
            treeFolders.Clear();
            treeView.Root.Children.Clear();

            // Create and add nodes to dictionary
            foreach (var folder in AppState.Folders.Values)
            {
                var vmFolder = new SubscriptionFolderViewModel(folder);
                var tvFolder = new SortedTreeViewNode<SubscriptionItemViewModelBase, string>(vmFolder);
                treeFolders.Add(folder.Id, tvFolder);
            }

            // Parent all the nodes
            foreach (var pair in treeFolders)
                GetParentFolder(pair.Value.Data.ParentId).Children.Add(pair.Value);
            
            // Create subscription leafs
            foreach (var sub in AppState.Subscriptions.Values)
                AddSubscription(sub);
        }

        public void DeselectAll()
        {
            treeView.SelectedItem = null;
        }

        private void Subscriptions_DictionaryChanged(object sender, DictionaryChangedEventArgs<int, ApiSubscription> e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                RebuildTree();
            }
            else
            {
                foreach (var oldItem in e.OldItems)
                    RemoveSubscription(oldItem.Value);

                foreach (var newItem in e.NewItems)
                    AddSubscription(newItem.Value);
            }
        }

        private void Folders_DictionaryChanged(object sender, DictionaryChangedEventArgs<int, ApiSubscriptionFolder> e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset || e.Action == NotifyCollectionChangedAction.Replace)
            {
                RebuildTree();
            }
            else
            {
                foreach (var oldItem in e.OldItems)
                    RemoveFolder(oldItem.Value);

                // Add all the folders first
                foreach (var newItem in e.NewItems)
                {
                    var vmFolder = new SubscriptionFolderViewModel(newItem.Value);
                    var tvFolder = new SortedTreeViewNode<SubscriptionItemViewModelBase, string>(vmFolder);
                    treeFolders.Add(newItem.Key, tvFolder);
                }

                // And then parent them
                foreach (var newItem in e.NewItems)
                {
                    var tvFolder = treeFolders[newItem.Key];
                    GetParentFolder(tvFolder.Data.ParentId).Children.Add(tvFolder);
                }
            }

            StateHasChanged();
        }

        private TreeViewNode<SubscriptionItemViewModelBase> GetParentFolder(int? parentId)
        {
            if (parentId.HasValue && treeFolders.TryGetValue(parentId.Value, out var folder))
                return folder;

            return treeView.Root;
        }

        private void FixRelativeUrl(ApiSubscription sub)
        {
            if (!sub.ThumbnailUrl.IsAbsoluteUri)
                sub.ThumbnailUrl = new Uri(AppState.BackendBase, sub.ThumbnailUrl);
        }

        private void AddSubscription(ApiSubscription sub)
        {
            FixRelativeUrl(sub);
            var vmSub = new SubscriptionViewModel(sub);
            var tvSub = new SortedTreeViewNode<SubscriptionItemViewModelBase, string>(vmSub);

            GetParentFolder(sub.ParentFolderId).Children.Add(tvSub);
        }

        private bool RemoveSubscription(ApiSubscription subscription)
        {
            var parent = GetParentFolder(subscription.ParentFolderId);

            foreach (var child in parent.Children)
            {
                if (child.Data is SubscriptionViewModel subVm
                    && subVm.Subscription.Id == subscription.Id)
                {
                    parent.Children.Remove(child);

                    if (child == treeView.SelectedItem)
                        treeView.SelectedItem = null;

                    return true;
                }
            }

            return false;
        }

        private bool RemoveFolder(ApiSubscriptionFolder folder)
        {
            if (treeFolders.TryGetValue(folder.Id, out var folderNode))
            {
                folderNode.Parent.Children.Remove(folderNode);
                treeFolders.Remove(folder.Id);

                // Deselect
                if (treeView.SelectedItem != null)
                {
                    if (treeView.SelectedItem.Data is SubscriptionViewModel subVm && subVm.ParentId == folder.Id)
                        treeView.SelectedItem = null;

                    if (treeView.SelectedItem.Data is SubscriptionFolderViewModel folderVm && folderVm.Folder.Id == folder.Id)
                        treeView.SelectedItem = null;
                }
                return true;
            }

            return false;
        }

        protected virtual async Task OnSelectedItemChanged(TreeViewNode<SubscriptionItemViewModelBase> item)
        {
            if (item.Data is SubscriptionFolderViewModel vmFolder)
                AppState.SelectedSubscription = vmFolder.Folder;

            else if (item.Data is SubscriptionViewModel vmSub)
                AppState.SelectedSubscription = vmSub.Subscription;

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
            if (item.Data is SubscriptionViewModel subVM)
                AppController.EditSubscription(subVM.Subscription);

            else if (item.Data is SubscriptionFolderViewModel subFolderVM)
                AppController.EditSubscription(subFolderVM.Folder);
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
                        await SubscriptionManager.Delete(folderVm.Folder, deleteRecursive, deleteDownloadedFiles);
                });
            }
            else if (item.Data is SubscriptionViewModel subVm)
            {
                deleteFolder = false;
                await deleteDialog.ShowDialog(async result =>
                {
                    if (result == DialogResult.Primary)
                        await SubscriptionManager.Delete(subVm.Subscription, deleteDownloadedFiles);
                });
            }
        }

        protected virtual async Task OnSynchronizeItem(TreeViewNode<SubscriptionItemViewModelBase> item)
        {
            if (item.Data is SubscriptionFolderViewModel folderVm)
                await SubscriptionManager.Synchronize(folderVm.Folder);

            if (item.Data is SubscriptionViewModel subVm)
                await SubscriptionManager.Synchronize(subVm.Subscription);
        }
    }
}
