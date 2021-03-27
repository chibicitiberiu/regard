using Regard.Model;
using System;

namespace Regard.Common.API.Model
{
    public class ApiSubscription
    {
        [Flags]
        public enum Parts
        {
            None = 0,
            Config = 1,
            Stats = 2
        }

        public int Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public int? ParentFolderId { get; set; }

        public string ThumbnailUrl { get; set; }

        public ApiSubscriptionConfig Config { get; set; }

        public ApiSubscriptionStats Stats { get; set; }
    }

    public class ApiSubscriptionConfig
    {
        public bool? AutoDownload { get; set; }

        public int? DownloadMaxCount { get; set; }

        public VideoOrder? DownloadOrder { get; set; }

        public bool? AutomaticDeleteWatched { get; set; }

        public string DownloadPath { get; set; }
    }

    public class ApiSubscriptionStats
    {
        public int TotalVideoCount { get; set; }

        public int WatchedVideoCount { get; set; }

        public int DownloadedVideoCount { get; set; }

        public int DiskUsageBytes { get; set; }
    }
}
