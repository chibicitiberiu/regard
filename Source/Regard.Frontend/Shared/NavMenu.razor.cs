using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Regard.Common.API.Model;
using Regard.Frontend.Services;
using Regard.Frontend.Shared.Controls;
using Regard.Services;
using Regard.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared
{
    public partial class NavMenu : IDisposable
    {
        private ElementReference notificationsLink;
        private ElementReference userLink;

        [Inject] protected NavigationManager NavigationManager { get; set; }

        [Inject] protected AuthenticationService Auth { get; set; }

        [Inject] protected NotificationsService Notifications { get; set; }

        private string username;

        [Parameter] public EventCallback LogoClicked { get; set; }

        // TODO
        private bool CanRegister { get; set; } = true;

        private bool HaveNotifications => Notifications.HasActivity;

        private bool NotificationsPanelVisible { get; set; } = false;

        private bool UserPanelVisible { get; set; } = false;

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            username = await Auth.GetUsername();

            Notifications.ActiveJobs.CollectionChanged += OnNotificationsChanged;
            Notifications.RecentMessages.CollectionChanged += OnNotificationsChanged;
        }

        private void OnNotificationsChanged(object sender, NotifyCollectionChangedEventArgs e)
            => InvokeAsync(StateHasChanged);

        private static string ProgressWidth(ApiJobInfo job)
            => job.Progress.HasValue
                ? $"width:{(int)(Math.Clamp(job.Progress.Value, 0f, 1f) * 100)}%"
                : string.Empty;

        public void Dispose()
        {
            Notifications.ActiveJobs.CollectionChanged -= OnNotificationsChanged;
            Notifications.RecentMessages.CollectionChanged -= OnNotificationsChanged;
        }

        private async Task Logout()
        {
            HideAllPanels();
            await Auth.Logout();
            // Auth.Logout()'s state-change notification targets a fresh DI scope's provider (a WASM
            // quirk the login flow works around with a forced navigation), so the app's AuthorizeView
            // doesn't otherwise re-evaluate. Force a reload to land on the login screen.
            NavigationManager.NavigateTo("/", forceLoad: true);
        }

        private void HideAllPanels()
        {
            NotificationsPanelVisible = false;
            UserPanelVisible = false;
        }

        private void ToggleNotificationsPanel()
        {
            bool visible = NotificationsPanelVisible;
            HideAllPanels();
            if (!visible)
                NotificationsPanelVisible = true;
        }

        private void ToggleUserPanel()
        {
            bool visible = UserPanelVisible;
            HideAllPanels();
            if (!visible)
                UserPanelVisible = true;
        }
    }
}
