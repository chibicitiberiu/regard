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
        MainAppController appCtrl { get; set; }

        SubscriptionCreateModal subscriptionCreateModal { get; set; }

        FolderCreateModal folderCreateModal { get; set; }

        private VideoList videoList;

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            await appCtrl.OnInitialize();
        }

        private async Task CreateSubscription()
        {
            await subscriptionCreateModal?.Show();
        }

        private async Task CreateFolder()
        {
            await folderCreateModal?.Show();
        }

        private void Import()
        {
        }

        private async Task OnSelectedItemChanged(ISubscriptionItemViewModel selectedItem)
        {
            if (selectedItem is SubscriptionViewModel subscriptionViewModel)
                await videoList.SetSelectedSubscription(subscriptionViewModel.Subscription);
            else if (selectedItem is SubscriptionFolderViewModel folderViewModel)
                await videoList.SetSelectedFolder(folderViewModel.Folder);
        }
    }
}
