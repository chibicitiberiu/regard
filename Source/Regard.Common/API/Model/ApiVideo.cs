using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.API.Model
{
    public class ApiVideo
    {
        public int Id { get; set; }

        public string Name { get; set; }
        
        public string Description { get; set; }

        public bool IsWatched { get; set; }

        public bool IsNew { get; set; }

        public bool IsDownloaded { get; set; }

        public long? DownloadedSize { get; set; }

        public string StreamMimeType { get; set; }

        public int SubscriptionId { get; set; }

        public int PlaylistIndex { get; set; }

        public DateTimeOffset Published { get; set; }

        /// <summary>
        /// False while the video is a flat listing entry awaiting full metadata. The UI hides the
        /// published date (a placeholder until enrichment) and can show a "pending" hint.
        /// </summary>
        public bool IsEnriched { get; set; }

        public DateTimeOffset LastUpdated { get; set; }

        public Uri ThumbnailUrl { get; set; }
        
        public string UploaderName { get; set; }

        public ulong? Views { get; set; }

        public int? Duration { get; set; }

        public float? Rating { get; set; }

        /// <summary>The video's canonical page URL on its source site (yt-dlp webpage_url).</summary>
        public string OriginalUrl { get; set; }

        /// <summary>
        /// An embeddable player URL, set only when the user allows embedding AND the source host is
        /// embeddable (YouTube today). Null otherwise — the watch page then shows the download /
        /// watch-on-site placeholder instead of an embedded player.
        /// </summary>
        public string EmbedUrl { get; set; }

        /// <summary>
        /// True when this video's downloaded file had SponsorBlock segments cut out (its timeline no longer
        /// matches the original), so the player must not apply the original-timeline SponsorSegments below.
        /// </summary>
        public bool SponsorsRemoved { get; set; }

        /// <summary>
        /// SponsorBlock segments to skip during playback (original-timeline seconds). Set only for the
        /// single-video watch fetch of a YouTube video whose subscription has a "skip" category and whose
        /// file wasn't cut. Null/empty otherwise.
        /// </summary>
        public System.Collections.Generic.List<ApiSponsorSegment> SponsorSegments { get; set; }

        /// <summary>
        /// Real like/dislike estimates from ReturnYouTubeDislike, set only for the single-video watch fetch
        /// of a YouTube video when the feature is enabled. Null when disabled/unavailable. YouTube stopped
        /// exposing public dislikes in 2021, so these come from RYD, not yt-dlp.
        /// </summary>
        public long? Likes { get; set; }

        public long? Dislikes { get; set; }

        /// <summary>
        /// The video's chapters (original-timeline seconds), set only for the single-video watch fetch.
        /// Null/empty otherwise. Not applied for seeking on a downloaded file that had SponsorBlock
        /// segments cut (<see cref="SponsorsRemoved"/> true) — the timeline no longer matches.
        /// </summary>
        public System.Collections.Generic.List<ApiChapter> Chapters { get; set; }

        /// <summary>
        /// Resume point in whole seconds, or null when the video is watched / not yet started. Present on
        /// list items too so thumbnails can draw a progress bar.
        /// </summary>
        public int? PlaybackPositionSeconds { get; set; }

        /// <summary>
        /// When non-null, this downloaded video is marked for deletion and its files will be removed at
        /// this time (a grace period). The listing shows a trash badge with a countdown; "Unmark for
        /// deletion" clears it. Null = not marked.
        /// </summary>
        public DateTimeOffset? DeleteScheduledAt { get; set; }
    }
}
