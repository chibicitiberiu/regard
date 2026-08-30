using Microsoft.Extensions.Logging;
using Quartz;
using Regard.Backend.Common.Model;
using Regard.Backend.Common.Providers;
using Regard.Backend.Common.Services;
using Regard.Backend.DB;
using Regard.Backend.Model;
using Regard.Backend.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Jobs
{
    /// <summary>
    /// Finishes creating a subscription: resolves its provider, fetches its real name/description/artwork,
    /// re-checks for duplicates now that the provider's own id is known, and kicks off the first sync.
    ///
    /// All of this used to happen on the create request thread, where it blocked the Add dialog for
    /// minutes (two identical yt-dlp extractions, a blocking HTML scrape, and throttle pacing shared with
    /// background syncs). Moving it here is the whole point: the UI gets a row immediately, and the live
    /// change feed fills in the real details a few seconds later.
    ///
    /// Marked resumable because it is idempotent — every step either no-ops or overwrites with the same
    /// values, and it bails out if the row it was created for has since been deleted.
    /// </summary>
    [ResumeAfterRestart]
    public class ResolveSubscriptionJob : JobBase
    {
        private static readonly string Data_SubscriptionId = "SubscriptionId";
        private static readonly string Data_AllowDuplicate = "AllowDuplicate";

        private readonly IProviderManager providerManager;
        private readonly SubscriptionManager subscriptionManager;
        private readonly NotificationService notificationService;
        private readonly IYoutubeDlService ytdlService;

        public ResolveSubscriptionJob(ILogger<ResolveSubscriptionJob> logger,
                                      DataContext dataContext,
                                      JobTrackerService jobTrackerService,
                                      IProviderManager providerManager,
                                      SubscriptionManager subscriptionManager,
                                      NotificationService notificationService,
                                      IYoutubeDlService ytdlService)
            : base(logger, dataContext, jobTrackerService)
        {
            this.providerManager = providerManager;
            this.subscriptionManager = subscriptionManager;
            this.notificationService = notificationService;
            this.ytdlService = ytdlService;
        }

        /// <summary>
        /// Waits for yt-dlp to be ready before running.
        ///
        /// This job is [ResumeAfterRestart], so after a restart it re-fires straight away — which can
        /// easily beat YoutubeDLService.Initialize (it probes versions and impersonation targets by
        /// spawning processes). Without this guard the provider throws "YoutubeDL not yet downloaded!"
        /// on a perfectly good subscription. Deferring costs a few seconds; the alternative is a
        /// spurious failure every time the server restarts with a half-created subscription pending.
        /// </summary>
        protected override Task<DateTimeOffset?> ShouldDefer(IJobExecutionContext context)
        {
            if (ytdlService.CurrentVersion == null)
            {
                JobLog("Waiting for yt-dlp to finish starting up…");
                return Task.FromResult<DateTimeOffset?>(DateTimeOffset.UtcNow.AddSeconds(15));
            }

            return Task.FromResult<DateTimeOffset?>(null);
        }

        /// <summary>
        /// Scheduled with <c>retryCount: 0</c> deliberately. The one case that deletes the row is
        /// definitive (no provider handles the URL), so retrying it would just fail against a row that no
        /// longer exists; every other failure keeps the subscription, which the user can retry or remove.
        /// There is also a known bug where RetryCount never actually decrements (see BACKLOG.md), so
        /// asking for retries here would mean unbounded ones.
        /// </summary>
        public static Task Schedule(RegardScheduler scheduler, Subscription subscription, bool allowDuplicate)
        {
            return scheduler.Schedule<ResolveSubscriptionJob>(
                name: $"Setting up subscription {subscription.Name}",
                jobData: new Dictionary<string, object>()
                {
                    { Data_SubscriptionId, subscription.Id },
                    { Data_AllowDuplicate, allowDuplicate },
                },
                retryCount: 0,
                retryIntervalSecs: 0);
        }

        protected override JobNotification GetOngoingNotification()
            => new JobNotification { Title = "Adding subscription", Text = Job?.Name };

        protected override JobNotification GetSuccessNotification() => null;   // the tree updating is the feedback

        protected override async Task ExecuteJob(IJobExecutionContext context)
        {
            int subId = ReadInt(Data_SubscriptionId);
            bool allowDuplicate = ReadBool(Data_AllowDuplicate);

            var sub = dataContext.Subscriptions.Find(subId);
            if (sub == null)
            {
                // Deleted while we were queued (or by a previous failed run of this same job, which is
                // possible after a restart-resume). Nothing to do, and not an error.
                JobLog($"Subscription {subId} no longer exists — nothing to resolve.");
                return;
            }

            var uri = new Uri(sub.OriginalUrl);

            // One extraction, not two. When creation already identified the provider from the URL, go
            // straight to CreateSubscription; FindFromSubscriptionUrl would repeat the identical yt-dlp
            // call purely to answer a question we can already answer.
            ISubscriptionProvider provider = sub.SubscriptionProviderId != null
                ? providerManager.Get<ISubscriptionProvider>(sub.SubscriptionProviderId)
                : await providerManager.FindFromSubscriptionUrl(uri).FirstOrDefaultAsync();

            if (provider == null)
            {
                // Definitive: no provider claims this URL, so it was never going to become a
                // subscription. Safe to remove the placeholder.
                await FailAndRemove(sub, "Unsupported service or URL format.");
                return;
            }

            Subscription resolved;
            try
            {
                resolved = await provider.CreateSubscription(uri);
            }
            catch (Exception ex)
            {
                // Anything that throws here is treated as TRANSIENT and the row is kept.
                //
                // Learned the hard way: after a restart this job resumes immediately and can beat
                // YoutubeDLService.Initialize, which throws "YoutubeDL not yet downloaded!". Deleting on
                // that destroyed a subscription the user had legitimately created, for a condition that
                // resolves itself seconds later. A rate limit, a network blip or a temporarily
                // unreachable channel are all the same shape. A leftover placeholder row is a far
                // cheaper mistake than silently discarding someone's subscription, and they can retry or
                // delete it themselves.
                await FailAndKeep(sub, ex.Message);
                return;
            }

            if (resolved == null)
            {
                await FailAndKeep(sub, "The provider returned no information for this URL.");
                return;
            }

            // The authoritative duplicate check: the same channel resolves to the same provider id no
            // matter which URL form was pasted. Unlike creation, this can't offer "create anyway" — a
            // bell card has no buttons for that — so the subscription is KEPT and the user is told. They
            // asked for it; silently deleting it would throw away an explicit action.
            if (!allowDuplicate && resolved.SubscriptionId != null)
            {
                var existing = dataContext.Subscriptions.AsQueryable()
                    .Where(x => x.UserId == sub.UserId)
                    .Where(x => x.Id != sub.Id)
                    .Where(x => x.SubscriptionProviderId == resolved.SubscriptionProviderId)
                    .Where(x => x.SubscriptionId == resolved.SubscriptionId)
                    .FirstOrDefault();

                if (existing != null)
                {
                    JobLog($"This is also \"{existing.Name}\", which you're already subscribed to. Keeping both.");
                    _ = notificationService.PostOrUpdate(
                        sub.UserId, $"subscription-duplicate:{sub.Id}",
                        "Already subscribed",
                        $"\"{resolved.Name}\" duplicates \"{existing.Name}\". Both were kept — delete either if you meant only one.",
                        NotificationSeverity.Warning, progress: null, ongoing: false);
                }
            }

            // Copy the whole set the provider produced. OriginalUrl matters as much as the name: the
            // provider rewrites channel URLs into their "uploads" form, and FetchVideos reads it on every
            // later sync. SubscriptionProviderId matters for URLs the hint couldn't classify — without it
            // SynchronizeJob silently skips the subscription forever.
            sub.Name = resolved.Name ?? sub.Name;
            sub.Description = resolved.Description;
            sub.ThumbnailPath = resolved.ThumbnailPath;
            sub.SubscriptionId = resolved.SubscriptionId;
            sub.SubscriptionProviderId = resolved.SubscriptionProviderId ?? sub.SubscriptionProviderId;
            if (!string.IsNullOrEmpty(resolved.OriginalUrl))
                sub.OriginalUrl = resolved.OriginalUrl;

            await dataContext.SaveChangesAsync();   // the change feed pushes the rename + artwork live
            JobLog($"Resolved as \"{sub.Name}\".");

            await subscriptionManager.ScheduleThumbnailFetch();
            await subscriptionManager.SynchronizeSubscription(sub);
        }

        /// <summary>
        /// A URL that never resolved never became a real subscription, so the placeholder row is removed
        /// rather than left as a permanent "https://…" ghost in the tree. The user is told why.
        /// </summary>
        private async Task FailAndRemove(Subscription sub, string reason)
        {
            JobLog($"Could not set up this subscription: {reason}", MessageSeverity.Error);

            var name = sub.Name;
            var userId = sub.UserId;

            dataContext.Subscriptions.Remove(sub);
            await dataContext.SaveChangesAsync();

            _ = notificationService.PostOrUpdate(
                userId, $"subscription-failed:{sub.Id}",
                "Couldn't add subscription",
                $"{name}: {reason}",
                NotificationSeverity.Error, progress: null, ongoing: false);
        }

        /// <summary>
        /// Something went wrong that might not be permanent, so the subscription stays. It keeps its
        /// placeholder name and its hinted provider, which means syncing still works — only the pretty
        /// name and artwork are missing until it's resolved again.
        /// </summary>
        private Task FailAndKeep(Subscription sub, string reason)
        {
            JobLog($"Could not finish setting up \"{sub.Name}\": {reason}. The subscription was kept.",
                   MessageSeverity.Warning);

            _ = notificationService.PostOrUpdate(
                sub.UserId, $"subscription-unresolved:{sub.Id}",
                "Subscription needs attention",
                $"Couldn't fetch details for \"{sub.Name}\": {reason}",
                NotificationSeverity.Warning, progress: null, ongoing: false);

            return Task.CompletedTask;
        }

        private int ReadInt(string key)
            => Job.JobData.TryGetValue(key, out var v) && v != null ? Convert.ToInt32(v) : 0;

        private bool ReadBool(string key)
        {
            try
            {
                return Job.JobData.TryGetValue(key, out var v) && v != null && Convert.ToBoolean(v);
            }
            catch
            {
                return false;
            }
        }
    }
}
