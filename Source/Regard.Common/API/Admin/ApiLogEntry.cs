using System;

namespace Regard.Common.API.Admin
{
    /// <summary>One parsed line (or line plus its stack trace) from a server log file.</summary>
    public class ApiLogEntry
    {
        /// <summary>
        /// Server-local wall clock as the log recorded it. No offset, because NLog's ${longdate} does
        /// not write one — displaying it as if it were UTC would shift every timestamp.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>Level as written (INFO, WARN, …), so an unrecognised one still shows.</summary>
        public string Level { get; set; }

        /// <summary>Sortable severity; matches LogSeverity on the server.</summary>
        public int Severity { get; set; }

        public string Logger { get; set; }

        /// <summary>Null on entries written before the log format gained the field.</summary>
        public string Callsite { get; set; }

        /// <summary>Only set on request-scoped entries; null for anything logged from a job.</summary>
        public string RequestUrl { get; set; }

        public string Message { get; set; }

        /// <summary>Exception and stack trace, when the entry had one. Collapsed in the UI.</summary>
        public string Detail { get; set; }
    }

    /// <summary>A page of log entries, newest first.</summary>
    public class ApiLogPage
    {
        public ApiLogEntry[] Entries { get; set; } = Array.Empty<ApiLogEntry>();

        /// <summary>Entries matching the filter across the whole file, independent of skip/take.</summary>
        public int TotalMatched { get; set; }

        /// <summary>Which file this page came from, so the UI can reflect the resolved default.</summary>
        public string File { get; set; }
    }

    /// <summary>One log file on disk.</summary>
    public class ApiLogFile
    {
        public string Name { get; set; }
        public long Bytes { get; set; }
        public DateTimeOffset LastWriteUtc { get; set; }
    }
}
