namespace Regard.Common.API.Admin
{
    /// <summary>
    /// Set (or clear) a per-user quota override. A null value clears the override so the user
    /// inherits the global default again.
    /// </summary>
    public class SetUserQuotaRequest
    {
        public string UserId { get; set; }

        /// <summary>Video-count hard cap; null = clear override (inherit global default).</summary>
        public int? VideoQuota { get; set; }

        /// <summary>Storage hard cap in GB; null = clear override (inherit global default).</summary>
        public double? StorageQuotaGb { get; set; }
    }
}
