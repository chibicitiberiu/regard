using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.API.Response
{
    public class ServerStatusResponse
    {
        /// <summary>
        /// If true, initial setup was completed
        /// </summary>
        public bool Initialized { get; set; }

        /// <summary>
        /// If true, there is at least 1 user
        /// </summary>
        public bool HaveUsers { get; set; }

        /// <summary>
        /// If true, there is at least 1 admin user
        /// </summary>
        public bool HaveAdmin { get; set; }

        /// <summary>
        /// If true, self-registration is open (anonymous — lets the welcome/login UI show or hide the
        /// Register option without needing an admin-only settings call).
        /// </summary>
        public bool AllowRegistrations { get; set; }
    }
}
