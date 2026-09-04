using Microsoft.Extensions.Logging;
using Regard.Backend.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Regard.Backend.Services
{
    public sealed class LogFileDescriptor
    {
        public string Name { get; set; }
        public long Bytes { get; set; }
        public DateTimeOffset LastWriteUtc { get; set; }
    }

    /// <summary>
    /// Reads the NLog output files for the admin log viewer.
    ///
    /// Two things here are load-bearing:
    ///
    /// **Sharing.** NLog holds the current day's file open for writing for the life of the process
    /// (confirmed with lsof). Opening it without <see cref="FileShare.ReadWrite"/> fails outright, and
    /// the point of this feature is watching the log that is being written right now. Delete is shared
    /// too, so a read in flight cannot stop NLog's own retention from rolling a file away.
    ///
    /// **Path handling.** File names arrive from a browser. Nothing here ever combines a client string
    /// with a directory — a requested name is resolved by listing the directory and looking for an
    /// exact match, so "../../Regard.db" simply is not in the list. That is the same rule
    /// VideoController.Subtitle follows for subtitle languages.
    /// </summary>
    public class LogFileReader
    {
        /// <summary>Matches what nlog.config writes: regard-yyyy-MM-dd.log, plus size-rolled siblings.</summary>
        private const string AppLogPattern = "regard-*.log";

        private const string YtdlPattern = "*.txt";

        private readonly ILogger log;
        private readonly StorageManager storageManager;

        public LogFileReader(ILogger<LogFileReader> log, StorageManager storageManager)
        {
            this.log = log;
            this.storageManager = storageManager;
        }

        // ------------------------------------------------------------------ listing

        /// <summary>
        /// Application log files, newest first. Ordered by name, which is safe here because the name is
        /// an ISO date (regard-2026-09-05.log) and sorts chronologically.
        /// </summary>
        public IReadOnlyList<LogFileDescriptor> ListAppLogs()
            => List(storageManager.LogsDirectory, AppLogPattern, byName: true);

        /// <summary>
        /// yt-dlp stdout captures, newest first. There can be thousands, so callers page.
        ///
        /// Ordered by modification time, NOT by name: these are stamped with a 12-hour clock and the
        /// AM/PM glued on the end (20260905121016AM_360_stdout.txt), so "12…AM" sorts after "01…PM"
        /// while actually being eleven hours earlier. The filesystem already knows the real order.
        /// </summary>
        public IReadOnlyList<LogFileDescriptor> ListYtdlLogs()
            => List(storageManager.YtdlLogsDirectory, YtdlPattern, byName: false);

        private IReadOnlyList<LogFileDescriptor> List(string directory, string pattern, bool byName)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return Array.Empty<LogFileDescriptor>();

            try
            {
                return new DirectoryInfo(directory)
                    .EnumerateFiles(pattern, SearchOption.TopDirectoryOnly)
                    .Select(f => new LogFileDescriptor
                    {
                        Name = f.Name,
                        Bytes = f.Length,
                        LastWriteUtc = new DateTimeOffset(f.LastWriteTimeUtc, TimeSpan.Zero),
                    })
                    .OrderByDescending(f => byName ? default : f.LastWriteUtc)
                    .ThenByDescending(f => byName ? f.Name : string.Empty, StringComparer.Ordinal)
                    .ToList();
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Could not list log files in {0}.", directory);
                return Array.Empty<LogFileDescriptor>();
            }
        }

        // ------------------------------------------------------------------ resolving a requested name

        /// <summary>
        /// Turns a client-supplied name into a real path, or null.
        ///
        /// Deliberately not Path.Combine + a traversal check: this resolves by looking the name up in
        /// the directory listing, so anything that is not already a log file in that directory cannot
        /// be addressed at all, whatever it is spelled like.
        /// </summary>
        public string ResolveAppLog(string name) => Resolve(storageManager.LogsDirectory, ListAppLogs(), name);

        public string ResolveYtdlLog(string name) => Resolve(storageManager.YtdlLogsDirectory, ListYtdlLogs(), name);

        private static string Resolve(string directory, IReadOnlyList<LogFileDescriptor> listing, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var match = listing.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.Ordinal));
            return match == null ? null : Path.Combine(directory, match.Name);
        }

        /// <summary>The newest application log, i.e. what the viewer opens by default.</summary>
        public string DefaultAppLogName() => ListAppLogs().FirstOrDefault()?.Name;

        // ------------------------------------------------------------------ reading

        /// <summary>
        /// Runs a query against one application log file. Streams: the parser is lazy and the query
        /// keeps only a bounded window, so a 10 MB file never lands in memory at once.
        /// </summary>
        public LogPage Query(string path, LogQuery query)
        {
            using var reader = OpenShared(path);
            return LogQueryRunner.Run(LogLineParser.Parse(ReadLines(reader)), query);
        }

        /// <summary>Whole contents of a yt-dlp capture. These are individually small.</summary>
        public string ReadText(string path, int maxBytes = 2 * 1024 * 1024)
        {
            using var reader = OpenShared(path);
            var buffer = new char[maxBytes];
            int read = reader.ReadBlock(buffer, 0, maxBytes);
            var text = new string(buffer, 0, read);

            // Say so rather than silently truncating.
            return reader.Peek() >= 0
                ? text + $"\n\n[truncated at {maxBytes / 1024} KB]"
                : text;
        }

        /// <summary>
        /// Opens a stream for reading a file another process holds open for writing. FileShare.Delete
        /// matters as much as ReadWrite: without it, a read in progress would block NLog's own retention
        /// from deleting an aged-out file.
        /// </summary>
        public static StreamReader OpenShared(string path)
        {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                                        FileShare.ReadWrite | FileShare.Delete);
            return new StreamReader(stream);
        }

        private static IEnumerable<string> ReadLines(StreamReader reader)
        {
            string line;
            while ((line = reader.ReadLine()) != null)
                yield return line;
        }
    }
}
