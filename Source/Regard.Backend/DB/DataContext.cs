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

        public DbSet<SubscriptionFilter> SubscriptionFilters { get; set; }

        public DbSet<Option> Options { get; set; }

        public DbSet<UserOption> UserOptions { get; set; }

        public DbSet<SubscriptionOption> SubscriptionOptions { get; set; }

        public DbSet<SubscriptionFolderOption> FolderOptions { get; set; }

        public DbSet<Message> Messages { get; set; }

        public DbSet<JobInfo> Jobs { get; set; }

        public DbSet<Notification> Notifications { get; set; }

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

            modelBuilder.Entity<SubscriptionFilter>()
                .HasOne(x => x.Subscription).WithMany(x => x.Filters)
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

            // Cascade so pruning an old job clears its linked messages instead of throwing an FK error.
            modelBuilder.Entity<Message>()
                .HasOne(x => x.Job)
                .WithMany()
                .HasForeignKey(x => x.JobId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            // Jobs
            modelBuilder.Entity<JobInfo>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            // Notifications. VideoDbId / JobId are deliberately plain scalars (no FK) so a notification
            // outlives the video or job it points at (the click targets just tolerate a 404). Index on
            // (UserId, Key) backs the upsert lookup; not unique because UserId is nullable (ownerless
            // system notifications) and the app-level upsert is the real dedup.
            modelBuilder.Entity<Notification>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Notification>()
                .HasIndex(x => new { x.UserId, x.Key });
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
