using System;

namespace Regard.Backend.Jellyfin
{
    public enum JellyfinSyncAction
    {
        /// <summary>Nothing to do — both sides already agree.</summary>
        None,
        /// <summary>Jellyfin has this played; mark the Regard video watched.</summary>
        MarkWatched,
        /// <summary>Jellyfin's resume position is newer; store it locally (with its timestamp).</summary>
        AdoptPosition,
        /// <summary>Regard is ahead; push its state to Jellyfin.</summary>
        PushToJellyfin
    }

    /// <summary>The single action to take for one matched video after comparing both sides.</summary>
    public readonly struct JellyfinSyncDecision
    {
        public JellyfinSyncAction Action { get; init; }

        // AdoptPosition payload.
        public int PositionSeconds { get; init; }
        public DateTimeOffset Timestamp { get; init; }

        // PushToJellyfin payload.
        public long PushTicks { get; init; }
        public bool PushPlayed { get; init; }

        public static readonly JellyfinSyncDecision None = new() { Action = JellyfinSyncAction.None };
        public static readonly JellyfinSyncDecision MarkWatched = new() { Action = JellyfinSyncAction.MarkWatched };
        public static JellyfinSyncDecision Adopt(int seconds, DateTimeOffset ts) =>
            new() { Action = JellyfinSyncAction.AdoptPosition, PositionSeconds = seconds, Timestamp = ts };
        public static JellyfinSyncDecision Push(long ticks, bool played) =>
            new() { Action = JellyfinSyncAction.PushToJellyfin, PushTicks = ticks, PushPlayed = played };
    }

    /// <summary>
    /// Pure two-way reconciliation for one video: given Regard's and Jellyfin's playback state, decides the
    /// single action. Extracted from <see cref="Jobs.JellyfinSyncJob"/> so the (subtle) newer-wins rules are
    /// unit-testable without a live server. All time comparisons are in UTC.
    /// </summary>
    public static class JellyfinReconciler
    {
        public static JellyfinSyncDecision Reconcile(
            bool regardWatched, int? regardPositionSeconds, DateTimeOffset? regardUpdated,
            bool jellyfinPlayed, long? jellyfinPositionTicks, DateTime? jellyfinLastPlayed)
        {
            // 1) Watched takes priority over any resume position.
            if (jellyfinPlayed)
                return regardWatched ? JellyfinSyncDecision.None : JellyfinSyncDecision.MarkWatched;
            if (regardWatched)
                return JellyfinSyncDecision.Push(0L, true);   // Regard watched, Jellyfin not

            // 2) Neither side watched: reconcile the resume position by newer-wins.
            int jfSecs = (jellyfinPositionTicks ?? 0) > 0
                ? (int)(jellyfinPositionTicks.Value / TimeSpan.TicksPerSecond) : 0;
            int rSecs = regardPositionSeconds ?? 0;
            DateTime? jfTs = jellyfinLastPlayed?.ToUniversalTime();
            DateTimeOffset? rTs = regardUpdated;

            bool jfNewer = jfTs.HasValue && (!rTs.HasValue || jfTs.Value > rTs.Value.UtcDateTime);

            if (jfSecs > 0 && jfNewer)
            {
                // Stamp Jellyfin's timestamp (not now) so the next sync doesn't see Regard as freshly-newer
                // and ping-pong the value straight back.
                return JellyfinSyncDecision.Adopt(jfSecs, new DateTimeOffset(jfTs.Value, TimeSpan.Zero));
            }
            if (rSecs > 0 && (!jfTs.HasValue || (rTs.HasValue && rTs.Value.UtcDateTime >= jfTs.Value)))
            {
                return JellyfinSyncDecision.Push((long)rSecs * TimeSpan.TicksPerSecond, false);
            }
            return JellyfinSyncDecision.None;
        }
    }
}
