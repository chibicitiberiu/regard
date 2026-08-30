using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Regard.Backend.DB;
using Regard.Backend.Hubs;
using Regard.Common;
using Regard.Common.API.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Regard.Backend.Services.LiveUpdates
{
    /// <summary>
    /// Buffers live updates per user and flushes them over SignalR on a short debounce, so a burst
    /// (a sync inserting hundreds of videos, or a thumbnail job saving once per row) collapses into a
    /// handful of messages instead of a storm.
    ///
    /// Registered before AddQuartzServer so that, since hosted services stop in reverse order, this one
    /// shuts down last and can still flush what a draining job wrote.
    /// </summary>
    public class LiveUpdateDispatcher : BackgroundService
    {
        private class UserOutbox
        {
            public Dictionary<int, ApiVideo> VideoUpdates { get; } = new Dictionary<int, ApiVideo>();
            public HashSet<int> CoarseSubscriptions { get; } = new HashSet<int>();
            public Dictionary<int, (ApiSubscription Dto, bool Created)> SubUpserts { get; } = new Dictionary<int, (ApiSubscription, bool)>();
            public HashSet<int> SubDeletes { get; } = new HashSet<int>();
            public Dictionary<int, (ApiSubscriptionFolder Dto, bool Created)> FolderUpserts { get; } = new Dictionary<int, (ApiSubscriptionFolder, bool)>();
            public HashSet<int> FolderDeletes { get; } = new HashSet<int>();

            public DateTime FirstDirtyUtc { get; set; }
            public DateTime LastDirtyUtc { get; set; }

            public bool IsEmpty => VideoUpdates.Count == 0 && CoarseSubscriptions.Count == 0
                && SubUpserts.Count == 0 && SubDeletes.Count == 0
                && FolderUpserts.Count == 0 && FolderDeletes.Count == 0;
        }

        private readonly IHubContext<MessagingHub, IMessagingClient> hub;
        private readonly IServiceScopeFactory scopeFactory;
        private readonly SubscriptionOwnerCache owners;
        private readonly ILogger<LiveUpdateDispatcher> log;

        private readonly object gate = new object();
        private readonly Dictionary<string, UserOutbox> outboxes = new Dictionary<string, UserOutbox>();

        // Video messages whose owning subscription wasn't in the same save and isn't cached yet. Resolved
        // on the timer loop with a fresh scope, deliberately off the SaveChanges path.
        private readonly ConcurrentQueue<LiveMessage> unresolved = new ConcurrentQueue<LiveMessage>();

        public LiveUpdateDispatcher(IHubContext<MessagingHub, IMessagingClient> hub,
                                    IServiceScopeFactory scopeFactory,
                                    SubscriptionOwnerCache owners,
                                    ILogger<LiveUpdateDispatcher> log)
        {
            this.hub = hub;
            this.scopeFactory = scopeFactory;
            this.owners = owners;
            this.log = log;
        }

        /// <summary>
        /// Accepts messages from the change feed. Dictionary work under a lock only — never any I/O and
        /// never an await on the hub, because this runs inside SaveChanges and must not couple database
        /// latency (or a SignalR failure) to the transaction.
        /// </summary>
        public void Post(IReadOnlyList<LiveMessage> messages)
        {
            try
            {
                foreach (var m in messages)
                {
                    if (m.UserId == null)
                    {
                        if (m.SubscriptionId > 0 && owners.TryGet(m.SubscriptionId, out var known))
                            Merge(known, m);
                        else
                            unresolved.Enqueue(m);
                        continue;
                    }

                    Merge(m.UserId, m);
                }
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Live updates: failed to queue a batch.");
            }
        }

        private void Merge(string userId, LiveMessage m)
        {
            lock (gate)
            {
                if (!outboxes.TryGetValue(userId, out var box))
                {
                    box = new UserOutbox { FirstDirtyUtc = DateTime.UtcNow };
                    outboxes[userId] = box;
                }

                switch (m.Kind)
                {
                    case LiveMessageKind.VideoUpdated:
                        // A later DTO for the same video simply wins.
                        if (!box.CoarseSubscriptions.Contains(m.SubscriptionId))
                            box.VideoUpdates[m.EntityId] = m.Video;
                        break;

                    case LiveMessageKind.VideosChanged:
                        box.CoarseSubscriptions.Add(m.SubscriptionId);
                        // The coarse message makes the client refetch that subscription anyway.
                        foreach (var id in box.VideoUpdates
                                     .Where(kv => kv.Value != null && kv.Value.SubscriptionId == m.SubscriptionId)
                                     .Select(kv => kv.Key).ToList())
                            box.VideoUpdates.Remove(id);
                        break;

                    case LiveMessageKind.SubscriptionCreated:
                    case LiveMessageKind.SubscriptionUpdated:
                        box.SubDeletes.Remove(m.EntityId);
                        box.SubUpserts[m.EntityId] = (m.Subscription,
                            m.Kind == LiveMessageKind.SubscriptionCreated
                            || (box.SubUpserts.TryGetValue(m.EntityId, out var prev) && prev.Created));
                        break;

                    case LiveMessageKind.SubscriptionDeleted:
                        box.SubUpserts.Remove(m.EntityId);
                        box.SubDeletes.Add(m.EntityId);
                        break;

                    case LiveMessageKind.FolderCreated:
                    case LiveMessageKind.FolderUpdated:
                        box.FolderDeletes.Remove(m.EntityId);
                        box.FolderUpserts[m.EntityId] = (m.Folder,
                            m.Kind == LiveMessageKind.FolderCreated
                            || (box.FolderUpserts.TryGetValue(m.EntityId, out var prevF) && prevF.Created));
                        break;

                    case LiveMessageKind.FolderDeleted:
                        box.FolderUpserts.Remove(m.EntityId);
                        box.FolderDeletes.Add(m.EntityId);
                        break;
                }

                box.LastDirtyUtc = DateTime.UtcNow;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // One loop over a small dictionary rather than a timer per user.
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await ResolveOwnersAsync();
                    await FlushDueAsync(force: false);
                }
            }
            catch (OperationCanceledException)
            {
                // shutting down
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);
            try
            {
                await ResolveOwnersAsync();
                await FlushDueAsync(force: true);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Live updates: final flush failed.");
            }
        }

        private async Task ResolveOwnersAsync()
        {
            if (unresolved.IsEmpty)
                return;

            var pending = new List<LiveMessage>();
            while (unresolved.TryDequeue(out var m))
                pending.Add(m);

            var needed = pending.Select(p => p.SubscriptionId)
                                .Where(id => id > 0 && !owners.TryGet(id, out _))
                                .Distinct()
                                .ToArray();

            if (needed.Length > 0)
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<DataContext>();
                    var found = await db.Subscriptions.AsNoTracking()
                        .Where(s => needed.Contains(s.Id))
                        .Select(s => new { s.Id, s.UserId })
                        .ToListAsync();

                    foreach (var f in found)
                        owners.Learn(f.Id, f.UserId);
                }
                catch (Exception ex)
                {
                    log.LogWarning(ex, "Live updates: could not resolve subscription owners.");
                }
            }

            foreach (var m in pending)
            {
                if (owners.TryGet(m.SubscriptionId, out var userId))
                    Merge(userId, m);
                else
                    log.LogWarning("Live updates: dropping a {0} for subscription {1} — owner unresolved.",
                        m.Kind, m.SubscriptionId);   // never broadcast what we can't attribute
            }
        }

        private async Task FlushDueAsync(bool force)
        {
            List<KeyValuePair<string, UserOutbox>> due = null;
            var now = DateTime.UtcNow;

            lock (gate)
            {
                foreach (var kv in outboxes)
                {
                    if (kv.Value.IsEmpty)
                        continue;
                    if (force
                        || now - kv.Value.LastDirtyUtc >= LivePushPolicy.DebounceWindow
                        || now - kv.Value.FirstDirtyUtc >= LivePushPolicy.MaxDelay)
                        (due ??= new List<KeyValuePair<string, UserOutbox>>()).Add(kv);
                }

                if (due != null)
                    foreach (var kv in due)
                        outboxes.Remove(kv.Key);
            }

            if (due == null)
                return;

            foreach (var (userId, box) in due)
            {
                try
                {
                    await SendAsync(userId, box);
                }
                catch (Exception ex)
                {
                    // A disconnected client is a silent no-op in SignalR; anything else just gets logged.
                    log.LogWarning(ex, "Live updates: send to user {0} failed.", userId);
                }
            }
        }

        private async Task SendAsync(string userId, UserOutbox box)
        {
            var client = hub.Clients.User(userId);

            // Folders before subscriptions before videos, so the client's tree never references a parent
            // it hasn't been told about yet.
            foreach (var kv in box.FolderUpserts)
            {
                if (kv.Value.Created)
                    await client.NotifySubscriptionFolderCreated(kv.Value.Dto);
                else
                    await client.NotifySubscriptionFolderUpdated(kv.Value.Dto);
            }

            foreach (var kv in box.SubUpserts)
            {
                if (kv.Value.Created)
                    await client.NotifySubscriptionCreated(kv.Value.Dto);
                else
                    await client.NotifySubscriptionUpdated(kv.Value.Dto);
            }

            if (box.FolderDeletes.Count > 0)
                await client.NotifySubscriptionFoldersDeleted(box.FolderDeletes.ToArray());

            if (box.SubDeletes.Count > 0)
                await client.NotifySubscriptionsDeleted(box.SubDeletes.ToArray());

            // Above the threshold, per-video updates stop being worth their bytes: collapse to one coarse
            // message per subscription and let the client refetch (the server owns filter/order/paging).
            if (box.VideoUpdates.Count > LivePushPolicy.VideoCollapseThreshold)
            {
                foreach (var subId in box.VideoUpdates.Values.Where(v => v != null).Select(v => v.SubscriptionId).Distinct())
                    box.CoarseSubscriptions.Add(subId);
                box.VideoUpdates.Clear();
            }

            foreach (var video in box.VideoUpdates.Values)
                await client.NotifyVideoUpdated(video);

            foreach (var subId in box.CoarseSubscriptions)
                await client.NotifyVideosChanged(subId);
        }
    }
}
