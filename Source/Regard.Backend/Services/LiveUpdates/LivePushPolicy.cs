using System;
using System.Collections.Generic;

namespace Regard.Backend.Services.LiveUpdates
{
    /// <summary>
    /// Decides which entity changes are worth broadcasting, and when per-entity messages collapse into
    /// a coarse one. Keeping this as data rather than as remembered call sites is the whole point of the
    /// change feed: a new mutation path is live automatically, and a high-frequency one stays quiet
    /// without anyone having to know about it.
    /// </summary>
    public static class LivePushPolicy
    {
        /// <summary>
        /// Video ENTITY property names that <c>ApiModelFactory.ToApi</c> actually projects. Deliberately
        /// keyed on the entity, not on ApiVideo: ToApi renames four of them (IsNew←Published,
        /// IsDownloaded←DownloadedPath, IsEnriched←EnrichedAt, ThumbnailUrl←ThumbnailPath), so deriving
        /// this list from the DTO would silently stop downloads from ever pushing.
        /// Adding a field to ApiVideo means adding its source property here.
        /// </summary>
        private static readonly HashSet<string> VideoProjected = new HashSet<string>(StringComparer.Ordinal)
        {
            "Name", "Description", "IsWatched", "Published", "DownloadedPath", "DownloadedSize",
            "SubscriptionId", "PlaylistIndex", "EnrichedAt", "LastUpdated", "ThumbnailPath",
            "UploaderName", "Views", "Duration", "Rating", "OriginalUrl", "SponsorsRemoved",
            "DeleteScheduledAt", "PlaybackPositionSeconds",
        };

        /// <summary>
        /// Playback telemetry: projected into ApiVideo, but written every few seconds during playback and
        /// not worth a broadcast on its own. A normal position tick writes only these two, so it stays
        /// silent; MarkWatched also writes IsWatched, so it pushes. This preserves the deliberate silence
        /// of VideoManager.SetPlaybackPosition as a property of the data.
        /// </summary>
        private static readonly HashSet<string> VideoSuppressed = new HashSet<string>(StringComparer.Ordinal)
        {
            "PlaybackPositionSeconds", "PlaybackPositionUpdated",
        };

        private static readonly HashSet<string> SubscriptionProjected = new HashSet<string>(StringComparer.Ordinal)
        {
            "Name", "Description", "ParentFolderId", "ThumbnailPath",
        };

        private static readonly HashSet<string> FolderProjected = new HashSet<string>(StringComparer.Ordinal)
        {
            "Name", "ParentId",
        };

        /// <summary>
        /// Above this many per-entity video updates for one user in a single flush, collapse to one
        /// coarse "this subscription's videos changed" message per subscription and let the client
        /// refetch — the server owns filtering/ordering/paging anyway.
        /// </summary>
        public const int VideoCollapseThreshold = 25;

        /// <summary>Quiet period before a user's pending updates are flushed.</summary>
        public static readonly TimeSpan DebounceWindow = TimeSpan.FromMilliseconds(200);

        /// <summary>
        /// Upper bound on how long a change can sit unflushed. Needed because SynchronizeJob saves once
        /// per video in a tight loop, which would re-arm a pure debounce indefinitely.
        /// </summary>
        public static readonly TimeSpan MaxDelay = TimeSpan.FromMilliseconds(1500);

        /// <summary>True if a modification touching these properties should be broadcast.</summary>
        public static bool ShouldPushModified(EntityKind kind, IEnumerable<string> modifiedProperties)
        {
            switch (kind)
            {
                case EntityKind.Video:
                    bool any = false;
                    foreach (var p in modifiedProperties)
                    {
                        if (!VideoProjected.Contains(p))
                            continue;
                        if (VideoSuppressed.Contains(p))
                            continue;
                        any = true;
                        break;
                    }
                    return any;

                case EntityKind.Subscription:
                    foreach (var p in modifiedProperties)
                        if (SubscriptionProjected.Contains(p))
                            return true;
                    return false;

                case EntityKind.SubscriptionFolder:
                    foreach (var p in modifiedProperties)
                        if (FolderProjected.Contains(p))
                            return true;
                    return false;
            }

            return false;
        }
    }
}
