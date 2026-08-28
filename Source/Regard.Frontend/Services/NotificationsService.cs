using Regard.Common.API.Model;
using Regard.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Services
{
    /// <summary>
    /// Client-side store for the notification bell + toasts. One unified stream: MessagingService pushes
    /// <see cref="ApiNotification"/> updates (keyed by <see cref="ApiNotification.Key"/>, upserted in
    /// place so a download's row transitions running → done), seeded on startup from the backend so the
    /// bell survives a reload. Warnings/errors also pop a transient toast.
    /// </summary>
    public class NotificationsService
    {
        private const int ToastDurationMs = 6000;
        private int nextToastId = 1;
        private bool initialized = false;

        /// <summary>All notifications, newest first (ongoing + terminal).</summary>
        public ObservableCollection<ApiNotification> Notifications { get; } = new();

        /// <summary>Transient toasts (warnings/errors + explicit info), newest first.</summary>
        public ObservableCollection<ToastItem> Toasts { get; } = new();

        /// <summary>Highest notification Id the user has already seen (drives the unread dot).</summary>
        public long LastSeenId { get; private set; } = 0;

        /// <summary>Raised when the unread/activity state may have changed (bell needs a re-render).</summary>
        public event EventHandler ActivityChanged;

        /// <summary>The bell lights up while anything is in flight, or there are unseen notifications.</summary>
        public bool HasActivity => Notifications.Any(n => n.Ongoing) || Notifications.Any(n => n.Id > LastSeenId);

        public NotificationsService(MessagingService messaging)
        {
            messaging.NotificationReceived += OnNotificationReceived;
            messaging.NotificationRemoved += OnNotificationRemoved;
        }

        /// <summary>
        /// Seeds the store from the backend (persisted notifications). Idempotent + safe to call from
        /// several NavMenu instances; the caller must only invoke it once authenticated.
        /// </summary>
        public async Task InitializeAsync(BackendService backend)
        {
            if (initialized)
                return;
            initialized = true;

            try
            {
                var resp = await backend.GetRecentNotifications();
                var items = resp?.Data?.Notifications;
                if (items != null)
                    foreach (var n in items) // server returns newest-first
                        if (IndexOfKey(n.Key) < 0)
                            Notifications.Add(n);
            }
            catch
            {
                // Not signed in yet / transient failure: the live push will still populate the bell.
            }

            ActivityChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Restores the last-seen marker (from localStorage, read by the caller).</summary>
        public void SetLastSeen(long id)
        {
            LastSeenId = id;
            ActivityChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Marks everything currently shown as seen; returns the new marker to persist.</summary>
        public long MarkAllSeen()
        {
            long max = Notifications.Count > 0 ? Notifications.Max(n => n.Id) : LastSeenId;
            if (max > LastSeenId)
                LastSeenId = max;
            ActivityChanged?.Invoke(this, EventArgs.Empty);
            return LastSeenId;
        }

        private void OnNotificationReceived(object sender, ApiNotification n)
        {
            if (n == null)
                return;

            int idx = IndexOfKey(n.Key);
            if (idx >= 0)
                Notifications[idx] = n; // Replace -> CollectionChanged fires; same key = update in place
            else
                Notifications.Insert(0, n);

            if (n.Severity == ApiNotificationSeverity.Warning || n.Severity == ApiNotificationSeverity.Error)
                ShowToast(new ToastItem
                {
                    Id = nextToastId++,
                    Severity = MapSeverity(n.Severity),
                    Message = string.IsNullOrEmpty(n.Text) ? n.Title : $"{n.Title}: {n.Text}",
                });

            ActivityChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnNotificationRemoved(object sender, string key)
        {
            RemoveByKey(key);
        }

        /// <summary>Removes a notification locally (used by the removed push and optimistic dismiss).</summary>
        public void RemoveByKey(string key)
        {
            int idx = IndexOfKey(key);
            if (idx >= 0)
            {
                Notifications.RemoveAt(idx);
                ActivityChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Clears all terminal (non-ongoing) notifications locally.</summary>
        public void ClearTerminalLocal()
        {
            for (int i = Notifications.Count - 1; i >= 0; i--)
                if (!Notifications[i].Ongoing)
                    Notifications.RemoveAt(i);
            ActivityChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Pops a transient info toast (e.g. a "started" confirmation from a dialog).</summary>
        public void ShowInfo(string message)
        {
            ShowToast(new ToastItem
            {
                Id = nextToastId++,
                Severity = ApiMessageSeverity.Info,
                Message = message,
            });
        }

        private void ShowToast(ToastItem toast)
        {
            Toasts.Insert(0, toast);
            _ = DismissLater(toast);
        }

        private async Task DismissLater(ToastItem toast)
        {
            await Task.Delay(ToastDurationMs);
            Dismiss(toast);
        }

        public void Dismiss(ToastItem toast)
        {
            Toasts.Remove(toast);
        }

        private int IndexOfKey(string key)
        {
            for (int i = 0; i < Notifications.Count; i++)
                if (Notifications[i].Key == key)
                    return i;
            return -1;
        }

        private static ApiMessageSeverity MapSeverity(ApiNotificationSeverity s) => s switch
        {
            ApiNotificationSeverity.Error => ApiMessageSeverity.Error,
            ApiNotificationSeverity.Warning => ApiMessageSeverity.Warning,
            _ => ApiMessageSeverity.Info,
        };
    }

    public class ToastItem
    {
        public int Id { get; set; }
        public ApiMessageSeverity Severity { get; set; }
        public string Message { get; set; }
    }
}
