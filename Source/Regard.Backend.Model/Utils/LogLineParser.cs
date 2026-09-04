using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Regard.Backend.Common.Utils
{
    /// <summary>
    /// Turns the lines of a Regard log file into entries.
    ///
    /// Three things about the real files drive this, all measured across the ~80,000 lines on the dev
    /// install rather than assumed:
    ///
    /// 1. **Two layouts coexist, sometimes inside one file.** Before Batch 6a the layout was five
    ///    fields; after it, eight (callsite, request URL and MVC action were folded in when the
    ///    duplicate CSV target was dropped). The file that was being written across that restart holds
    ///    both, so the layout has to be decided per line. Old entries age out on their own — NLog keeps
    ///    14 days — but a viewer shipping today has to read them.
    ///
    /// 2. **Messages contain pipes.** A video titled "Swimming | Episode 2" produces a line with more
    ///    separators than fields; one real file has 681 of them. So every split is bounded, and the
    ///    message keeps whatever pipes it had.
    ///
    /// 3. **About 9% of lines are continuations** — the stack trace under an exception. A line that
    ///    does not begin with a timestamp belongs to the entry above it.
    ///
    /// The layout is told apart by field 5. In the eight-field layout that is ${callsite}, which is
    /// either empty or a dotted method path with no spaces; in the five-field layout it is the message,
    /// which is prose. Requiring a dot as well as the absence of whitespace was checked against every
    /// line on disk: zero of the 69,815 old-format lines are mistaken for new ones, including the file
    /// full of embedded pipes.
    /// </summary>
    public static class LogLineParser
    {
        /// <summary>NLog's ${longdate} is fixed-width, but be tolerant of fewer fractional digits.</summary>
        private static readonly string[] TimestampFormats =
        {
            "yyyy-MM-dd HH:mm:ss.ffff",
            "yyyy-MM-dd HH:mm:ss.fff",
            "yyyy-MM-dd HH:mm:ss.ff",
            "yyyy-MM-dd HH:mm:ss.f",
            "yyyy-MM-dd HH:mm:ss",
        };

        private const int NewFieldCount = 8;
        private const int OldFieldCount = 5;

        /// <summary>
        /// Parses one line as the start of an entry. False means it is a continuation, a blank, or
        /// something that is not a log line at all — the caller decides which.
        /// </summary>
        public static bool TryParseHeader(string line, out LogEntry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(line))
                return false;

            int bar = line.IndexOf('|');
            if (bar <= 0)
                return false;

            if (!TryParseTimestamp(line.Substring(0, bar), out var timestamp))
                return false;

            // Bounded split: at most 8 pieces, so a message keeps any pipes of its own.
            var parts = line.Split('|', NewFieldCount);

            string level, logger, callsite, requestUrl, mvcAction, message;
            string eventIdText = parts.Length > 1 ? parts[1] : string.Empty;

            if (parts.Length == NewFieldCount && LooksLikeCallsite(parts[4]))
            {
                level = parts[2];
                logger = parts[3];
                callsite = NullIfEmpty(parts[4]);
                requestUrl = NullIfEmpty(parts[5]);
                mvcAction = NullIfEmpty(parts[6]);
                message = parts[7];
            }
            else
            {
                // Pre-Batch-6a layout. Re-split so the message is whole again: the 8-way split above
                // will have chopped it at any pipes it contains.
                var old = line.Split('|', OldFieldCount);
                if (old.Length < OldFieldCount)
                    return false;

                level = old[2];
                logger = old[3];
                callsite = null;
                requestUrl = null;
                mvcAction = null;
                message = old[4];
            }

            entry = new LogEntry
            {
                Timestamp = timestamp,
                EventId = int.TryParse(eventIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) ? id : 0,
                Level = level,
                Severity = ParseSeverity(level),
                Logger = logger,
                Callsite = callsite,
                RequestUrl = requestUrl,
                MvcAction = mvcAction,
                // NLog puts a space between the message and the exception, so single-line entries end
                // in one. Harmless, but it shows up in the UI.
                Message = message?.TrimEnd(),
            };
            return true;
        }

        /// <summary>
        /// Streams entries in file order (oldest first). Continuation lines are folded into the entry
        /// above; anything before the first header is discarded, because there is nothing to attach it
        /// to — that happens when reading a file that was rolled mid-exception.
        /// </summary>
        public static IEnumerable<LogEntry> Parse(IEnumerable<string> lines)
        {
            if (lines == null)
                yield break;

            LogEntry current = null;
            StringBuilder detail = null;

            foreach (var line in lines)
            {
                if (TryParseHeader(line, out var parsed))
                {
                    if (current != null)
                    {
                        current.Detail = detail?.Length > 0 ? detail.ToString() : null;
                        yield return current;
                    }

                    current = parsed;
                    detail = null;
                    continue;
                }

                // Not a header. Blank lines separate nothing worth keeping; anything else is the tail
                // of the entry above.
                if (current == null || string.IsNullOrWhiteSpace(line))
                    continue;

                detail ??= new StringBuilder();
                if (detail.Length > 0)
                    detail.Append('\n');
                detail.Append(line.TrimEnd());
            }

            if (current != null)
            {
                current.Detail = detail?.Length > 0 ? detail.ToString() : null;
                yield return current;
            }
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// Whether field 5 is a ${callsite} rather than the start of a message. Empty counts: NLog
        /// writes nothing there when it cannot resolve one. Otherwise it must look like a method path —
        /// no whitespace, and at least one dot, since a callsite is always Namespace.Type.Method.
        /// The dot is what makes this safe against a message that happens to contain several pipes.
        /// </summary>
        private static bool LooksLikeCallsite(string value)
        {
            if (value == null)
                return false;
            if (value.Length == 0)
                return true;

            if (!char.IsLetter(value[0]) && value[0] != '_')
                return false;

            bool hasDot = false;
            foreach (char c in value)
            {
                if (char.IsWhiteSpace(c))
                    return false;
                if (c == '.')
                    hasDot = true;
            }
            return hasDot;
        }

        private static bool TryParseTimestamp(string value, out DateTime timestamp)
            => DateTime.TryParseExact(value, TimestampFormats, CultureInfo.InvariantCulture,
                                      DateTimeStyles.None, out timestamp);

        private static string NullIfEmpty(string value)
            => string.IsNullOrEmpty(value) ? null : value;

        public static LogSeverity ParseSeverity(string level)
        {
            if (string.IsNullOrEmpty(level))
                return LogSeverity.Unknown;

            switch (level.Trim().ToUpperInvariant())
            {
                case "TRACE": return LogSeverity.Trace;
                case "DEBUG": return LogSeverity.Debug;
                case "INFO": return LogSeverity.Info;
                case "WARN": return LogSeverity.Warn;
                case "ERROR": return LogSeverity.Error;
                case "FATAL": return LogSeverity.Fatal;
                default: return LogSeverity.Unknown;
            }
        }
    }
}
