using System;
using System.Collections.Generic;
using System.Linq;

namespace Regard.Backend.Common.Utils
{
    /// <summary>
    /// Which yt-dlp stdout captures to delete.
    ///
    /// These are the one-file-per-invocation dumps in <c>Logs/ytdl</c>. NLog does not write them and so
    /// cannot prune them, and nothing else ever did: on the dev install they reached 2794 files and
    /// 581 MB in a week, growing ~40 MB a day from the hourly metadata refresh alone.
    ///
    /// Selection is by last-write time, deliberately not by the timestamp in the file name. Those names
    /// look parseable but are written with a 12-hour clock and no AM/PM separator
    /// (<c>20260903084512PM_417_stdout.txt</c>), so 08:45 and 20:45 differ only by a "PM" glued to the
    /// end — and getting that wrong silently deletes the wrong twelve hours. The filesystem already
    /// knows the answer.
    /// </summary>
    public static class LogRetention
    {
        /// <summary>
        /// Returns the names to delete. A non-positive retention keeps everything, matching the other
        /// retention knobs: an admin typing 0 has not asked for the logs to be erased.
        ///
        /// Deleting a capture while yt-dlp still has it open is safe — the writer appends per line and
        /// re-creates the file if it has gone — but a file written within the last few minutes is
        /// excluded anyway, because there is no reason to race it.
        /// </summary>
        public static IReadOnlyList<string> SelectForDeletion(
            IEnumerable<(string Name, DateTimeOffset LastWriteUtc)> files,
            DateTimeOffset nowUtc,
            int retentionDays)
        {
            if (files == null || retentionDays <= 0)
                return Array.Empty<string>();

            var cutoff = nowUtc.AddDays(-retentionDays);

            return files
                .Where(f => !string.IsNullOrEmpty(f.Name))
                .Where(f => f.LastWriteUtc < cutoff)
                .Select(f => f.Name)
                .ToList();
        }
    }
}
