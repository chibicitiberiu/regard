using Regard.Common.API.Model;
using Regard.Frontend.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Services
{
    /// <summary>
    /// Client-side store for the notification bell + toasts. Fed by MessagingService hub events:
    /// live job updates populate <see cref="ActiveJobs"/>, and user messages populate
    /// <see cref="RecentMessages"/> (warnings/errors also pop a transient toast).
    /// </summary>
    public class NotificationsService
    {
        private const int MaxRecentMessages = 50;
        private const int ToastDurationMs = 6000;

        private int nextToastId = 1;

        /// <summary>Jobs currently in flight (created/scheduled/running), keyed by Id.</summary>
        public ObservableCollection<ApiJobInfo> ActiveJobs { get; } = new();

        /// <summary>Recent user-facing messages, newest first.</summary>
        public ObservableCollection<ApiMessage> RecentMessages { get; } = new();

        /// <summary>Transient toasts (warnings/errors), newest first.</summary>
        public ObservableCollection<ToastItem> Toasts { get; } = new();

        public bool HasActivity => ActiveJobs.Count > 0 || RecentMessages.Count > 0;

        public NotificationsService(MessagingService messaging)
        {
            messaging.JobUpdated += OnJobUpdated;
            messaging.MessageReceived += OnMessageReceived;
        }

        private void OnJobUpdated(object sender, ApiJobInfo job)
        {
            if (job == null)
                return;

            int idx = IndexOfJob(job.Id);

            // A finished job leaves the active list (its failure, if any, already arrives as a message).
            if (job.State == ApiJobState.Completed || job.State == ApiJobState.Failed)
            {
                if (idx >= 0)
                    ActiveJobs.RemoveAt(idx);
                return;
            }

            if (idx >= 0)
                ActiveJobs[idx] = job;   // Replace -> CollectionChanged fires, UI refreshes
            else
                ActiveJobs.Add(job);
        }

        private void OnMessageReceived(object sender, ApiMessage message)
        {
            if (message == null)
                return;

            RecentMessages.Insert(0, message);
            while (RecentMessages.Count > MaxRecentMessages)
                RecentMessages.RemoveAt(RecentMessages.Count - 1);

            if (message.Severity == ApiMessageSeverity.Warning || message.Severity == ApiMessageSeverity.Error)
                ShowToast(new ToastItem
                {
                    Id = nextToastId++,
                    Severity = message.Severity,
                    Message = message.Message,
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

        private int IndexOfJob(long id)
        {
            for (int i = 0; i < ActiveJobs.Count; i++)
                if (ActiveJobs[i].Id == id)
                    return i;
            return -1;
        }
    }

    public class ToastItem
    {
        public int Id { get; set; }
        public ApiMessageSeverity Severity { get; set; }
        public string Message { get; set; }
    }
}
