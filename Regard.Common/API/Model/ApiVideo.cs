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

        public string DownloadedStreamUrl { get; set; }

        public int? DownloadedSize { get; set; }

        public int SubscriptionId { get; set; }

        public int PlaylistIndex { get; set; }

        public DateTimeOffset Published { get; set; }

        public DateTimeOffset LastUpdated { get; set; }

        public string ThumbnailUrl { get; set; }
        
        public string UploaderName { get; set; }

        public int? Views { get; set; }

        public float? Rating { get; set; }
    }
}
