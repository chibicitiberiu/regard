using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Regard.Backend.Model
{
    public class Video
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        
        [Required, MaxLength(2048)]
        public string OriginalUrl { get; set; }

        /// <summary>
        /// Video ID as defined by the subscription provider
        /// </summary>
        [MaxLength(60)]
        public string SubscriptionProviderId { get; set; }

        /// <summary>
        /// Provider ID
        /// </summary>
        [Required, MaxLength(60)]
        public string VideoProviderId { get; set; }

        /// <summary>
        /// Video ID as defined by the video provider
        /// </summary>
        [Required, MaxLength(60)]
        public string VideoId { get; set; }

        [Required, MaxLength(250)]
        public string Name { get; set; }

        [MaxLength(4096)]
        public string Description { get; set; }

        public bool IsWatched { get; set; } = false;

        /// <summary>
        /// Set when the user cancels an in-progress download: the video is excluded from
        /// auto-download (so the next-newest takes its slot) but stays visible and can still be
        /// downloaded manually. Cleared when a manual download is requested.
        /// </summary>
        public bool DownloadSkipped { get; set; } = false;

        [MaxLength(260)]
        public string DownloadedPath { get; set; }

        public long? DownloadedSize { get; set; }

        public int SubscriptionId { get; set; }
        //subscription = models.ForeignKey(Subscription, on_delete=models.CASCADE)

        public Subscription Subscription { get; set; }

        public int PlaylistIndex { get; set; } = 0;


        public DateTimeOffset Published { get; set; }

        /// <summary>
        /// When full metadata (duration, description, real published date, rating) was fetched, or null
        /// if this video is still a flat listing entry awaiting enrichment. Set the first time the video
        /// is enriched (during sync for the newest videos, lazily on watch/download for the rest).
        /// </summary>
        public DateTimeOffset? EnrichedAt { get; set; }

        public DateTimeOffset LastUpdated { get; set; }

        public DateTimeOffset Discovered { get; set; }

        [MaxLength(2048)]
        public string ThumbnailPath { get; set; }

        public string UploaderName { get; set; }

        public ulong? Views { get; set; }

        /// <summary>Video length in whole seconds (from yt-dlp), or null if unknown.</summary>
        public int? Duration { get; set; }

        public float? Rating { get; set; }

        public string ProviderData { get; set; }

        public override string ToString()
        {
            return $"({SubscriptionId}:{Id}:{Name ?? OriginalUrl})";
        }
    }
}
