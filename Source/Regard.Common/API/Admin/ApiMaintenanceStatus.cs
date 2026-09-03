using System;

namespace Regard.Common.API.Admin
{
    /// <summary>
    /// Read-only picture of database housekeeping: how big the database is, how much of it is dead
    /// space, and what snapshots exist. Everything here is derived at request time rather than stored.
    /// </summary>
    public class ApiMaintenanceStatus
    {
        /// <summary>
        /// False on SQL Server, where neither snapshots nor compaction can be driven from this process
        /// (BACKUP DATABASE writes on the database host, and there is no VACUUM). The UI shows a plain
        /// note instead of controls that would silently do nothing.
        /// </summary>
        public bool Supported { get; set; }

        /// <summary>Where snapshots are written. Derived from the data directory, not settable.</summary>
        public string BackupDirectory { get; set; }

        /// <summary>Size of the live database file.</summary>
        public long DatabaseBytes { get; set; }

        /// <summary>
        /// Size of the write-ahead log. Worth showing separately: it grows during a long-running read
        /// and only shrinks at a checkpoint, so a large value here is usually transient rather than a
        /// problem.
        /// </summary>
        public long WalBytes { get; set; }

        /// <summary>
        /// Bytes an in-place compaction would return to the filesystem (free pages × page size). null
        /// when it cannot be determined. This is the number that says whether compacting is worth the
        /// disruption it causes.
        /// </summary>
        public long? ReclaimableBytes { get; set; }

        /// <summary>Free space on the filesystem holding the backup directory. null if undeterminable.</summary>
        public long? FreeBytes { get; set; }

        /// <summary>How many snapshots are currently kept.</summary>
        public int BackupCount { get; set; }

        /// <summary>Total size of those snapshots.</summary>
        public long BackupTotalBytes { get; set; }

        /// <summary>When the newest snapshot was taken. null if there are none.</summary>
        public DateTimeOffset? LatestBackupUtc { get; set; }

        /// <summary>
        /// When the housekeeping sweep last completed. Distinct from <see cref="LatestBackupUtc"/> on
        /// purpose — it tells "never ran" apart from "ran, but took no snapshot".
        /// </summary>
        public DateTimeOffset? LastSweepUtc { get; set; }
    }
}
