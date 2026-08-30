using System.Collections.Concurrent;

namespace Regard.Backend.Services.LiveUpdates
{
    /// <summary>
    /// subscription id → owning user id. A Video has no UserId of its own (only SubscriptionId), and the
    /// Subscription navigation is null in practice because lazy-loading proxies are not active, so the
    /// change feed needs this to know who to push a video update to.
    ///
    /// Kept warm by learning from every Subscription entry the feed observes, in any state — which also
    /// covers the awkward case of a subscription and its cascading videos being deleted in one save.
    /// SQLite reuses rowids when a table has no AUTOINCREMENT, so entries are re-learned on every insert
    /// and dropped on delete to make sure an id can never carry a previous owner.
    /// </summary>
    public class SubscriptionOwnerCache
    {
        private readonly ConcurrentDictionary<int, string> owners = new ConcurrentDictionary<int, string>();

        public void Learn(int subscriptionId, string userId)
        {
            if (subscriptionId > 0 && !string.IsNullOrEmpty(userId))
                owners[subscriptionId] = userId;
        }

        public void Forget(int subscriptionId) => owners.TryRemove(subscriptionId, out _);

        public bool TryGet(int subscriptionId, out string userId)
        {
            userId = null;
            return subscriptionId > 0 && owners.TryGetValue(subscriptionId, out userId);
        }
    }
}
