using Microsoft.AspNetCore.Components;
using Regard.Frontend.Services;
using Regard.Frontend.Shared.Modals;
using Regard.Frontend.Shared.Subscription;
using Regard.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared
{
    public partial class MainLayout
    {
        private SubscriptionCreateModal subscriptionCreateModal;
        private SubscriptionCreateEmptyModal subscriptionCreateEmptyModal;
        private FolderCreateModal folderCreateModal;
        private ImportModal importModal;

        private ElementReference addButton;
        private bool isAddMenuVisible = false;

        private SubscriptionTree subscriptionTree;

        [Inject] protected AppController AppCtrl { get; set; }

        [Inject] protected AppState AppState { get; set; }

        [Inject] protected SubscriptionManagerService SubscriptionManager { get; set; }

        private void OnToolbarAddClicked()
        {
            isAddMenuVisible = true;
        }

        private async Task OnAddSubscription()
        {
            await subscriptionCreateModal?.Show();
        }

        private async Task OnAddEmptySubscription()
        {
            await subscriptionCreateEmptyModal?.Show();
        }

        private async Task OnAddFolder()
        {
            await folderCreateModal?.Show();
        }

        private async Task OnImport()
        {
            await importModal?.Show();
        }

        private async Task OnSynchronize()
        {
            await SubscriptionManager.SynchronizeAll();
        }

        private void OnLogoClicked()
        {
            subscriptionTree.DeselectAll();
        }
    }
}
