using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Regard.Backend.Model;
using RegardBackend.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace RegardBackend.DB
{
    public class DataContext : IdentityDbContext<UserAccount>
    {
        protected readonly IConfiguration Configuration;

        public DbSet<Preference> Preferences { get; set; }

        public DbSet<UserPreference> UserPreferences { get; set; }

        public DbSet<SubscriptionFolder> SubscriptionFolders { get; set; }

        public DbSet<Subscription> Subscriptions { get; set; }

        public DbSet<Video> Videos { get; set; }

        protected DataContext(IConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Preference>()
                .HasKey(c => new { c.Key });

            modelBuilder.Entity<UserPreference>()
                .HasKey(c => new { c.Key, c.UserId });

            modelBuilder.Entity<UserPreference>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
