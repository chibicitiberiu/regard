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

        public bool? IsWatched { get; set; }

        public bool? IsDownloaded { get; set; }

        public string Query { get; set; }

        public int? Limit { get; set; }

        public int? Offset { get; set; }
    }
}
