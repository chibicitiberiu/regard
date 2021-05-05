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

        public DbSet<ProviderConfiguration> ProviderConfigurations { get; set; }

        public DbSet<SubscriptionFolder> SubscriptionFolders { get; set; }

        public DbSet<Subscription> Subscriptions { get; set; }

        public DbSet<Video> Videos { get; set; }

        public DbSet<Option> Options { get; set; }

        public DbSet<UserOption> UserOptions { get; set; }

        public DbSet<SubscriptionOption> SubscriptionOptions { get; set; }

        public DbSet<SubscriptionFolderOption> FolderOptions { get; set; }

        public DbSet<Message> Messages { get; set; }

        protected DataContext(IConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Video>()
                .HasOne(x => x.Subscription).WithMany()
                .HasForeignKey(x => x.SubscriptionId)
                .IsRequired(true)
                .OnDelete(DeleteBehavior.Cascade);

            // Subscriptions
            modelBuilder.Entity<Subscription>()
                .HasOne(x => x.ParentFolder).WithMany()
                .HasForeignKey(x => x.ParentFolderId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Subscription>()
                .HasOne(x => x.User).WithMany()
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

            // Options
            modelBuilder.Entity<Option>()
                .HasKey(c => new { c.Key });

            modelBuilder.Entity<UserOption>()
                .HasKey(c => new { c.Key, c.UserId });

            modelBuilder.Entity<UserOption>()
                .HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId)
                .IsRequired(true)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SubscriptionOption>()
                .HasKey(c => new { c.Key, c.SubscriptionId });

            modelBuilder.Entity<SubscriptionOption> ()
                .HasOne(x => x.Subscription).WithMany()
                .HasForeignKey(x => x.SubscriptionId)
                .IsRequired(true)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SubscriptionFolderOption>()
                .HasKey(c => new { c.Key, c.SubscriptionFolderId });

            modelBuilder.Entity<SubscriptionFolderOption>()
                .HasOne(x => x.SubscriptionFolder).WithMany()
                .HasForeignKey(x => x.SubscriptionFolderId)
                .IsRequired(true)
                .OnDelete(DeleteBehavior.Cascade);

            // Messages
            modelBuilder.Entity<Message>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .IsRequired(false)
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
