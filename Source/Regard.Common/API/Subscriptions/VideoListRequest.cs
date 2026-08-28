using Regard.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.API.Subscriptions
{
    public class VideoListRequest
    {
        public int[] Ids { get; set; }

        public int? SubscriptionId { get; set; }

        public int? SubscriptionFolderId { get; set; }

        public VideoOrder Order { get; set; }

        /// <summary>
        /// Legacy tri-state watch filter (null=all, true=watched, false=unwatched). Still used by
        /// programmatic callers such as the watch-page "Up next" queue. When <see cref="WatchState"/>
        /// is set it takes precedence.
        /// </summary>
        public bool? IsWatched { get; set; }

        /// <summary>
        /// Four-way watch-state filter for the grid toolbar. Null falls back to <see cref="IsWatched"/>.
        /// </summary>
        public VideoWatchState? WatchState { get; set; }

        public bool? IsDownloaded { get; set; }

        public string Query { get; set; }

        public int? Limit { get; set; }

        public int? Offset { get; set; }
    }
}
