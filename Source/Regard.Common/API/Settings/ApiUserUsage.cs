namespace Regard.Common.API.Settings
{
    /// <summary>
    /// A user's current download usage against their effective quota, for the Settings page.
    /// Quota fields are null when unlimited.
    /// </summary>
    public class ApiUserUsage
    {
        /// <summary>Number of downloaded videos the user currently keeps.</summary>
        public int VideoCount { get; set; }

        /// <summary>Total bytes the user's downloads occupy on disk.</summary>
        public long UsedBytes { get; set; }

        /// <summary>Effective video-count hard cap; null = unlimited.</summary>
        public int? VideoQuota { get; set; }

        /// <summary>Effective storage hard cap in bytes; null = unlimited.</summary>
        public long? StorageQuotaBytes { get; set; }
    }
}
