using System.Collections.Generic;

namespace Regard.Common.API.Response
{
    public enum SetupCheckStatus
    {
        Ok = 0,
        Warning = 1,
        Error = 2,
    }

    public class SetupCheckResult
    {
        public string Name { get; set; }
        public SetupCheckStatus Status { get; set; }
        public string Message { get; set; }
    }

    public class SetupChecksResponse
    {
        public List<SetupCheckResult> Checks { get; set; } = new();

        /// <summary>True if any check is an Error (the wizard blocks continuing until fixed).</summary>
        public bool HasErrors { get; set; }
    }
}
