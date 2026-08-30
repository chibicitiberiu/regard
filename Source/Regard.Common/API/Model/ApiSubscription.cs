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

        /// <summary>Take YouTube Shorts into the library at all. Applied at sync time.</summary>
        public bool? IncludeShorts { get; set; }

        /// <summary>Take members-only videos into the library at all. Applied at sync time.</summary>
        public bool? IncludeMembersOnly { get; set; }

        /// <summary>Auto-download only videos published on/after this "yyyy-MM-dd" date ("" = no bound).</summary>
        public string PublishedAfter { get; set; }

        /// <summary>Auto-download only videos published on/before this "yyyy-MM-dd" date, inclusive of
        /// the whole day ("" = no bound).</summary>
        public string PublishedBefore { get; set; }

        // Effective inherited defaults: the value each tri-state field would resolve to (from the parent
        // folder / user / global) when left unset. Used to label the "Default (…)" option on the form.
        public bool AutoDownloadDefault { get; set; }
        public VideoOrder DownloadOrderDefault { get; set; }
        public bool DeleteWatchedDefault { get; set; }
        public bool MarkDeletedAsWatchedDefault { get; set; }
        public bool IncludeShortsDefault { get; set; }
        public bool IncludeMembersOnlyDefault { get; set; }
    }

    public class ApiSubscriptionStats
    {
        public int TotalVideoCount { get; set; }

        public int WatchedVideoCount { get; set; }

        public int DownloadedVideoCount { get; set; }

        public long DiskUsageBytes { get; set; }
    }
}
