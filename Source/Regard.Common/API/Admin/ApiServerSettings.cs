namespace Regard.Common.API.Admin
{
    /// <summary>
    /// Server-wide administrative settings. Quota fields are <c>null</c> when unlimited (stored
    /// server-side as the <c>-1</c> sentinel); an empty quota input therefore means "no limit".
    /// </summary>
    public class ApiServerSettings
    {
        /// <summary>Whether new users may self-register.</summary>
        public bool AllowRegistrations { get; set; }

        /// <summary>Default hard cap on downloaded videos per user. null = unlimited.</summary>
        public int? DefaultVideoQuota { get; set; }

        /// <summary>Default hard cap on stored downloads per user, in GB. null = unlimited.</summary>
        public double? DefaultStorageQuotaGb { get; set; }

        /// <summary>How long finished jobs are kept in the Job Log, in days.</summary>
        public int JobHistoryRetentionDays { get; set; }

        // ---- Download throttling / anti-bot ----

        /// <summary>Master switch for download pacing + per-host throttling.</summary>
        public bool ThrottleEnabled { get; set; }

        /// <summary>Seconds yt-dlp sleeps between extraction HTTP requests.</summary>
        public int SleepRequests { get; set; }

        /// <summary>Minimum seconds yt-dlp sleeps before each download.</summary>
        public int SleepInterval { get; set; }

        /// <summary>Maximum seconds yt-dlp sleeps before each download.</summary>
        public int MaxSleepInterval { get; set; }

        /// <summary>Global default download bandwidth cap (yt-dlp --limit-rate, e.g. "2M"); empty = none.</summary>
        public string LimitRate { get; set; }

        /// <summary>Per-host jittered pacing between downloads (seconds).</summary>
        public int DownloadMinSeconds { get; set; }
        public int DownloadMaxSeconds { get; set; }

        /// <summary>Per-host jittered pacing between metadata extractions (seconds).</summary>
        public int ExtractMinSeconds { get; set; }
        public int ExtractMaxSeconds { get; set; }

        /// <summary>Per-host download caps (0 = unlimited).</summary>
        public int MaxPerHour { get; set; }
        public int MaxPerDay { get; set; }

        /// <summary>Max simultaneous downloads per host.</summary>
        public int PerHostConcurrency { get; set; }

        /// <summary>Read-only: max parallel jobs (Quartz pool). Set via REGARD_MAX_PARALLEL_JOBS; restart to change.</summary>
        public int MaxParallelJobs { get; set; }

        /// <summary>Read-only: whether a cookies.txt is currently configured/present on the server.</summary>
        public bool CookiesConfigured { get; set; }

        /// <summary>
        /// Write-only (never returned by GET): uploaded cookies.txt content. null = leave as-is;
        /// empty string = remove the current cookies file; non-empty = replace it.
        /// </summary>
        public string CookiesFileContent { get; set; }
    }
}
