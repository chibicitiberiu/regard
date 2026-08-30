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

        /// <summary>
        /// True when this video's downloaded file had SponsorBlock segments physically cut out
        /// (yt-dlp --sponsorblock-remove) at download time, so its timeline no longer matches the
        /// original. Recorded so the in-player Skip never applies original-timeline segments to a file
        /// that was already cut (which would misalign) — even if the subscription's SponsorBlock settings
        /// change after the download.
        /// </summary>
        public bool SponsorsRemoved { get; set; } = false;

        [MaxLength(260)]
        public string DownloadedPath { get; set; }

        public long? DownloadedSize { get; set; }

        /// <summary>
        /// When non-null, this downloaded video is scheduled for deletion at this time — a grace period
        /// after it was marked (on watch, or manually). A periodic sweep (ProcessScheduledDeletionsJob)
        /// removes the files once the time passes; "Unmark for deletion" clears it. Also cleared when the
        /// file is actually removed. During the grace window the video still counts toward the quota.
        /// </summary>
        public DateTimeOffset? DeleteScheduledAt { get; set; }

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

        /// <summary>
        /// Like count, from yt-dlp's <c>like_count</c> during enrichment, or refreshed from Return
        /// YouTube Dislike when that's enabled. Null when unknown.
        /// </summary>
        public long? Likes { get; set; }

        /// <summary>
        /// Liked ratio in 0..1 — <c>likes / (likes + dislikes)</c>, NOT a star average. Null when no
        /// dislike data is available, which is the normal case: YouTube stopped publishing dislike
        /// counts in 2021, so yt-dlp never supplies one and only Return YouTube Dislike can fill this in.
        ///
        /// Storing the ratio rather than an absolute dislike count is deliberate: when a later metadata
        /// refresh raises <see cref="Likes"/>, the implied dislikes scale with it instead of pairing a
        /// fresh like count with a frozen dislike number.
        /// </summary>
        public float? Rating { get; set; }

        public string ProviderData { get; set; }

        /// <summary>
        /// The video's chapters as a JSON array of {Start, End, Title} (original-timeline seconds),
        /// captured from yt-dlp during metadata enrichment. Null when the source has no chapters.
        /// Like SponsorSegments these are on the ORIGINAL timeline, so the watch page must not seek by
        /// them on a downloaded file that had segments cut (see <see cref="SponsorsRemoved"/>).
        /// </summary>
        public string Chapters { get; set; }

        /// <summary>
        /// Resume point: seconds into the video where playback was last left off, or null once watched /
        /// never started. Cleared when the video is marked watched (a finished video restarts from 0).
        /// </summary>
        public int? PlaybackPositionSeconds { get; set; }

        /// <summary>
        /// When <see cref="PlaybackPositionSeconds"/> was last written. Drives newer-wins reconciliation
        /// with Jellyfin's UserData.LastPlayedDate during two-way sync.
        /// </summary>
        public DateTimeOffset? PlaybackPositionUpdated { get; set; }

        public override string ToString()
        {
            return $"({SubscriptionId}:{Id}:{Name ?? OriginalUrl})";
        }
    }
}
