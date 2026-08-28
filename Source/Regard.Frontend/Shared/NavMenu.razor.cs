using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Regard.Common.API.Model;
using Regard.Common.API.Subscriptions;
using Regard.Frontend.Services;
using Regard.Services;
using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared
{
    public partial class NavMenu : IDisposable
    {
        private const string LastSeenStorageKey = "regard.notif.lastSeen";

        private ElementReference notificationsLink;
        private ElementReference userLink;

        [Inject] protected NavigationManager NavigationManager { get; set; }

        [Inject] protected AuthenticationService Auth { get; set; }

        [Inject] protected NotificationsService Notifications { get; set; }

        [Inject] protected BackendService Backend { get; set; }

        [Inject] protected IJSRuntime JS { get; set; }

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

            Notifications.Notifications.CollectionChanged += OnNotificationsChanged;
            Notifications.ActivityChanged += OnActivityChanged;

            // NavMenu renders in both the Authorized and NotAuthorized layouts, so this runs even when
            // signed out — only seed (a request that would 401) when there's a token. Init is idempotent,
            // so two NavMenu instances calling it is safe.
            string token = null;
            try { token = await Auth.GetToken(); } catch { }
            if (!string.IsNullOrEmpty(token))
            {
                await LoadLastSeen();
                await Notifications.InitializeAsync(Backend);
            }
        }

        private void OnNotificationsChanged(object sender, NotifyCollectionChangedEventArgs e)
            => InvokeAsync(StateHasChanged);

        private void OnActivityChanged(object sender, EventArgs e)
            => InvokeAsync(StateHasChanged);

        private async Task LoadLastSeen()
        {
            try
            {
                var raw = await JS.InvokeAsync<string>("localStorage.getItem", LastSeenStorageKey);
                if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long id))
                    Notifications.SetLastSeen(id);
            }
            catch
            {
                // localStorage unavailable / malformed -> treat everything as unseen.
            }
        }

        private static string ProgressWidth(ApiNotification n)
            => n.Progress.HasValue
                ? $"width:{(int)(Math.Clamp(n.Progress.Value, 0f, 1f) * 100)}%"
                : string.Empty;

        private static bool HasPrimaryAction(ApiNotification n)
            => (n.PrimaryAction == ApiNotificationAction.OpenVideo && n.VideoId.HasValue)
            || (n.PrimaryAction == ApiNotificationAction.OpenLogs && n.JobId.HasValue);

        private void OnNotificationClick(ApiNotification n)
        {
            if (n.PrimaryAction == ApiNotificationAction.OpenVideo && n.VideoId.HasValue)
            {
                HideAllPanels();
                NavigationManager.NavigateTo($"/watch/{n.VideoId.Value}");
            }
            else if (n.PrimaryAction == ApiNotificationAction.OpenLogs && n.JobId.HasValue)
            {
                HideAllPanels();
                NavigationManager.NavigateTo($"/jobs/{n.JobId.Value}");
            }
        }

        private async Task OnRetry(ApiNotification n)
        {
            if (!n.VideoId.HasValue)
                return;
            // Re-download; drop the failed notification (a fresh "Downloading" one will arrive).
            await Backend.VideoDownload(new VideoDownloadRequest { VideoIds = new[] { n.VideoId.Value } });
            Notifications.RemoveByKey(n.Key);
            await Backend.DismissNotification(n.Id);
        }

        private async Task OnCancel(ApiNotification n)
        {
            if (n.JobId.HasValue)
                await Backend.JobCancel(n.JobId.Value);
        }

        private async Task OnDismiss(ApiNotification n)
        {
            Notifications.RemoveByKey(n.Key);
            await Backend.DismissNotification(n.Id);
        }

        private async Task OnClearAll()
        {
            Notifications.ClearTerminalLocal();
            await Backend.ClearNotifications();
        }

        public void Dispose()
        {
            Notifications.Notifications.CollectionChanged -= OnNotificationsChanged;
            Notifications.ActivityChanged -= OnActivityChanged;
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

        private async Task ToggleNotificationsPanel()
        {
            bool visible = NotificationsPanelVisible;
            HideAllPanels();
            if (!visible)
            {
                NotificationsPanelVisible = true;
                // Opening the panel marks everything read; persist the marker so it sticks across reloads.
                long id = Notifications.MarkAllSeen();
                try { await JS.InvokeVoidAsync("localStorage.setItem", LastSeenStorageKey, id.ToString(CultureInfo.InvariantCulture)); }
                catch { }
            }
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
