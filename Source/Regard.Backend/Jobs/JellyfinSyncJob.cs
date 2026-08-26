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
    /// Polls Jellyfin for videos the configured user has marked played and marks the matching
    /// Regard videos watched (which, per subscription settings, deletes the file and refills the
    /// download window). One-way (Jellyfin -> Regard) and reconciling: every poll recomputes truth,
    /// so it's idempotent. Videos are matched by full file path, so the download volume must be
    /// mounted identically in both containers and DownloadDirectory must be absolute.
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

            var playedItems = await client.GetPlayedItemsAsync(userId);
            var playedPaths = playedItems
                .Where(i => !string.IsNullOrEmpty(i.Path))
                .Select(i => Path.GetFullPath(Path.ChangeExtension(i.Path, null)))
                .ToHashSet();

            if (playedPaths.Count == 0)
            {
                log.LogInformation("Jellyfin sync: no played items reported for user '{0}'.", jellyfinUser);
                return;
            }

            // Match against the user's downloaded, unwatched videos by normalized full path.
            var candidates = dataContext.Videos
                .Where(v => v.Subscription.UserId == user.Id && v.DownloadedPath != null && !v.IsWatched)
                .ToList();

            var matchedIds = candidates
                .Where(v => playedPaths.Contains(Path.GetFullPath(v.DownloadedPath)))
                .Select(v => v.Id)
                .ToArray();

            if (matchedIds.Length == 0)
            {
                log.LogInformation("Jellyfin sync: {0} played item(s), none matched an unwatched download.", playedPaths.Count);
                return;
            }

            await videoManager.MarkWatched(user, matchedIds);
            log.LogInformation("Jellyfin sync: marked {0} video(s) watched (of {1} played item(s)).",
                matchedIds.Length, playedPaths.Count);
        }
    }
}
