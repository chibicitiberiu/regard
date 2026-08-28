using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quartz;
using Regard.Backend.DB;
using Regard.Backend.Jellyfin;
using Regard.Backend.Model;
using Regard.Backend.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Jobs
{
    /// <summary>
    /// Two-way sync between Jellyfin and Regard for watched state and resume position. Jellyfin's played
    /// items mark the matching Regard videos watched (which, per subscription settings, deletes the file
    /// and refills the download window); resume positions are reconciled newer-wins (Jellyfin's
    /// UserData.LastPlayedDate vs Regard's PlaybackPositionUpdated) and pushed back to Jellyfin when Regard
    /// is ahead. Reconciling and idempotent: every poll recomputes truth. Videos are matched by full file
    /// path, so the download volume must be mounted identically in both containers and DownloadDirectory
    /// must be absolute.
    /// </summary>
    public class JellyfinSyncJob : JobBase
    {
        private readonly IConfiguration configuration;
        private readonly VideoManager videoManager;
        private readonly UserManager<UserAccount> userManager;
        private readonly IJellyfinClient client;

        public JellyfinSyncJob(ILogger<JellyfinSyncJob> log,
                               DataContext dataContext,
                               JobTrackerService jobTrackerService,
                               IConfiguration configuration,
                               VideoManager videoManager,
                               UserManager<UserAccount> userManager,
                               IJellyfinClient client) : base(log, dataContext, jobTrackerService)
        {
            this.configuration = configuration;
            this.videoManager = videoManager;
            this.userManager = userManager;
            this.client = client;
        }

        public static Task<DateTimeOffset> Schedule(RegardScheduler scheduler, string cron)
        {
            return scheduler.Schedule<JellyfinSyncJob>(
                cronSchedule: cron,
                name: "Jellyfin watched sync",
                retryCount: 0,
                retryIntervalSecs: 0);
        }

        protected override async Task ExecuteJob(IJobExecutionContext context)
        {
            // Validate configuration.
            var baseUrl = configuration["Jellyfin:BaseUrl"];
            var apiKey = configuration["Jellyfin:ApiKey"];
            var jellyfinUser = configuration["Jellyfin:JellyfinUser"];
            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(jellyfinUser))
            {
                log.LogWarning("Jellyfin sync skipped: BaseUrl, ApiKey and JellyfinUser must all be configured.");
                return;
            }
            if (!Path.IsPathFullyQualified(configuration["DownloadDirectory"] ?? string.Empty))
            {
                log.LogWarning("Jellyfin sync skipped: DownloadDirectory must be an absolute path to match Jellyfin's file paths.");
                return;
            }

            // Resolve the Regard user that owns the videos.
            var regardUserName = configuration["Jellyfin:RegardUser"];
            if (string.IsNullOrWhiteSpace(regardUserName))
                regardUserName = jellyfinUser;
            var user = await userManager.FindByNameAsync(regardUserName);
            if (user == null)
            {
                log.LogWarning("Jellyfin sync skipped: Regard user '{0}' not found.", regardUserName);
                return;
            }

            // Resolve the Jellyfin user and fetch its played items.
            var userId = await client.ResolveUserIdAsync(jellyfinUser);
            if (userId == null)
            {
                log.LogWarning("Jellyfin sync skipped: Jellyfin user '{0}' not found.", jellyfinUser);
                return;
            }

            var jfItems = await client.GetItemsWithUserDataAsync(userId);

            // Index Jellyfin items by normalized full path (extension stripped), same key the match uses.
            var jfByPath = new Dictionary<string, JellyfinItem>();
            foreach (var it in jfItems)
            {
                if (string.IsNullOrEmpty(it.Path))
                    continue;
                jfByPath[Path.GetFullPath(Path.ChangeExtension(it.Path, null))] = it;
            }

            if (jfByPath.Count == 0)
            {
                log.LogInformation("Jellyfin sync: no items reported for user '{0}'.", jellyfinUser);
                return;
            }

            // The user's downloaded videos (watched ones included, so Regard->Jellyfin can push played).
            var candidates = dataContext.Videos
                .Where(v => v.Subscription.UserId == user.Id && v.DownloadedPath != null)
                .ToList();

            var markWatchedIds = new List<int>();
            var adopts = new List<(int Id, int Seconds, DateTimeOffset Timestamp)>();
            var pushes = new List<(string ItemId, long Ticks, bool Played)>();

            foreach (var v in candidates)
            {
                if (!jfByPath.TryGetValue(Path.GetFullPath(v.DownloadedPath), out var jf))
                    continue;
                var ud = jf.UserData;

                var decision = JellyfinReconciler.Reconcile(
                    v.IsWatched, v.PlaybackPositionSeconds, v.PlaybackPositionUpdated,
                    ud?.Played ?? false, ud?.PlaybackPositionTicks, ud?.LastPlayedDate);

                switch (decision.Action)
                {
                    case JellyfinSyncAction.MarkWatched:
                        markWatchedIds.Add(v.Id);
                        break;
                    case JellyfinSyncAction.AdoptPosition:
                        adopts.Add((v.Id, decision.PositionSeconds, decision.Timestamp));
                        break;
                    case JellyfinSyncAction.PushToJellyfin:
                        pushes.Add((jf.Id, decision.PushTicks, decision.PushPlayed));
                        break;
                }
            }

            if (markWatchedIds.Count > 0)
                await videoManager.MarkWatched(user, markWatchedIds.ToArray());

            foreach (var a in adopts)
                videoManager.SetPlaybackPosition(user, a.Id, a.Seconds, a.Timestamp);

            int pushed = 0;
            foreach (var pu in pushes)
                if (await client.UpdateUserDataAsync(userId, pu.ItemId, pu.Ticks, pu.Played))
                    pushed++;

            log.LogInformation("Jellyfin sync: {0} marked watched, {1} position(s) adopted, {2}/{3} pushed to Jellyfin.",
                markWatchedIds.Count, adopts.Count, pushed, pushes.Count);
        }
    }
}
