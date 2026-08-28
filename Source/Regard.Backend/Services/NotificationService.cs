using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Regard.Backend.Common.Model;
using Regard.Backend.DB;
using Regard.Backend.Hubs;
using Regard.Common;
using Regard.Common.API.Model;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    /// <summary>
    /// The single entry point for user-facing notifications (the bell). Persists each notification —
    /// upserting by (UserId, Key) so a background update replaces the same row — and pushes it over
    /// SignalR. Jobs and one-off events both post here, unifying the old split of live job updates and
    /// discrete messages into one stream. Singleton: uses IServiceScopeFactory for the DataContext and
    /// an injected (singleton) IHubContext.
    /// </summary>
    public class NotificationService
    {
        private readonly IServiceScopeFactory scopeFactory;
        private readonly IHubContext<MessagingHub, IMessagingClient> hub;
        private readonly ILogger<NotificationService> log;

        public NotificationService(IServiceScopeFactory scopeFactory,
                                   IHubContext<MessagingHub, IMessagingClient> hub,
                                   ILogger<NotificationService> log)
        {
            this.scopeFactory = scopeFactory;
            this.hub = hub;
            this.log = log;
        }

        /// <summary>
        /// Creates or updates the notification with key <paramref name="key"/> for the given user, then
        /// pushes it live. Ownerless notifications (userId == null) go to all clients.
        /// </summary>
        public async Task PostOrUpdate(string userId, string key,
            string title, string text, NotificationSeverity severity,
            float? progress, bool ongoing,
            int? videoDbId = null, long? jobId = null,
            NotificationPrimaryAction primaryAction = NotificationPrimaryAction.None,
            bool cancellable = false)
        {
            ApiNotification dto;
            try
            {
                using var scope = scopeFactory.CreateScope();
                using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

                var n = db.Notifications.FirstOrDefault(x => x.Key == key && x.UserId == userId);
                if (n == null)
                {
                    n = new Notification { Key = key, UserId = userId };
                    db.Notifications.Add(n);
                }

                n.Timestamp = DateTimeOffset.UtcNow;
                n.Title = title;
                n.Text = text;
                n.Severity = severity;
                n.Progress = progress;
                n.Ongoing = ongoing;
                n.VideoDbId = videoDbId;
                n.JobId = jobId;
                n.PrimaryAction = primaryAction;
                n.Cancellable = cancellable;

                db.SaveChanges();
                dto = ToApi(n);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to persist notification (key={0})", key);
                return;
            }

            await Send(userId, c => c.NotifyNotification(dto), "notification");
        }

        /// <summary>Removes the notification with the given key and tells clients to drop it.</summary>
        public async Task Remove(string userId, string key)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

                var rows = db.Notifications.Where(x => x.Key == key && x.UserId == userId).ToList();
                if (rows.Count > 0)
                {
                    db.Notifications.RemoveRange(rows);
                    db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to remove notification (key={0})", key);
            }

            await Send(userId, c => c.NotifyNotificationRemoved(key), "notification-removed");
        }

        /// <summary>
        /// Recent notifications visible to the user (their own + ownerless; admins see all), newest
        /// first. Scalar projection only — never dereference the lazy User nav.
        /// </summary>
        public ApiNotification[] GetRecent(string userId, bool isAdmin, int take)
        {
            using var scope = scopeFactory.CreateScope();
            using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

            IQueryable<Notification> q = db.Notifications;
            if (!isAdmin)
                q = q.Where(n => n.UserId == userId || n.UserId == null);

            // Order by Id (monotonic with creation) — SQLite can't ORDER BY on DateTimeOffset.
            return q.OrderByDescending(n => n.Id)
                .Take(take)
                .Select(n => new ApiNotification
                {
                    Id = n.Id,
                    Key = n.Key,
                    Timestamp = n.Timestamp,
                    Title = n.Title,
                    Text = n.Text,
                    Severity = (ApiNotificationSeverity)(int)n.Severity,
                    Progress = n.Progress,
                    Ongoing = n.Ongoing,
                    VideoId = n.VideoDbId,
                    JobId = n.JobId,
                    PrimaryAction = (ApiNotificationAction)(int)n.PrimaryAction,
                    Cancellable = n.Cancellable,
                })
                .ToArray();
        }

        /// <summary>Dismisses one notification (and tells clients to drop it). Returns false if not found / not visible.</summary>
        public async Task<bool> Dismiss(long id, string userId, bool isAdmin)
        {
            string key;
            string owner;
            try
            {
                using var scope = scopeFactory.CreateScope();
                using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

                var n = db.Notifications.FirstOrDefault(x => x.Id == id);
                if (n == null)
                    return false;
                if (!isAdmin && n.UserId != userId && n.UserId != null)
                    return false;

                key = n.Key;
                owner = n.UserId;
                db.Notifications.Remove(n);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to dismiss notification {0}", id);
                return false;
            }

            await Send(owner, c => c.NotifyNotificationRemoved(key), "notification-removed");
            return true;
        }

        /// <summary>Clears all terminal (non-ongoing) notifications visible to the user.</summary>
        public int ClearAll(string userId, bool isAdmin)
        {
            using var scope = scopeFactory.CreateScope();
            using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

            IQueryable<Notification> q = db.Notifications.Where(n => !n.Ongoing);
            if (!isAdmin)
                q = q.Where(n => n.UserId == userId || n.UserId == null);

            var rows = q.ToList();
            if (rows.Count == 0)
                return 0;

            db.Notifications.RemoveRange(rows);
            db.SaveChanges();
            return rows.Count;
        }

        /// <summary>
        /// Deletes leftover Ongoing notifications at startup: nothing is running yet, so any that
        /// survived a crash mid-job are stale. Keeps the "in progress" indicator honest after a restart.
        /// </summary>
        public int ClearStaleOngoing()
        {
            using var scope = scopeFactory.CreateScope();
            using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

            var rows = db.Notifications.Where(n => n.Ongoing).ToList();
            if (rows.Count == 0)
                return 0;

            db.Notifications.RemoveRange(rows);
            db.SaveChanges();
            return rows.Count;
        }

        /// <summary>
        /// Ages out old notifications. Kept shorter than the Job Log retention, so a failed download's
        /// captured log stays inspectable in the Job Log after its bell notification is gone.
        /// </summary>
        public int PruneOld(int retentionDays)
        {
            if (retentionDays <= 0)
                return 0;

            using var scope = scopeFactory.CreateScope();
            using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

            var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);
            // Compare DateTimeOffset client-side — the SQLite provider can't translate it.
            var stale = db.Notifications.AsEnumerable().Where(n => n.Timestamp < cutoff).ToList();
            if (stale.Count == 0)
                return 0;

            db.Notifications.RemoveRange(stale);
            db.SaveChanges();
            return stale.Count;
        }

        private async Task Send(string userId, Func<IMessagingClient, Task> send, string what)
        {
            try
            {
                var target = userId != null ? hub.Clients.User(userId) : hub.Clients.All;
                await send(target);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to push {0} over SignalR", what);
            }
        }

        private static ApiNotification ToApi(Notification n) => new ApiNotification
        {
            Id = n.Id,
            Key = n.Key,
            Timestamp = n.Timestamp,
            Title = n.Title,
            Text = n.Text,
            Severity = (ApiNotificationSeverity)(int)n.Severity,
            Progress = n.Progress,
            Ongoing = n.Ongoing,
            VideoId = n.VideoDbId,
            JobId = n.JobId,
            PrimaryAction = (ApiNotificationAction)(int)n.PrimaryAction,
            Cancellable = n.Cancellable,
        };
    }
}
