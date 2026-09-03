using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Regard.Backend.Common.Utils;
using Regard.Backend.DB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    /// <summary>
    /// Takes and prunes timestamped snapshots of the database.
    ///
    /// The primitive is SQLite's <c>VACUUM INTO</c>, which runs as a read transaction against the live
    /// database. That means no stopping the app, no blocking writers, and — unlike copying the file — a
    /// consistent result that already includes everything sitting in the write-ahead log, with no
    /// -wal/-shm sidecars to carry alongside it. The copy also comes out compacted, so a snapshot is
    /// smaller than the database it came from.
    ///
    /// It is not atomic, though, and it does not reliably remove its output when it fails partway. So a
    /// snapshot is written to a .db.tmp name, opened and integrity-checked, and only then moved into
    /// place. Without that, a backup interrupted by a full disk leaves a truncated file that is the
    /// newest thing in the directory — and retention would count it as good and evict a real one.
    ///
    /// SQL Server has no equivalent that this process can use: BACKUP DATABASE writes to a path on the
    /// database host, which for anything but a co-located server is not this filesystem. There, every
    /// operation here reports itself skipped rather than failing.
    /// </summary>
    public class DatabaseBackupService
    {
        private static readonly TimeSpan TempFileStaleAfter = TimeSpan.FromHours(1);

        private readonly ILogger log;
        private readonly DataContext dataContext;
        private readonly StorageManager storageManager;

        public DatabaseBackupService(ILogger<DatabaseBackupService> log,
                                     DataContext dataContext,
                                     StorageManager storageManager)
        {
            this.log = log;
            this.dataContext = dataContext;
            this.storageManager = storageManager;
        }

        public string BackupDirectory => storageManager.BackupDirectory;

        /// <summary>
        /// True when the live database is one we can snapshot, i.e. SQLite backed by a real file.
        /// </summary>
        public bool IsSupported => ResolveDatabasePath() != null;

        /// <summary>
        /// Absolute path of the SQLite database file, or null when there isn't one (SQL Server, or an
        /// in-memory database as used by the design-time context factory).
        ///
        /// Taken from the live connection rather than rebuilt from DataDirectory, so an overridden
        /// connection string stays correct. Note SqliteConnection.DataSource only returns an absolute
        /// path while the connection is OPEN; closed it hands back the connection-string value verbatim,
        /// and appsettings.json ships a relative "DataDirectory": "Data". Hence GetFullPath.
        /// </summary>
        public string ResolveDatabasePath()
        {
            try
            {
                var connection = dataContext.Database.GetDbConnection();
                if (!(connection is SqliteConnection))
                    return null;

                var source = connection.DataSource;

                // An open in-memory connection reports "" rather than ":memory:", so both are checked.
                if (string.IsNullOrWhiteSpace(source)
                    || source.Equals(":memory:", StringComparison.OrdinalIgnoreCase)
                    || source.Contains("mode=memory", StringComparison.OrdinalIgnoreCase))
                    return null;

                return Path.GetFullPath(source);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Could not determine the database file path.");
                return null;
            }
        }

        // ------------------------------------------------------------------ taking a snapshot

        /// <summary>
        /// Writes a snapshot and returns what happened. Never throws for an expected condition — an
        /// unsupported provider, a missing database, not enough disk — because the caller is usually an
        /// unattended sweep. The pre-migration path inspects <see cref="BackupOutcome.Succeeded"/> and
        /// decides for itself whether to abort.
        /// </summary>
        public async Task<BackupOutcome> CreateBackupAsync(bool preMigration = false,
                                                           CancellationToken cancellation = default)
        {
            var databasePath = ResolveDatabasePath();
            if (databasePath == null)
                return BackupOutcome.Skip("the database is not SQLite, so it cannot be snapshotted from here");

            if (!File.Exists(databasePath))
                return BackupOutcome.Skip($"there is no database file at {databasePath} yet");

            var databaseBytes = new FileInfo(databasePath).Length;
            if (databaseBytes == 0)
                return BackupOutcome.Skip("the database file is empty");

            Directory.CreateDirectory(BackupDirectory);

            // The snapshot is at most the size of the database plus its write-ahead log; ask for double
            // that so a backup never fills the disk it is protecting. This machine has hit disk-I/O
            // errors from SQLite at high occupancy before, and those wedge every request.
            long walBytes = FileLengthOrZero(databasePath + "-wal");
            long required = (databaseBytes + walBytes) * 2;
            long? free = GetAvailableFreeSpace(BackupDirectory);
            if (free.HasValue && free.Value < required)
                return BackupOutcome.Skip(
                    $"only {Bytes(free.Value)} free at {BackupDirectory}, need {Bytes(required)}");

            var (finalPath, tempPath) = ReserveNames(preMigration);

            try
            {
                // VACUUM INTO refuses to overwrite, so clear any debris on the exact temp name first.
                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                // Bound parameter, not interpolation: this path is derived rather than user-supplied
                // today, but a quote in it would otherwise break the statement, and DDL built by string
                // concatenation is a habit worth not having.
                if (dataContext.Database.CurrentTransaction != null)
                    return BackupOutcome.Skip("a transaction is open on this context; VACUUM cannot run inside one");

                await dataContext.Database.ExecuteSqlRawAsync("VACUUM INTO {0}", new object[] { tempPath }, cancellation);

                var problem = VerifySnapshot(tempPath);
                if (problem != null)
                {
                    TryDelete(tempPath);
                    return BackupOutcome.Fail($"the snapshot failed verification and was discarded: {problem}");
                }

                File.Move(tempPath, finalPath);

                var bytes = new FileInfo(finalPath).Length;
                log.LogInformation("Database snapshot written: {0} ({1}, from a {2} database).",
                    Path.GetFileName(finalPath), Bytes(bytes), Bytes(databaseBytes));

                return BackupOutcome.Success(finalPath, bytes);
            }
            catch (Exception ex)
            {
                TryDelete(tempPath);
                log.LogError(ex, "Database snapshot failed.");
                return BackupOutcome.Fail(ex.Message);
            }
        }

        /// <summary>
        /// Opens the freshly written file as a database and asks SQLite whether it is intact. This is
        /// the gate that stops a half-written snapshot being promoted; returns null when it is fine, or
        /// a description of the problem.
        /// </summary>
        internal string VerifySnapshot(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return "the file was not created";
                if (new FileInfo(path).Length == 0)
                    return "the file is empty";

                var builder = new SqliteConnectionStringBuilder
                {
                    DataSource = path,
                    Mode = SqliteOpenMode.ReadOnly,
                };

                using var connection = new SqliteConnection(builder.ToString());
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA integrity_check;";
                var result = command.ExecuteScalar() as string;

                if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                    return $"integrity_check returned '{result}'";

                // A structurally valid but contentless file would pass integrity_check, so confirm the
                // schema actually came across.
                command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'table';";
                var tables = Convert.ToInt64(command.ExecuteScalar());
                if (tables == 0)
                    return "the snapshot contains no tables";

                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>
        /// Picks the final and temporary names, stepping the timestamp forward if something already holds
        /// them. Names carry seconds, and VACUUM INTO errors rather than overwriting, so two backups
        /// inside one second — a double-clicked button — would otherwise fail the second one.
        /// </summary>
        private (string Final, string Temp) ReserveNames(bool preMigration)
        {
            var stamp = DateTimeOffset.UtcNow;
            for (int attempt = 0; attempt < 60; attempt++)
            {
                var name = BackupRetention.NameFor(stamp.AddSeconds(attempt), preMigration);
                var final = Path.Combine(BackupDirectory, name);
                var temp = Path.Combine(BackupDirectory, name + ".tmp");

                if (!File.Exists(final))
                    return (final, temp);
            }

            // 60 collisions means something else is wrong; fall through to a unique suffix rather than
            // looping forever.
            var fallback = Path.Combine(BackupDirectory,
                BackupRetention.NameFor(stamp, preMigration) + "." + Guid.NewGuid().ToString("N").Substring(0, 6));
            return (fallback, fallback + ".tmp");
        }

        // ------------------------------------------------------------------ retention

        /// <summary>
        /// Deletes surplus snapshots and abandoned .db.tmp debris. Returns how many files went.
        /// Only ever touches names this service could have written — see <see cref="BackupRetention"/>.
        /// </summary>
        public int ApplyRetention(int keepCount)
        {
            if (!Directory.Exists(BackupDirectory))
                return 0;

            var entries = Listing();
            int removed = 0;

            foreach (var name in BackupRetention.SelectForDeletion(entries, keepCount))
                if (TryDelete(Path.Combine(BackupDirectory, name)))
                    removed++;

            foreach (var name in BackupRetention.SelectStaleTempFiles(entries, DateTimeOffset.UtcNow, TempFileStaleAfter))
                if (TryDelete(Path.Combine(BackupDirectory, name)))
                {
                    log.LogInformation("Removed abandoned snapshot debris: {0}", name);
                    removed++;
                }

            return removed;
        }

        // ------------------------------------------------------------------ status

        public BackupStatus GetStatus()
        {
            var status = new BackupStatus
            {
                Supported = IsSupported,
                Directory = BackupDirectory,
            };

            var databasePath = ResolveDatabasePath();
            if (databasePath != null && File.Exists(databasePath))
            {
                status.DatabaseBytes = new FileInfo(databasePath).Length;
                status.WalBytes = FileLengthOrZero(databasePath + "-wal");
                status.ReclaimableBytes = GetReclaimableBytes();
            }

            if (Directory.Exists(BackupDirectory))
            {
                var backups = Listing()
                    .Where(f => BackupRetention.TryParseName(f.Name, out _, out _))
                    .ToList();

                status.Count = backups.Count;
                status.TotalBytes = backups.Sum(f => FileLengthOrZero(Path.Combine(BackupDirectory, f.Name)));
                if (backups.Count > 0)
                    status.LatestUtc = backups.Max(f => f.LastWriteUtc);
            }

            status.FreeBytes = GetAvailableFreeSpace(BackupDirectory);
            return status;
        }

        /// <summary>
        /// How many bytes an in-place VACUUM would hand back to the filesystem: pages on the free list
        /// that SQLite is holding for reuse. Cheap to read and the only honest way to say whether
        /// compacting is worth its cost.
        /// </summary>
        public long? GetReclaimableBytes()
        {
            if (!IsSupported)
                return null;

            try
            {
                var connection = dataContext.Database.GetDbConnection();
                bool wasClosed = connection.State != System.Data.ConnectionState.Open;
                if (wasClosed)
                    connection.Open();

                try
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = "PRAGMA freelist_count;";
                    long freelist = Convert.ToInt64(command.ExecuteScalar());

                    command.CommandText = "PRAGMA page_size;";
                    long pageSize = Convert.ToInt64(command.ExecuteScalar());

                    return freelist * pageSize;
                }
                finally
                {
                    if (wasClosed)
                        connection.Close();
                }
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Could not read the database free-page count.");
                return null;
            }
        }

        // ------------------------------------------------------------------ helpers

        private List<(string Name, DateTimeOffset LastWriteUtc)> Listing()
            => new DirectoryInfo(BackupDirectory)
                .EnumerateFiles()
                .Select(f => (f.Name, (DateTimeOffset)new DateTimeOffset(f.LastWriteTimeUtc, TimeSpan.Zero)))
                .ToList();

        private static long FileLengthOrZero(string path)
        {
            try { return File.Exists(path) ? new FileInfo(path).Length : 0; }
            catch { return 0; }
        }

        private bool TryDelete(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return false;
                File.Delete(path);
                return true;
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Could not delete {0}.", path);
                return false;
            }
        }

        /// <summary>
        /// Free space on the filesystem holding <paramref name="directory"/>. Deliberately not
        /// Path.GetPathRoot: on Linux that is "/", which measures the container's root filesystem rather
        /// than the volume the data is actually mounted on. DriveInfo does a statvfs on the path given,
        /// which is what we want — but it throws on Windows for anything that is not a drive root, hence
        /// the fallback.
        /// </summary>
        private long? GetAvailableFreeSpace(string directory)
        {
            try
            {
                return new DriveInfo(Path.GetFullPath(directory)).AvailableFreeSpace;
            }
            catch (Exception)
            {
                try
                {
                    var root = Path.GetPathRoot(Path.GetFullPath(directory));
                    return string.IsNullOrEmpty(root) ? (long?)null : new DriveInfo(root).AvailableFreeSpace;
                }
                catch (Exception ex)
                {
                    log.LogDebug(ex, "Could not determine free space for {0}.", directory);
                    return null;
                }
            }
        }

        internal static string Bytes(long value)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = value;
            int unit = 0;
            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }
            return $"{size:0.#} {units[unit]}";
        }
    }

    /// <summary>Result of a snapshot attempt. "Skipped" is a normal, expected outcome, not a failure.</summary>
    public class BackupOutcome
    {
        public bool Succeeded { get; private set; }
        public bool Skipped { get; private set; }
        public string Path { get; private set; }
        public long Bytes { get; private set; }
        public string Reason { get; private set; }

        public static BackupOutcome Success(string path, long bytes)
            => new BackupOutcome { Succeeded = true, Path = path, Bytes = bytes };

        public static BackupOutcome Skip(string reason)
            => new BackupOutcome { Skipped = true, Reason = reason };

        public static BackupOutcome Fail(string reason)
            => new BackupOutcome { Reason = reason };

        public string Describe()
            => Succeeded ? $"{System.IO.Path.GetFileName(Path)} ({DatabaseBackupService.Bytes(Bytes)})"
             : Skipped ? $"skipped: {Reason}"
             : $"failed: {Reason}";
    }

    public class BackupStatus
    {
        public bool Supported { get; set; }
        public string Directory { get; set; }
        public int Count { get; set; }
        public long TotalBytes { get; set; }
        public DateTimeOffset? LatestUtc { get; set; }
        public long DatabaseBytes { get; set; }
        public long WalBytes { get; set; }
        public long? ReclaimableBytes { get; set; }
        public long? FreeBytes { get; set; }
    }
}
