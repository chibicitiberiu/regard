using System;
using System.Collections.Generic;
using System.Linq;

namespace Regard.Backend.Common.Utils
{
    public sealed class LogQuery
    {
        /// <summary>Entries below this are dropped. Defaults to everything.</summary>
        public LogSeverity MinSeverity { get; set; } = LogSeverity.Trace;

        /// <summary>
        /// Case-insensitive substring, matched against the message, logger and callsite — and against
        /// the exception detail too, because "find where this stack trace happened" is most of why
        /// anyone opens a log.
        /// </summary>
        public string Search { get; set; }

        public int Skip { get; set; }

        public int Take { get; set; } = 100;
    }

    public sealed class LogPage
    {
        public IReadOnlyList<LogEntry> Entries { get; set; } = Array.Empty<LogEntry>();

        /// <summary>Total entries matching the filter, independent of Skip/Take.</summary>
        public int TotalMatched { get; set; }
    }

    /// <summary>
    /// Filters and pages a stream of entries, newest first.
    ///
    /// Pure and streaming on purpose. Log files reach several megabytes — tens of thousands of entries —
    /// and the caller reads them off disk lazily, so materialising the file to sort it would be the
    /// expensive way to answer a question that only needs one pass. Instead this keeps a sliding window
    /// of the most recent <c>Skip + Take</c> matches and counts the rest, which bounds memory by the
    /// page position rather than by the file size.
    /// </summary>
    public static class LogQueryRunner
    {
        /// <summary>Guards against a client asking for a page so deep it would buffer the whole file.</summary>
        public const int MaxWindow = 10_000;

        public static LogPage Run(IEnumerable<LogEntry> entries, LogQuery query)
        {
            query ??= new LogQuery();

            int take = Math.Max(0, query.Take);
            int skip = Math.Max(0, query.Skip);
            int window = Math.Min(MaxWindow, skip + take);

            var recent = new LinkedList<LogEntry>();
            int matched = 0;

            foreach (var entry in entries ?? Enumerable.Empty<LogEntry>())
            {
                if (!Matches(entry, query))
                    continue;

                matched++;

                if (window == 0)
                    continue;

                recent.AddLast(entry);
                if (recent.Count > window)
                    recent.RemoveFirst();
            }

            // `recent` holds the newest `window` matches in file order; reverse for newest-first, then
            // page into it. Anything older than the window is by definition on a later page than the
            // one asked for.
            var page = recent.Reverse().Skip(skip).Take(take).ToList();

            return new LogPage { Entries = page, TotalMatched = matched };
        }

        private static bool Matches(LogEntry entry, LogQuery query)
        {
            if (entry == null)
                return false;

            if (entry.Severity < query.MinSeverity)
                return false;

            if (string.IsNullOrWhiteSpace(query.Search))
                return true;

            var needle = query.Search;
            return Contains(entry.Message, needle)
                || Contains(entry.Logger, needle)
                || Contains(entry.Callsite, needle)
                || Contains(entry.RequestUrl, needle)
                || Contains(entry.Detail, needle);
        }

        private static bool Contains(string haystack, string needle)
            => haystack != null && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
