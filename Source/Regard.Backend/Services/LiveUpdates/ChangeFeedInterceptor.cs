using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Regard.Backend.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Regard.Backend.Services.LiveUpdates
{
    /// <summary>
    /// Broadcasts entity changes to the owning user's clients by hooking the one place every write in
    /// the app converges: SaveChanges. Jobs bypass the managers and write the DbContext directly, so an
    /// event-based bridge would keep missing sites; this makes liveness a property of persisting a
    /// change rather than something each call site has to remember.
    ///
    /// Capture happens in SavingChanges because that is the only point where entity state and the
    /// modified-property set are readable (afterwards AcceptAllChanges has run). Emit happens in
    /// SavedChanges, after the write committed, because store-generated ids only exist then — and
    /// because nothing should be announced for a save that rolled back.
    /// </summary>
    public class ChangeFeedInterceptor : SaveChangesInterceptor
    {
        // ConditionalWeakTable, not a dictionary: SavingChanges runs before SaveChanges' own try/catch,
        // so a capture that threw would otherwise strand an entry holding the context and every captured
        // entity forever (UserLogger keeps one DataContext for the whole process lifetime).
        private readonly ConditionalWeakTable<DbContext, PendingBatch> pending = new ConditionalWeakTable<DbContext, PendingBatch>();

        private readonly SubscriptionOwnerCache owners;
        private readonly LiveUpdateDispatcher dispatcher;
        private readonly ApiModelFactory modelFactory;
        private readonly ILogger<ChangeFeedInterceptor> log;

        public ChangeFeedInterceptor(SubscriptionOwnerCache owners,
                                     LiveUpdateDispatcher dispatcher,
                                     ApiModelFactory modelFactory,
                                     ILogger<ChangeFeedInterceptor> log)
        {
            this.owners = owners;
            this.dispatcher = dispatcher;
            this.modelFactory = modelFactory;
            this.log = log;
        }

        // Both the sync and async pairs are needed: SubscriptionManager uses SaveChanges() throughout
        // while the jobs use SaveChangesAsync.

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Capture(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Capture(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            Emit(eventData.Context);
            return base.SavedChanges(eventData, result);
        }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            Emit(eventData.Context);
            return base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        public override void SaveChangesFailed(DbContextErrorEventData eventData)
        {
            Discard(eventData.Context);
            base.SaveChangesFailed(eventData);
        }

        public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            Discard(eventData.Context);
            return base.SaveChangesFailedAsync(eventData, cancellationToken);
        }

        public override void SaveChangesCanceled(DbContextEventData eventData)
        {
            Discard(eventData.Context);
            base.SaveChangesCanceled(eventData);
        }

        public override Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
        {
            Discard(eventData.Context);
            return base.SaveChangesCanceledAsync(eventData, cancellationToken);
        }

        private void Discard(DbContext context)
        {
            if (context != null)
                pending.Remove(context);
        }

        /// <summary>
        /// Snapshot the interesting changes. Overwrites any previous capture for this context rather than
        /// appending, which makes SQLiteDataContext's SQLITE_BUSY retry loop (it re-invokes the whole
        /// pipeline) idempotent instead of double-counting.
        /// </summary>
        private void Capture(DbContext context)
        {
            if (context == null)
                return;

            pending.Remove(context);

            try
            {
                // ChangeTracker.Entries() forces DetectChanges, which SaveChanges then repeats, so bail
                // out as early as possible: most saves in this app are Notifications, JobInfo, options
                // and Identity rows, none of which the feed cares about.
                List<CapturedChange> changes = null;

                foreach (var entry in context.ChangeTracker.Entries())
                {
                    bool added = entry.State == EntityState.Added;
                    bool deleted = entry.State == EntityState.Deleted;
                    bool modified = entry.State == EntityState.Modified;
                    if (!added && !deleted && !modified)
                        continue;

                    EntityKind kind;
                    int subscriptionId = 0;
                    string userId = null;

                    switch (entry.Entity)
                    {
                        case Video video:
                            kind = EntityKind.Video;
                            // Read the FK off the entry, never video.Subscription: lazy-loading proxies
                            // are not active, so the navigation is simply null.
                            var fk = entry.Property(nameof(Video.SubscriptionId));
                            subscriptionId = Convert.ToInt32(deleted ? fk.OriginalValue : fk.CurrentValue);
                            break;

                        case Subscription sub:
                            kind = EntityKind.Subscription;
                            userId = sub.UserId;
                            subscriptionId = sub.Id;
                            break;

                        case SubscriptionFolder folder:
                            kind = EntityKind.SubscriptionFolder;
                            userId = folder.UserId;
                            break;

                        default:
                            continue;   // JobInfo, Notification, options, Identity, ... not our business
                    }

                    bool pushable = added || deleted
                        || LivePushPolicy.ShouldPushModified(kind, entry.Properties
                            .Where(p => p.IsModified)
                            .Select(p => p.Metadata.Name));

                    (changes ??= new List<CapturedChange>()).Add(new CapturedChange
                    {
                        Kind = kind,
                        IsAdded = added,
                        IsDeleted = deleted,
                        Entity = entry.Entity,
                        UserId = userId,
                        SubscriptionId = subscriptionId,
                        Pushable = pushable,
                    });
                }

                if (changes == null)
                    return;

                // Learn owners from every subscription in the batch before resolving videos, so a
                // cascade (subscription + its videos in one save) still resolves after the rows go.
                foreach (var c in changes)
                {
                    if (c.Kind == EntityKind.Subscription && c.Entity is Subscription s)
                    {
                        if (c.IsDeleted)
                            owners.Forget(s.Id);
                        else
                            owners.Learn(s.Id, s.UserId);
                    }
                }

                foreach (var c in changes)
                {
                    if (c.Kind == EntityKind.Video && c.UserId == null
                        && owners.TryGet(c.SubscriptionId, out var ownerId))
                        c.UserId = ownerId;
                }

                var batch = new PendingBatch();
                batch.Changes.AddRange(changes);
                pending.Add(context, batch);
            }
            catch (Exception ex)
            {
                // Never let the feed break a write.
                log.LogWarning(ex, "Live change feed: capture failed; this save will not be broadcast.");
            }
        }

        /// <summary>Turns the committed capture into messages and hands them to the dispatcher.</summary>
        private void Emit(DbContext context)
        {
            if (context == null || !pending.TryGetValue(context, out var batch))
                return;

            pending.Remove(context);

            try
            {
                var messages = new List<LiveMessage>();

                foreach (var c in batch.Changes)
                {
                    if (!c.Pushable)
                        continue;

                    switch (c.Kind)
                    {
                        case EntityKind.Video:
                            var video = (Video)c.Entity;
                            if (c.IsAdded || c.IsDeleted)
                            {
                                // A new video can't be slotted into a server-filtered, server-ordered,
                                // server-paged list from the client, so never send it per-entity — one
                                // coarse message per subscription lets the client refetch. (Deletes reach
                                // us only in the rare non-cascade case; same treatment.)
                                messages.Add(new LiveMessage
                                {
                                    Kind = LiveMessageKind.VideosChanged,
                                    UserId = c.UserId,
                                    SubscriptionId = c.SubscriptionId,
                                });
                            }
                            else
                            {
                                messages.Add(new LiveMessage
                                {
                                    Kind = LiveMessageKind.VideoUpdated,
                                    UserId = c.UserId,
                                    SubscriptionId = c.SubscriptionId,
                                    EntityId = video.Id,
                                    // Built here, inside the save, where the entity is stable — not at
                                    // flush time, when a job thread may already be mutating it.
                                    Video = modelFactory.ToApi(video),
                                });
                            }
                            break;

                        case EntityKind.Subscription:
                            var sub = (Subscription)c.Entity;
                            messages.Add(new LiveMessage
                            {
                                Kind = c.IsDeleted ? LiveMessageKind.SubscriptionDeleted
                                     : c.IsAdded ? LiveMessageKind.SubscriptionCreated
                                                 : LiveMessageKind.SubscriptionUpdated,
                                UserId = c.UserId,
                                SubscriptionId = sub.Id,
                                EntityId = sub.Id,
                                Subscription = c.IsDeleted ? null : modelFactory.ToApi(sub),
                            });
                            break;

                        case EntityKind.SubscriptionFolder:
                            var folder = (SubscriptionFolder)c.Entity;
                            messages.Add(new LiveMessage
                            {
                                Kind = c.IsDeleted ? LiveMessageKind.FolderDeleted
                                     : c.IsAdded ? LiveMessageKind.FolderCreated
                                                 : LiveMessageKind.FolderUpdated,
                                UserId = c.UserId,
                                EntityId = folder.Id,
                                Folder = c.IsDeleted ? null : modelFactory.ToApi(folder),
                            });
                            break;
                    }
                }

                if (messages.Count > 0)
                    dispatcher.Post(messages);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Live change feed: emit failed; this save will not be broadcast.");
            }
        }
    }
}
