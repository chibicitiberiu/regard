using System;

namespace Regard.Backend.Common.Utils
{
    /// <summary>
    /// NLog's severity levels, in NLog's own order, so "at least Warn" is a comparison rather than a
    /// set membership test. <see cref="Unknown"/> sorts last deliberately: a level string we don't
    /// recognise is more likely to be something that went wrong than something routine, and hiding it
    /// behind a min-level filter would be the wrong default.
    /// </summary>
    public enum LogSeverity
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warn = 3,
        Error = 4,
        Fatal = 5,
        Unknown = 6,
    }

    /// <summary>
    /// One entry from a Regard log file — which is not the same as one line. Roughly 9% of the lines in
    /// a real log are continuations of the entry above them (exception stack traces), and treating those
    /// as entries turns a single failure into a dozen meaningless rows.
    /// </summary>
    public sealed class LogEntry
    {
        /// <summary>
        /// Server-local wall clock, as written. Deliberately a <see cref="DateTime"/> with
        /// <see cref="DateTimeKind.Unspecified"/> rather than a DateTimeOffset: NLog's ${longdate}
        /// records no timezone, so attaching one would be inventing information.
        /// </summary>
        public DateTime Timestamp { get; set; }

        public int EventId { get; set; }

        public LogSeverity Severity { get; set; }

        /// <summary>The level exactly as it appeared, so an unrecognised one is still readable.</summary>
        public string Level { get; set; }

        public string Logger { get; set; }

        /// <summary>Method that logged it. Null on entries written before the format gained the field.</summary>
        public string Callsite { get; set; }

        /// <summary>Set only on request-scoped entries; null or empty for anything logged from a job.</summary>
        public string RequestUrl { get; set; }

        public string MvcAction { get; set; }

        public string Message { get; set; }

        /// <summary>
        /// The continuation lines that followed, joined — in practice an exception and its stack trace.
        /// Null when the entry was a single line, which is the common case.
        /// </summary>
        public string Detail { get; set; }

        public bool HasDetail => !string.IsNullOrEmpty(Detail);
    }
}
