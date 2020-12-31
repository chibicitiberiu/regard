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
        [Inject]
        protected AppState AppState { get; set; }

        [Inject]
        protected MessagingService Messaging { get; set; }

        [Inject]
        protected BackendService Backend { get; set; }

        [Parameter]
        public EventCallback<SubscriptionItemViewModelBase> SelectedItemChanged { get; set; }

        private TreeView<SubscriptionItemViewModelBase> treeView;

        private readonly Dictionary<int, TreeViewNode<SubscriptionItemViewModelBase>> treeFolders = new Dictionary<int, TreeViewNode<SubscriptionItemViewModelBase>>();

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();

            Messaging.SubscriptionCreated += Messaging_SubscriptionCreated;
            Messaging.SubscriptionUpdated += Messaging_SubscriptionUpdated;
            Messaging.SubscriptionDeleted += Messaging_SubscriptionDeleted;
            Messaging.SubscriptionFolderCreated += Messaging_SubscriptionFolderCreated;
            Messaging.SubscriptionFolderDeleted += Messaging_SubscriptionFolderDeleted;
            Messaging.SubscriptionFolderUpdated += Messaging_SubscriptionFolderUpdated;

            var (folders, httpResp) = await Backend.SubscriptionFolderList(new SubscriptionFolderListRequest());
            httpResp.EnsureSuccessStatusCode();

            var (subs, httpRespSubs) = await Backend.SubscriptionList(new SubscriptionListRequest());
            httpRespSubs.EnsureSuccessStatusCode();

            BuildTree(folders.Data.Folders, subs.Data.Subscriptions);
        }

        private void BuildTree(ApiSubscriptionFolder[] folders, ApiSubscription[] subscriptions)
        {
            treeFolders.Clear();

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

        private void Messaging_SubscriptionFolderDeleted(object sender, ApiSubscriptionFolder e)
        {
            Console.WriteLine($"Folder deleted: {e.Id} {e.Name}");
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

        private void Messaging_SubscriptionDeleted(object sender, ApiSubscription e)
        {
            Console.WriteLine($"Sub deleted: {e.Id} {e.Name}");

            var parent = treeView.Root;
            if (e.ParentFolderId.HasValue)
                parent = treeFolders[e.ParentFolderId.Value];

            var itemToDelete = parent.Children.First(x => x.Data is SubscriptionViewModel vmSub && vmSub.Subscription.Id == e.Id);
            parent.Children.Remove(itemToDelete);
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

        protected virtual void OnDeleteItem(TreeViewNode<SubscriptionItemViewModelBase> item)
        {
            Console.WriteLine($"TODO: delete item {item.Data.Name}");
        }

        protected virtual void OnRefreshItem(TreeViewNode<SubscriptionItemViewModelBase> item)
        {
            Console.WriteLine($"TODO: refresh item {item.Data.Name}");
        }
    }
}
