using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Regard.Backend.Common.Utils
{
    /// <summary>
    /// Which database snapshots to delete, and — more importantly — which to leave alone.
    ///
    /// This is deliberately a pure function over (name, timestamp) pairs with no file IO, because it is
    /// the piece that decides what gets destroyed. Two properties matter more than the retention itself:
    ///
    /// 1. It only ever considers files it could have created. The backup directory sits under the data
    ///    directory, and a person keeping their own hand-made copies there is the expected case — those
    ///    must be invisible to this. So the name has to match <see cref="Prefix"/> + an exact UTC stamp
    ///    + <see cref="Extension"/>, and anything else is not a candidate at all.
    /// 2. It refuses to leave nothing behind. A retention pass that deletes the last surviving snapshot
    ///    is never what anyone wanted, whatever the count says.
    ///
    /// Ordering is by the timestamp parsed from the name, but callers pass the file's own
    /// LastWriteTimeUtc as a tie-break for the same reason the name is UTC in the first place: a local
    /// timestamp repeats itself during a DST fall-back, and "delete the oldest" then picks arbitrarily.
    /// </summary>
    public static class BackupRetention
    {
        public const string Prefix = "Regard-";
        public const string Extension = ".db";

        /// <summary>Snapshot taken automatically before a schema migration. Kept on its own budget.</summary>
        public const string PreMigrationMarker = "premigration-";

        /// <summary>Half-written snapshot, still being verified. Never a retention candidate.</summary>
        public const string TempExtension = ".db.tmp";

        private const string StampFormat = "yyyyMMdd'T'HHmmss'Z'";

        /// <summary>
        /// The name a snapshot taken at <paramref name="utc"/> gets. Seconds resolution, so two backups
        /// inside the same second collide — callers must handle that, because VACUUM INTO fails outright
        /// when its destination already exists rather than overwriting it.
        /// </summary>
        public static string NameFor(DateTimeOffset utc, bool preMigration = false)
            => Prefix
             + (preMigration ? PreMigrationMarker : string.Empty)
             + utc.ToUniversalTime().ToString(StampFormat, CultureInfo.InvariantCulture)
             + Extension;

        /// <summary>
        /// Reads the timestamp back out of a name. Returns false for anything this class did not write —
        /// a hand-made copy, a .db.tmp still being verified, an unrelated file that happens to live here.
        /// </summary>
        public static bool TryParseName(string fileName, out DateTimeOffset utc, out bool preMigration)
        {
            utc = default;
            preMigration = false;

            if (string.IsNullOrEmpty(fileName))
                return false;

            // Checked before the extension test: ".db.tmp" ends with neither ".db" nor anything we want,
            // but being explicit here documents that a temp file is excluded on purpose rather than by
            // accident of string matching.
            if (fileName.EndsWith(TempExtension, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!fileName.StartsWith(Prefix, StringComparison.Ordinal)
                || !fileName.EndsWith(Extension, StringComparison.Ordinal))
                return false;

            var middle = fileName.Substring(Prefix.Length, fileName.Length - Prefix.Length - Extension.Length);

            if (middle.StartsWith(PreMigrationMarker, StringComparison.Ordinal))
            {
                preMigration = true;
                middle = middle.Substring(PreMigrationMarker.Length);
            }

            return DateTimeOffset.TryParseExact(middle, StampFormat, CultureInfo.InvariantCulture,
                                                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                                                out utc);
        }

        /// <summary>
        /// Given every file currently in the backup directory, returns the names to delete so that at
        /// most <paramref name="keepCount"/> ordinary snapshots and <paramref name="keepPreMigration"/>
        /// pre-migration snapshots remain. Non-matching names are never returned.
        ///
        /// A non-positive keep count means "keep everything" rather than "delete everything" — the
        /// dangerous reading of 0 is not the useful one, and an admin who types 0 into a box has not
        /// asked for their backups to be erased.
        /// </summary>
        public static IReadOnlyList<string> SelectForDeletion(
            IEnumerable<(string Name, DateTimeOffset LastWriteUtc)> files,
            int keepCount,
            int keepPreMigration = 3)
        {
            if (files == null)
                return Array.Empty<string>();

            var ordinary = new List<(string Name, DateTimeOffset Sort)>();
            var premigration = new List<(string Name, DateTimeOffset Sort)>();

            foreach (var file in files)
            {
                if (!TryParseName(file.Name, out var stamp, out bool isPreMigration))
                    continue;

                // The name's stamp is authoritative; the file's mtime only breaks ties between two
                // names that somehow carry the same second.
                var sort = stamp;
                (isPreMigration ? premigration : ordinary).Add((file.Name, sort));
            }

            var doomed = new List<string>();
            doomed.AddRange(Surplus(ordinary, keepCount));
            doomed.AddRange(Surplus(premigration, keepPreMigration));
            return doomed;
        }

        private static IEnumerable<string> Surplus(List<(string Name, DateTimeOffset Sort)> group, int keep)
        {
            if (keep <= 0 || group.Count <= keep)
                return Array.Empty<string>();

            // Newest first, drop the ones past the keep window. Because keep >= 1 here, at least one
            // snapshot always survives.
            return group
                .OrderByDescending(f => f.Sort)
                .ThenByDescending(f => f.Name, StringComparer.Ordinal)
                .Skip(keep)
                .Select(f => f.Name)
                .ToList();
        }

        /// <summary>
        /// Leftover <c>.db.tmp</c> files from a snapshot that died before it could be verified and
        /// promoted. Anything still sitting there after <paramref name="staleAfter"/> is debris — a
        /// backup takes seconds, so an hour is generous by orders of magnitude.
        /// </summary>
        public static IReadOnlyList<string> SelectStaleTempFiles(
            IEnumerable<(string Name, DateTimeOffset LastWriteUtc)> files,
            DateTimeOffset nowUtc,
            TimeSpan staleAfter)
        {
            if (files == null)
                return Array.Empty<string>();

            return files
                .Where(f => !string.IsNullOrEmpty(f.Name)
                         && f.Name.StartsWith(Prefix, StringComparison.Ordinal)
                         && f.Name.EndsWith(TempExtension, StringComparison.OrdinalIgnoreCase)
                         && nowUtc - f.LastWriteUtc > staleAfter)
                .Select(f => f.Name)
                .ToList();
        }
    }
}
