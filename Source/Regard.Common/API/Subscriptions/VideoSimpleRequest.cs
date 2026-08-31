using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.API.Subscriptions
{
    public class VideoSimpleRequest
    {
        public int[] VideoIds { get; set; }
    }

    public class VideoDownloadRequest : VideoSimpleRequest
    {
        /// <summary>
        /// "Download again": re-fetch even when the video is already marked downloaded. Set only by an
        /// explicit user action — automatic download and restart reconciliation rely on the
        /// already-downloaded no-op to stay idempotent. The job deletes the video's existing files first,
        /// so a missing or half-written download is genuinely replaced rather than skipped by yt-dlp.
        /// </summary>
        public bool Force { get; set; }
    }

    public class VideoDeleteFilesRequest : VideoSimpleRequest { }

    /// <summary>
    /// Re-run yt-dlp for an already-downloaded video's missing sidecars (subtitles), without touching
    /// the media file. Videos that aren't downloaded, or that already have every wanted language, are
    /// skipped server-side rather than rejected.
    /// </summary>
    public class VideoReprocessRequest : VideoSimpleRequest { }

    /// <summary>
    /// Re-fetch a video's metadata (views, likes, title, chapters) now, ignoring the age-based schedule
    /// the background refresh follows. Costs one throttled extraction per video, which is why there is
    /// no subscription-wide counterpart.
    /// </summary>
    public class VideoRefreshMetadataRequest : VideoSimpleRequest { }

    /// <summary>
    /// Queue a subtitle refetch for every downloaded video in a subscription that is missing one.
    /// The server decides which videos qualify; the response reports how many were queued.
    /// </summary>
    public class SubscriptionReprocessRequest
    {
        public int SubscriptionId { get; set; }
    }

    public class SubscriptionReprocessResponse
    {
        public int Queued { get; set; }

        /// <summary>Downloaded videos that were checked but already had everything they needed.</summary>
        public int AlreadyComplete { get; set; }
    }

    public class VideoMarkWatchedRequest : VideoSimpleRequest { }

    public class VideoMarkNotWatchedRequest : VideoSimpleRequest { }

    public class VideoMarkForDeletionRequest : VideoSimpleRequest { }

    public class VideoUnmarkForDeletionRequest : VideoSimpleRequest { }

    /// <summary>Report the current playback position (resume point) for a single video.</summary>
    public class VideoReportProgressRequest
    {
        public int VideoId { get; set; }

        public int PositionSeconds { get; set; }

        /// <summary>
        /// The player's known media duration in seconds, if any. Lets the backend backfill Video.Duration
        /// when it wasn't captured during metadata enrichment — without it the resume progress bar can't be
        /// drawn (it needs position/duration).
        /// </summary>
        public int? DurationSeconds { get; set; }
    }
}
