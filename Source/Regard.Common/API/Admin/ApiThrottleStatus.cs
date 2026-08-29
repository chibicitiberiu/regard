using System;
using System.Collections.Generic;

namespace Regard.Common.API.Admin
{
    /// <summary>
    /// Read-only view of download throttling, visible to any signed-in user so they understand why a
    /// download may be paced/queued. Editing the values is admin-only (ApiServerSettings).
    /// </summary>
    public class ApiThrottleStatus
    {
        public bool Enabled { get; set; }
        public int DownloadMinSeconds { get; set; }
        public int DownloadMaxSeconds { get; set; }
        public int MaxPerHour { get; set; }
        public int MaxPerDay { get; set; }
        public bool CookiesConfigured { get; set; }
        public List<ApiThrottleHost> Hosts { get; set; } = new();
    }

    public class ApiThrottleHost
    {
        public string Host { get; set; }
        public int InFlight { get; set; }
        public int Queued { get; set; }
        public int UsedLastHour { get; set; }
        public int UsedLastDay { get; set; }
        public DateTimeOffset? NextSlot { get; set; }
    }
}
