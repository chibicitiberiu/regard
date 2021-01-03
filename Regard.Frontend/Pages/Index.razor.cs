using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Regard.Frontend.Shared.Controls;
using Regard.Frontend.Shared.Modals;
using Regard.Frontend.Shared.Subscription;
using Regard.Frontend.Shared.Video;
using Regard.Services;
using System.Threading.Tasks;

namespace Regard.Frontend.Pages
{
    [Authorize]
    public partial class Index
    {
        [Inject]
        MainAppController ApplicationController { get; set; }

        [Inject]
        BackendService Backend { get; set; }

        SubscriptionCreateModal subscriptionCreateModal;
        SubscriptionCreateEmptyModal subscriptionCreateEmptyModal;
        FolderCreateModal folderCreateModal;
        VideoAddModal videoAddModal;

        VideoList videoList;
        SubscriptionTree subscriptionTree;

        bool isSubscriptionSelected = false;

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            await ApplicationController.OnInitialize();
        }

        private async Task CreateSubscription()
        {
            await subscriptionCreateModal?.Show();
        }

        private async Task CreateEmptySubscription()
        {
            await subscriptionCreateEmptyModal?.Show();
        }

        private async Task CreateFolder()
        {
            await folderCreateModal?.Show();
        }

        private void Import()
        {
        }

        private async Task Refresh()
        {
            await subscriptionTree.Repopulate();
            await videoList.Populate();
        }

        private async Task SynchronizeAll()
        {
            await Backend.SubscriptionSynchronizeAll();
        }

        private async Task AddVideo()
        {
            await videoAddModal?.Show();
        }

        private async Task OnSelectedItemChanged(SubscriptionItemViewModelBase selectedItem)
        {
            if (selectedItem is SubscriptionViewModel subscriptionViewModel)
            {
                isSubscriptionSelected = true;
                videoAddModal.SelectedSubscriptionId = subscriptionViewModel.Subscription.Id;
                await videoList.SetSelectedSubscription(subscriptionViewModel.Subscription);
            }
            else if (selectedItem is SubscriptionFolderViewModel folderViewModel)
            {
                isSubscriptionSelected = false;
                await videoList.SetSelectedFolder(folderViewModel.Folder);
            }
            else if (selectedItem == null)
            {
                isSubscriptionSelected = false;
                await videoList.DeselectAll();
            }
        }
    }
}
