using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.DB
{
    public class SQLiteDataContext : DataContext
    {
        public SQLiteDataContext(IConfiguration configuration) : base(configuration)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var connectionString = Configuration?.GetConnectionString("SQLite");
            if (string.IsNullOrEmpty(connectionString))
            {
                // No explicit SQLite connection string: default to a file under DataDirectory.
                var dataDir = Configuration?["DataDirectory"];
                if (string.IsNullOrEmpty(dataDir))
                    dataDir = Directory.GetCurrentDirectory();
                Directory.CreateDirectory(dataDir);
                connectionString = $"Data Source={Path.Combine(dataDir, "Regard.db")}";
            }
            optionsBuilder.UseSqlite(connectionString);
            optionsBuilder.AddInterceptors(SqlitePragmaInterceptor.Instance);
        }

        // Retry SQLITE_BUSY/LOCKED as a backstop to the 30s busy_timeout pragma (which handles the common
        // case): once the job pool is > 1, concurrent writers can, pathologically, exceed the timeout.
        private static bool IsBusy(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
                if (e is Microsoft.Data.Sqlite.SqliteException se && (se.SqliteErrorCode == 5 || se.SqliteErrorCode == 6))
                    return true;
            return false;
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            for (int attempt = 0; ; attempt++)
            {
                try { return base.SaveChanges(acceptAllChangesOnSuccess); }
                catch (Exception ex) when (IsBusy(ex) && attempt < 2) { System.Threading.Thread.Sleep(150 * (attempt + 1)); }
            }
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
            System.Threading.CancellationToken cancellationToken = default)
        {
            for (int attempt = 0; ; attempt++)
            {
                try { return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
                catch (Exception ex) when (IsBusy(ex) && attempt < 2) { await Task.Delay(150 * (attempt + 1), cancellationToken); }
            }
        }
    }

    public class SQLiteDataContextFactory : IDesignTimeDbContextFactory<SQLiteDataContext>
    {
        public SQLiteDataContext CreateDbContext(string[] args)
        {
            var dict = new Dictionary<string, string>()
            {
                { "ConnectionStrings:SQLite", "Data Source=:memory:" }
            };

            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(dict)
                .Build();

            return new SQLiteDataContext(config);
        }
    }
}
