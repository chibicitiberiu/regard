namespace Regard.Common.API.Admin
{
    /// <summary>
    /// A user account as seen by an administrator, with current usage and any per-user quota override.
    /// </summary>
    public class ApiAdminUser
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }

        /// <summary>True if the account has the admin role.</summary>
        public bool IsAdmin { get; set; }

        /// <summary>True if the account is disabled (locked out) and cannot log in.</summary>
        public bool IsDisabled { get; set; }

        /// <summary>Number of downloaded videos the user currently keeps.</summary>
        public int VideoCount { get; set; }

        /// <summary>Total bytes the user's downloads occupy on disk.</summary>
        public long UsedBytes { get; set; }

        /// <summary>Per-user video-quota override; null = inherits the global default.</summary>
        public int? VideoQuotaOverride { get; set; }

        /// <summary>Per-user storage-quota override in GB; null = inherits the global default.</summary>
        public double? StorageQuotaOverrideGb { get; set; }
    }
}
