using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Quartz;
using Regard.Backend.Common.Model;
using Regard.Backend.Common.Utils;
using Regard.Backend.Configuration;
using Regard.Backend.DB;
using Regard.Backend.Model;
using Regard.Backend.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Regard.Backend.Jobs
{
    /// <summary>
    /// Bulk-adds subscriptions from a parsed import tree. Walks the tree, mirroring OPML folder
    /// groups as Regard folders and calling <see cref="SubscriptionManager.Create"/> per feed, with
    /// live progress in the bell and per-URL results in the Job Log. One slow (network-bound) add per
    /// feed; runs serially under the single Quartz worker.
    /// </summary>
    // Resume after a restart: no periodic backstop re-runs an import, and a re-run skips feeds already
    // added (DuplicateSubscriptionException), so finishing an interrupted import from its row is safe.
    [ResumeAfterRestart]
    public class ImportSubscriptionsJob : JobBase
    {
        public static readonly string Data_Tree = "Tree";
        public static readonly string Data_ParentFolderId = "ParentFolderId";
        public static readonly string Data_AllowDuplicate = "AllowDuplicate";
        public static readonly string Data_AutoDownload = "AutoDownload";

        private readonly SubscriptionManager subscriptionManager;
        private readonly IOptionManager optionManager;
        private readonly UserManager<UserAccount> userManager;

        private int total;
        private int done;
        private int added;
        private int skipped;
        private int failed;

        public ImportSubscriptionsJob(ILogger<ImportSubscriptionsJob> log,
                                      DataContext dataContext,
                                      JobTrackerService jobTrackerService,
                                      SubscriptionManager subscriptionManager,
                                      IOptionManager optionManager,
                                      UserManager<UserAccount> userManager)
            : base(log, dataContext, jobTrackerService)
        {
            this.subscriptionManager = subscriptionManager;
            this.optionManager = optionManager;
            this.userManager = userManager;
        }

        public static Task<DateTimeOffset> Schedule(RegardScheduler scheduler,
                                                    string userId,
                                                    string treeJson,
                                                    int? parentFolderId,
                                                    bool allowDuplicate,
                                                    bool autoDownload)
        {
            return scheduler.Schedule<ImportSubscriptionsJob>(
                name: "Import subscriptions",
                userId: userId,
                jobData: new Dictionary<string, object>
                {
                    [Data_Tree] = treeJson,
                    [Data_ParentFolderId] = parentFolderId,
                    [Data_AllowDuplicate] = allowDuplicate,
                    [Data_AutoDownload] = autoDownload,
                },
                retryCount: 0,
                retryIntervalSecs: 0);
        }

        // "Import complete" — a plain informative terminal notification (no click action).
        protected override JobNotification GetSuccessNotification()
            => new JobNotification
            {
                Title = "Import complete",
                Text = $"Imported {added} subscription{(added == 1 ? "" : "s")}"
                     + (skipped + failed > 0 ? $" ({skipped} skipped, {failed} failed)" : ""),
            };

        protected override async Task ExecuteJob(IJobExecutionContext context)
        {
            var treeJson = Job.JobData.TryGetValue(Data_Tree, out var t) ? t?.ToString() : null;
            if (string.IsNullOrEmpty(treeJson))
            {
                JobLog("Nothing to import.", MessageSeverity.Warning);
                return;
            }

            var root = JsonUtils.Deserialize<ImportNode>(treeJson);

            // jobData round-trips through JSON, so numbers come back boxed as long — coerce safely.
            int? parentFolderId = Job.JobData.TryGetValue(Data_ParentFolderId, out var p) && p != null
                ? Convert.ToInt32(p) : (int?)null;
            bool allowDuplicate = Job.JobData.TryGetValue(Data_AllowDuplicate, out var a) && a != null && Convert.ToBoolean(a);
            bool autoDownload = Job.JobData.TryGetValue(Data_AutoDownload, out var d) && d != null && Convert.ToBoolean(d);

            var user = await userManager.FindByIdAsync(Job.UserId);
            if (user == null)
            {
                JobLog("Import failed: could not resolve the user.", MessageSeverity.Error);
                throw new Exception("User not found for import job.");
            }

            total = SubscriptionImportParser.CountFeeds(root);
            JobLog($"Importing {total} subscription(s)…");

            await ImportChildren(user, root, parentFolderId, allowDuplicate, autoDownload);

            // One thumbnail-cache pass for the whole batch (creates skipped their own).
            if (added > 0)
                await subscriptionManager.ScheduleThumbnailFetch();

            JobLog($"Import finished: {added} added, {skipped} skipped (duplicates), {failed} failed.");
        }

        private async Task ImportChildren(UserAccount user, ImportNode node, int? parentFolderId, bool allowDuplicate, bool autoDownload)
        {
            if (node.Children == null)
                return;

            foreach (var child in node.Children)
            {
                if (child.IsFolder)
                {
                    int? folderId = parentFolderId;
                    try
                    {
                        var name = string.IsNullOrWhiteSpace(child.Title) ? "Imported" : child.Title;
                        var folder = subscriptionManager.GetOrCreateFolder(user, name, parentFolderId);
                        folderId = folder.Id;
                        JobLog($"Folder: {folder.Name}");
                    }
                    catch (Exception ex)
                    {
                        JobLog($"Could not create folder '{child.Title}': {ex.Message}", MessageSeverity.Warning);
                    }
                    await ImportChildren(user, child, folderId, allowDuplicate, autoDownload);
                }
                else
                {
                    await ImportFeed(user, child, parentFolderId, allowDuplicate, autoDownload);
                }
            }
        }

        private async Task ImportFeed(UserAccount user, ImportNode feed, int? parentFolderId, bool allowDuplicate, bool autoDownload)
        {
            done++;
            ReportProgress(total > 0 ? (float)done / total : 0f, $"Adding {feed.Title ?? feed.Url}");

            Uri uri;
            try
            {
                uri = new Uri(feed.Url);
            }
            catch
            {
                failed++;
                JobLog($"Invalid URL: {feed.Url}", MessageSeverity.Error);
                return;
            }

            try
            {
                var sub = await subscriptionManager.Create(user, uri, parentFolderId, allowDuplicate, scheduleThumbnailFetch: false);
                optionManager.SetForSubscription(Options.Subscriptions_AutoDownload, sub.Id, autoDownload);
                added++;
                JobLog($"Added: {sub.Name}");
            }
            catch (DuplicateSubscriptionException)
            {
                skipped++;
                JobLog($"Skipped duplicate: {feed.Url}");
            }
            catch (Exception ex)
            {
                failed++;
                JobLog($"Failed: {feed.Url} — {ex.Message}", MessageSeverity.Error);
            }
        }
    }
}
