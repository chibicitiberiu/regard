using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Configuration;
using MoreLinq;
using Regard.Backend.Common.Model;
using Regard.Backend.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.DB
{
    public class DataContext : IdentityDbContext<UserAccount>
    {
        protected readonly IConfiguration Configuration;

        public DbSet<Preference> Preferences { get; set; }

        public DbSet<UserPreference> UserPreferences { get; set; }

        public DbSet<ProviderConfiguration> ProviderConfigurations { get; set; }

        public DbSet<SubscriptionFolder> SubscriptionFolders { get; set; }

        public DbSet<Subscription> Subscriptions { get; set; }

        public DbSet<Video> Videos { get; set; }

        public DbSet<Message> Messages { get; set; }

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

            modelBuilder.Entity<Message>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Video>()
                .HasOne(x => x.Subscription)
                .WithMany()
                .HasForeignKey(x => x.SubscriptionId)
                .IsRequired(true)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Subscription>()
                .HasOne(x => x.ParentFolder)
                .WithMany()
                .HasForeignKey(x => x.ParentFolderId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Subscription>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .IsRequired(true)
                .OnDelete(DeleteBehavior.Restrict);

            // cannot have OnDelete=SetNull here, because it may cause cycles
            modelBuilder.Entity<SubscriptionFolder>()
                .HasOne(x => x.Parent)
                .WithMany()
                .HasForeignKey(x => x.ParentId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SubscriptionFolder>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .IsRequired(true)
                .OnDelete(DeleteBehavior.Cascade);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.UseLazyLoadingProxies();
        }

        public IQueryable<Subscription> GetSubscriptionsRecursive(SubscriptionFolder root)
        {
            var folderIds = new HashSet<int>();

            var queue = new Queue<SubscriptionFolder>();
            queue.Enqueue(root);

            // Build set of subfolders
            while (queue.TryDequeue(out SubscriptionFolder current))
            {
                if (folderIds.Contains(current.Id))
                {
                    Debug.Fail($"Folder cycle detected for id {current.Id}!!!");
                    continue;
                }
                folderIds.Add(current.Id);

                SubscriptionFolders.AsQueryable()
                    .Where(f => f.ParentId == current.Id)
                    .ForEach(queue.Enqueue);
            }

            // Get subscriptions
            return Subscriptions.AsQueryable()
                .Where(x => x.ParentFolderId.HasValue && folderIds.Contains(x.ParentFolderId.Value));
        }

        public IEnumerable<SubscriptionFolder> GetFoldersRecursive(SubscriptionFolder root)
        {
            var folderIds = new HashSet<int>();

            var queue = new Queue<SubscriptionFolder>();
            queue.Enqueue(root);

            // Build set of subfolders
            while (queue.TryDequeue(out SubscriptionFolder current))
            {
                if (folderIds.Contains(current.Id))
                {
                    Debug.Fail($"Folder cycle detected for id {current.Id}!!!");
                    continue;
                }
                folderIds.Add(current.Id);

                yield return current;

                SubscriptionFolders.AsQueryable()
                    .Where(f => f.ParentId == current.Id)
                    .ForEach(queue.Enqueue);
            }
        }
    }
}
