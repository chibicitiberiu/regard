using Regard.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.API.Model
{
    public class ApiSubscriptionFolder
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

        public ParentId ParentId { get; set; }

        public ApiSubscriptionFolderConfig Config { get; set; }

        public ApiSubscriptionFolderStats Stats { get; set; }
    }

    public class ApiSubscriptionFolderConfig
    {
        public bool? AutoDownload { get; set; }

        public int? DownloadMaxCount { get; set; }

        public VideoOrder? DownloadOrder { get; set; }

        public bool? AutomaticDeleteWatched { get; set; }

        public string DownloadPath { get; set; }
    }

    public class ApiSubscriptionFolderStats
    {
        public int TotalSubscriptionCount { get; set; }

        public int TotalVideoCount { get; set; }

        public int WatchedVideoCount { get; set; }

        public int DownloadedVideoCount { get; set; }

        public int DiskUsageBytes { get; set; }
    }
}
