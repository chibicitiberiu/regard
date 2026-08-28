using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.API.Subscriptions
{
    public class VideoSimpleRequest
    {
        public int[] VideoIds { get; set; }
    }

    public class VideoDownloadRequest : VideoSimpleRequest { }

    public class VideoDeleteFilesRequest : VideoSimpleRequest { }

    public class VideoMarkWatchedRequest : VideoSimpleRequest { }

    public class VideoMarkNotWatchedRequest : VideoSimpleRequest { }

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
