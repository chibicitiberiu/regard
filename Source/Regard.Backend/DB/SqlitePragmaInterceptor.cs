using Microsoft.EntityFrameworkCore.Diagnostics;
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
    /// </summary>
    public class SqlitePragmaInterceptor : DbConnectionInterceptor
    {
        public static readonly SqlitePragmaInterceptor Instance = new SqlitePragmaInterceptor();

        private const string Pragmas =
            "PRAGMA busy_timeout=30000; PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";

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
