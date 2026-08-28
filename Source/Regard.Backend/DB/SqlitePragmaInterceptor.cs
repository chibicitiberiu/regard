using Microsoft.EntityFrameworkCore.Diagnostics;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Regard.Backend.DB
{
    /// <summary>
    /// Applies SQLite PRAGMAs on every opened connection so the app's concurrent jobs and
    /// background threads don't hit "database is locked" / disk I/O errors:
    /// WAL journalling (concurrent readers + one writer), a busy timeout (writers wait for
    /// locks instead of failing), and synchronous=NORMAL (safe and fast under WAL).
    ///
    /// journal_mode defaults to WAL, which is the right choice for a local disk. It can be overridden
    /// with the REGARD_SQLITE_JOURNAL_MODE env var (WAL/DELETE/TRUNCATE/PERSIST/MEMORY/OFF) for the rare
    /// deployment whose DB lives on a filesystem that can't do WAL's shared-memory locking — some network
    /// shares — where WAL surfaces as SQLITE_IOERR and DELETE/TRUNCATE work instead. Leave it unset on a
    /// normal local disk.
    /// </summary>
    public class SqlitePragmaInterceptor : DbConnectionInterceptor
    {
        public static readonly SqlitePragmaInterceptor Instance = new SqlitePragmaInterceptor();

        private static readonly string Pragmas =
            $"PRAGMA busy_timeout=30000; PRAGMA journal_mode={ResolveJournalMode()}; PRAGMA synchronous=NORMAL;";

        private static string ResolveJournalMode()
        {
            var mode = Environment.GetEnvironmentVariable("REGARD_SQLITE_JOURNAL_MODE")?.Trim().ToUpperInvariant();
            var allowed = new HashSet<string> { "WAL", "DELETE", "TRUNCATE", "PERSIST", "MEMORY", "OFF" };
            return (!string.IsNullOrEmpty(mode) && allowed.Contains(mode)) ? mode : "WAL";
        }

        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        {
            Apply(connection);
            base.ConnectionOpened(connection, eventData);
        }

        public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            Apply(connection);
            await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
        }

        private static void Apply(DbConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = Pragmas;
            cmd.ExecuteNonQuery();
        }
    }
}
