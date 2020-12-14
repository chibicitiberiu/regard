using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Regard.Frontend.Shared.Controls;
using Regard.Frontend.Shared.Modals;
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

        Modal folderCreateModal { get; set; }

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
    }
}
