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
