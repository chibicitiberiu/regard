using Regard.Model;
using System;
using System.Collections.Generic;

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

        public Uri ThumbnailUrl { get; set; }

        public ApiSubscriptionConfig Config { get; set; }

        public ApiSubscriptionStats Stats { get; set; }
    }

    public class ApiSubscriptionConfig
    {
        public bool? AutoDownload { get; set; }

        public int? DownloadMaxCount { get; set; }

        public VideoOrder? DownloadOrder { get; set; }

        public bool? MarkDeletedAsWatched { get; set; }

        public bool? DeleteWatched { get; set; }

        /// <summary>Grace period in minutes before a marked video's files are deleted (0 = immediate).</summary>
        public int? DeleteGracePeriod { get; set; }

        public string DownloadPath { get; set; }

        public bool? WriteSubtitles { get; set; }

        public bool? WriteAutoSub { get; set; }

        public bool? AllSubs { get; set; }

        public string SubFormat { get; set; }

        public string SubLang { get; set; }

        public string SponsorblockActions { get; set; }

        public List<ApiSubscriptionFilter> Filters { get; set; } = new();
    }

    public class ApiSubscriptionStats
    {
        public int TotalVideoCount { get; set; }

        public int WatchedVideoCount { get; set; }

        public int DownloadedVideoCount { get; set; }

        public long DiskUsageBytes { get; set; }
    }
}
