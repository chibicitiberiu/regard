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
        private bool loaded = false;

        private Dialog moveDialog;
        private string moveItemName = "";
        private int? moveTargetFolderId = null;
        private int? moveExcludeSubtreeRootId = null;

        private bool isHomeActive = true;

        private readonly Dictionary<int, TreeViewNode<SubscriptionItemViewModelBase>> treeFolders = new Dictionary<int, TreeViewNode<SubscriptionItemViewModelBase>>();

        // Subscription nodes by id. Lets a live update find its node directly instead of scanning the
        // parent computed from the (possibly already changed) incoming DTO.
        private readonly Dictionary<int, TreeViewNode<SubscriptionItemViewModelBase>> treeSubs = new Dictionary<int, TreeViewNode<SubscriptionItemViewModelBase>>();

        [Inject] protected AppState AppState { get; set; }

        [Inject] protected SubscriptionManagerService SubscriptionManager { get; set; }

        [Inject] protected AppController AppController { get; set; }

        [Inject] protected NavigationManager Navigation { get; set; }

        [Parameter] public EventCallback<SubscriptionItemViewModelBase> SelectedItemChanged { get; set; }

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();

            // Subscribe BEFORE loading: Load() populates the dictionaries and raises their change
            // events. If we subscribed afterwards, that initial population would fire into no
            // listeners and the tree would stay empty until the next change (e.g. a manual Refresh).
            AppState.Folders.DictionaryChanged += Folders_DictionaryChanged;
            AppState.Subscriptions.DictionaryChanged += Subscriptions_DictionaryChanged;
            AppState.PropertyChanged += AppState_PropertyChanged;
            isHomeActive = AppState.SelectedSubscription == null;

            await SubscriptionManager.Load();
            loaded = true;

            // Load's await yields, so the first render (which assigns the TreeView @ref) usually
            // happens while Load is still in flight -- OnAfterRender then builds an empty tree, and
            // the change events raised during population may have hit the treeView==null guard.
            // Rebuild now that the data is in and the ref is ready. No-op if the ref isn't set yet
            // (a synchronous Load on remount); OnAfterRender(firstRender) covers that case.
            RebuildTree();
        }

        protected override void OnAfterRender(bool firstRender)
        {
            base.OnAfterRender(firstRender);

            // The TreeView @ref is only populated after the first render, so build the initial
            // tree here rather than in OnInitializedAsync (which may complete before any render).
            if (firstRender)
                RebuildTree();
        }

        private void RebuildTree()
        {
            // Guard against being called before the TreeView @ref is assigned (e.g. a dictionary
            // change that arrives before the first render) — OnAfterRender builds it once ready.
            if (treeView == null)
                return;

            treeFolders.Clear();
            treeSubs.Clear();
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
                AddSorted(GetParentFolder(pair.Value.Data.ParentId), pair.Value);
            
            // Create subscription leafs
            foreach (var sub in AppState.Subscriptions.Values)
                AddSubscription(sub);

            StateHasChanged();
        }

        public void DeselectAll()
        {
            treeView.SelectedItem = null;
        }

        private void AppState_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppState.SelectedSubscription))
            {
                // Home is the all-videos root view: active exactly when nothing in the tree is selected.
                isHomeActive = AppState.SelectedSubscription == null;
                StateHasChanged();
            }
        }

        private void OnHomeClicked()
        {
            DeselectAll();                 // clears tree selection -> AppController navigates to "/"
            Navigation.NavigateTo("/");    // explicit, in case nothing was selected (already deselected)
        }

        private void Subscriptions_DictionaryChanged(object sender, DictionaryChangedEventArgs<int, ApiSubscription> e)
        {
            if (treeView == null)   // not rendered yet; OnAfterRender will build from current state
                return;

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                RebuildTree();
            }
            else if (e.Action == NotifyCollectionChangedAction.Replace)
            {
                // A live update of an existing subscription. Reuse the node (see ReplaceSubscription) so
                // the selection survives -- remove+add would deselect and navigate the user home.
                foreach (var newItem in e.NewItems)
                    ReplaceSubscription(newItem.Value);
            }
            else
            {
                foreach (var oldItem in e.OldItems)
                    RemoveSubscription(oldItem.Value);

                foreach (var newItem in e.NewItems)
                    AddSubscription(newItem.Value);
            }

            StateHasChanged();
        }

        private void Folders_DictionaryChanged(object sender, DictionaryChangedEventArgs<int, ApiSubscriptionFolder> e)
        {
            if (treeView == null)   // not rendered yet; OnAfterRender will build from current state
                return;

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                RebuildTree();
            }
            else if (e.Action == NotifyCollectionChangedAction.Replace)
            {
                // Update in place rather than rebuilding the whole tree: a rebuild detaches every node,
                // which loses the selection (and expansion state) of whatever the user is viewing.
                foreach (var newItem in e.NewItems)
                    ReplaceFolder(newItem.Value);
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
                    AddSorted(GetParentFolder(tvFolder.Data.ParentId), tvFolder);
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

        /// <summary>
        /// Adds a node under <paramref name="parent"/> at its sorted position. Folder nodes hold a
        /// SortedObservableCollection, so Add() already places them correctly; TreeView.Root is a plain
        /// node whose children are unsorted (they only look ordered because the API returns them that
        /// way), so root-level nodes are positioned by hand.
        /// </summary>
        private void AddSorted(TreeViewNode<SubscriptionItemViewModelBase> parent,
                               TreeViewNode<SubscriptionItemViewModelBase> node)
        {
            if (parent != treeView.Root)
            {
                parent.Children.Add(node);
                return;
            }

            int i = 0;
            while (i < parent.Children.Count
                   && string.CompareOrdinal(parent.Children[i].Data.SortKey, node.Data.SortKey) < 0)
                i++;

            parent.Children.Insert(i, node);
        }

        private void AddSubscription(ApiSubscription sub)
        {
            FixRelativeUrl(sub);
            var vmSub = new SubscriptionViewModel(sub);
            var tvSub = new SortedTreeViewNode<SubscriptionItemViewModelBase, string>(vmSub);

            treeSubs[sub.Id] = tvSub;
            AddSorted(GetParentFolder(sub.ParentFolderId), tvSub);
        }

        /// <summary>
        /// Applies a live update to an existing subscription node. The node object is reused, so
        /// TreeView.SelectedItem (which holds a node reference) keeps pointing at it — the old
        /// remove-then-add path deselected it, which navigated the user back to the home page. The node
        /// is only re-inserted when its sort position or parent actually changed, since
        /// SortedObservableCollection sorts on insert and never re-sorts in place.
        /// </summary>
        private void ReplaceSubscription(ApiSubscription sub)
        {
            if (!treeSubs.TryGetValue(sub.Id, out var node))
            {
                AddSubscription(sub);
                return;
            }

            FixRelativeUrl(sub);
            var vm = new SubscriptionViewModel(sub);
            bool reorder = node.Data.SortKey != vm.SortKey || node.Data.ParentId != sub.ParentFolderId;

            node.Data = vm;   // repaints through ChildPropertyChanged -> TreeView.StateHasChanged

            if (reorder)
            {
                node.Parent?.Children.Remove(node);
                AddSorted(GetParentFolder(sub.ParentFolderId), node);
            }
        }

        /// <summary>Same as <see cref="ReplaceSubscription"/>; reusing the node also keeps the subtree.</summary>
        private void ReplaceFolder(ApiSubscriptionFolder folder)
        {
            if (!treeFolders.TryGetValue(folder.Id, out var node))
                return;

            var vm = new SubscriptionFolderViewModel(folder);
            int? parentId = folder.ParentId;
            bool reorder = node.Data.SortKey != vm.SortKey || node.Data.ParentId != parentId;

            node.Data = vm;

            if (reorder)
            {
                node.Parent?.Children.Remove(node);
                AddSorted(GetParentFolder(parentId), node);
            }
        }

        private bool RemoveSubscription(ApiSubscription subscription)
        {
            if (!treeSubs.TryGetValue(subscription.Id, out var node))
                return false;

            treeSubs.Remove(subscription.Id);
            (node.Parent ?? GetParentFolder(subscription.ParentFolderId)).Children.Remove(node);

            if (node == treeView.SelectedItem)
                treeView.SelectedItem = null;

            return true;
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
            if (item == null)
            {
                // Deselected (e.g. Home / logo): clear the selection so the root view shows.
                AppState.SelectedSubscription = null;
                await SelectedItemChanged.InvokeAsync(null);
                return;
            }

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

        // --- Move to folder (menu action) ---------------------------------------------------------

        protected async Task OnMoveItem(TreeViewNode<SubscriptionItemViewModelBase> item)
        {
            moveItemName = item.Data.Name;

            if (item.Data is SubscriptionFolderViewModel folderVm)
            {
                moveTargetFolderId = folderVm.ParentId;
                moveExcludeSubtreeRootId = folderVm.Folder.Id;   // hide itself + descendants as targets
                await moveDialog.ShowDialog(async result =>
                {
                    if (result == DialogResult.Primary)
                        await SubscriptionManager.Move(folderVm.Folder, moveTargetFolderId);
                });
            }
            else if (item.Data is SubscriptionViewModel subVm)
            {
                moveTargetFolderId = subVm.ParentId;
                moveExcludeSubtreeRootId = null;
                await moveDialog.ShowDialog(async result =>
                {
                    if (result == DialogResult.Primary)
                        await SubscriptionManager.Move(subVm.Subscription, moveTargetFolderId);
                });
            }
        }

        // --- Drag and drop --------------------------------------------------------------------------

        /// <summary>The folder a drop onto <paramref name="target"/> would move the item into (null = root).</summary>
        private int? ResolveDestinationFolderId(TreeViewNode<SubscriptionItemViewModelBase> target)
        {
            if (target == null)
                return null;   // dropped on empty tree space -> root
            if (target.Data is SubscriptionFolderViewModel folderVm)
                return folderVm.Folder.Id;
            if (target.Data is SubscriptionViewModel subVm)
                return subVm.ParentId;   // drop beside a sub -> its containing folder
            return null;
        }

        /// <summary>True if moving <paramref name="draggedFolderId"/> under <paramref name="destFolderId"/> would loop.</summary>
        private bool WouldCreateCycle(int draggedFolderId, int? destFolderId)
        {
            int? current = destFolderId;
            int guard = 0;
            while (current.HasValue && guard++ < 4096)
            {
                if (current.Value == draggedFolderId)
                    return true;
                current = AppState.Folders.TryGetValue(current.Value, out var f) ? (int?)f.ParentId : null;
            }
            return false;
        }

        private bool CanDropItem(TreeViewNode<SubscriptionItemViewModelBase> dragged, TreeViewNode<SubscriptionItemViewModelBase> target)
        {
            if (dragged == null)
                return false;

            int? dest = ResolveDestinationFolderId(target);

            if (dragged.Data is SubscriptionFolderViewModel folderVm)
            {
                if (dest == folderVm.Folder.Id)
                    return false;                                   // into itself
                if (WouldCreateCycle(folderVm.Folder.Id, dest))
                    return false;                                   // into a descendant
                return dest != folderVm.ParentId;                   // already there -> no-op
            }
            if (dragged.Data is SubscriptionViewModel subVm)
            {
                return dest != subVm.ParentId;                      // already there -> no-op
            }
            return false;
        }

        private async Task OnItemDropped((TreeViewNode<SubscriptionItemViewModelBase> Dragged, TreeViewNode<SubscriptionItemViewModelBase> Target) drop)
        {
            int? dest = ResolveDestinationFolderId(drop.Target);

            if (drop.Dragged.Data is SubscriptionFolderViewModel folderVm)
                await SubscriptionManager.Move(folderVm.Folder, dest);
            else if (drop.Dragged.Data is SubscriptionViewModel subVm)
                await SubscriptionManager.Move(subVm.Subscription, dest);
        }
    }
}
