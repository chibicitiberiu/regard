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
            optionsBuilder.UseSqlite(Configuration?.GetConnectionString("SQLite"));
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
