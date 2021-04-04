using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.DB
{
    public class SQLServerDataContext : DataContext
    {
        public SQLServerDataContext(IConfiguration configuration) : base(configuration) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(Configuration.GetConnectionString("SqlServer"));
        }
    }

    public class SQLServerDataContextFactory : IDesignTimeDbContextFactory<SQLServerDataContext>
    {
        public SQLServerDataContext CreateDbContext(string[] args)
        {
            var dict = new Dictionary<string, string>()
            {
                { "ConnectionStrings:SqlServer", "Data Source=localhost\\SQLEXPRESS;Initial Catalog=Regard;Integrated Security=True;Pooling=False" }
            };

            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(dict)
                .Build();


            return new SQLServerDataContext(config);
        }
    }
}
