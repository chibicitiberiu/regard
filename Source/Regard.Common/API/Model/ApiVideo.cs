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
    }
}
