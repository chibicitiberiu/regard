using Regard.Common.API.Model;
using System.Collections.Generic;

namespace Regard.Backend.Services.LiveUpdates
{
    public enum EntityKind { Video, Subscription, SubscriptionFolder }

    public enum LiveMessageKind
    {
        VideoUpdated,
        VideosChanged,
        SubscriptionCreated,
        SubscriptionUpdated,
        SubscriptionDeleted,
        FolderCreated,
        FolderUpdated,
        FolderDeleted,
    }

    /// <summary>
    /// One outbound live update, produced inside <c>SavedChanges</c> and handed to the
    /// <see cref="LiveUpdateDispatcher"/>. Video messages may carry a null <see cref="UserId"/> when the
    /// owning subscription wasn't part of the same save and isn't cached yet; the dispatcher resolves
    /// those off the save path (see <see cref="SubscriptionId"/>).
    /// </summary>
    public class LiveMessage
    {
        public LiveMessageKind Kind { get; init; }

        /// <summary>Owner, or null when it still has to be resolved from <see cref="SubscriptionId"/>.</summary>
        public string UserId { get; init; }

        /// <summary>Owning subscription for video messages; the subscription itself for sub messages.</summary>
        public int SubscriptionId { get; init; }

        public int EntityId { get; init; }

        public ApiVideo Video { get; init; }

        public ApiSubscription Subscription { get; init; }

        public ApiSubscriptionFolder Folder { get; init; }
    }

    /// <summary>Per-<c>SaveChanges</c> capture, taken in SavingChanges and consumed in SavedChanges.</summary>
    internal class PendingBatch
    {
        public List<CapturedChange> Changes { get; } = new List<CapturedChange>();
    }

    /// <summary>
    /// A single captured entity change. State and the modified-property set can only be read during
    /// SavingChanges (afterwards AcceptAllChanges has run: everything is Unchanged, deletes are
    /// Detached, and IsModified is false everywhere), while a store-generated Id only exists after the
    /// save — hence the split capture/emit.
    /// </summary>
    internal class CapturedChange
    {
        public EntityKind Kind { get; init; }
        public bool IsAdded { get; init; }
        public bool IsDeleted { get; init; }
        public object Entity { get; init; }

        /// <summary>Owner if known at capture time (subscriptions/folders always; videos when cached).</summary>
        public string UserId { get; set; }

        /// <summary>For videos: the owning subscription id, read from the entry (never the navigation).</summary>
        public int SubscriptionId { get; init; }

        /// <summary>False when the property allowlist says this change isn't worth a push.</summary>
        public bool Pushable { get; init; }
    }
}
