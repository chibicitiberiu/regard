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
    }
}
