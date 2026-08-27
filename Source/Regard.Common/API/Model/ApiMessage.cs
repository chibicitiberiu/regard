using System;

namespace Regard.Common.API.Model
{
    /// <summary>
    /// Mirrors the backend MessageSeverity enum (same order, so an int cast maps cleanly).
    /// </summary>
    public enum ApiMessageSeverity
    {
        Info,
        Warning,
        Error
    }

    public class ApiMessage
    {
        public int Id { get; set; }

        public DateTimeOffset Timestamp { get; set; }

        public ApiMessageSeverity Severity { get; set; }

        public string Message { get; set; }

        public string Details { get; set; }

        public long? JobId { get; set; }
    }
}
