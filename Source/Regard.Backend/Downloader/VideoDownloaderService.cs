using Regard.Backend.Common.Utils;
using Regard.Backend.Configuration;
using Regard.Backend.DB;
using Regard.Backend.Model;
using Regard.Backend.Services;
using Regard.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Downloader
{
    public class VideoDownloaderService : IVideoDownloaderService
    {
        class VideoState 
        {
            internal VideoDownloadState State { get; set; }
            internal float? Progress { get; set; }
        }

        private readonly DataContext dataContext;
        private readonly IOptionManager optionManager;
        private readonly RegardScheduler scheduler;
        private readonly UserQuotaService userQuotaService;
        private readonly HostThrottle hostThrottle;
        private static readonly IDictionary<int, VideoState> videos = new Dictionary<int, VideoState>();
        private static event EventHandler<VideoDownloadStateChangedEventArgs> videoStateChanged;

        public event EventHandler<VideoDownloadStateChangedEventArgs> VideoStateChanged
        {
            add => videoStateChanged += value;
            remove => videoStateChanged -= value;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public VideoDownloaderService(DataContext dataContext,
                                      IOptionManager optionManager,
                                      RegardScheduler scheduler,
                                      UserQuotaService userQuotaService,
                                      HostThrottle hostThrottle)
        {
            this.dataContext = dataContext;
            this.optionManager = optionManager;
            this.scheduler = scheduler;
            this.userQuotaService = userQuotaService;
            this.hostThrottle = hostThrottle;
            //this.scheduler.ScheduledVideoDownload += OnVideoQueued;
        }

        public void OnDownloadFinished(int videoId)
        {
            lock (videos)
            {
                videos.Remove(videoId);
            }
            
            videoStateChanged?.Invoke(this, new VideoDownloadStateChangedEventArgs() 
            { 
                VideoId = videoId, 
                State = VideoDownloadState.Completed 
            });
        }

        public void OnVideoDownloading(int videoId, float progress)
        {
            var state = GetOrCreate(videoId);
            state.State = VideoDownloadState.Downloading;
            state.Progress = progress;

            videoStateChanged?.Invoke(this, new VideoDownloadStateChangedEventArgs() 
            {
                VideoId = videoId, 
                State = VideoDownloadState.Downloading, 
                Progress = progress 
            });
        }

        public void OnVideoQueued(int videoId)
        {
            var state = GetOrCreate(videoId);
            state.State = VideoDownloadState.Queued;

            videoStateChanged?.Invoke(this, new VideoDownloadStateChangedEventArgs()
            {
                VideoId = videoId,
                State = VideoDownloadState.Queued,
            });
        }

        private VideoState GetOrCreate(int videoId)
        {
            lock (videos)
            {
                if (!videos.TryGetValue(videoId, out VideoState state))
                {
                    state = new VideoState();
                    videos[videoId] = state;
                }

                return state;
            }
        }

        public int? DetermineMaximumVideoCount(Subscription sub)
        {
            int result = int.MaxValue;

            int userLimit = optionManager.GetForUser(Options.User_MaxCount, sub.UserId);
            int userQuota = optionManager.GetForUser(Options.User_CountQuota, sub.UserId);
            if (userLimit >= 0 || userQuota >= 0)
            {
                int globalLimit = (userLimit >= 0 && userQuota >= 0)
                    ? Math.Min(userLimit, userQuota)
                    : Math.Max(userLimit, userQuota);

                var globalDownloadedCount = userQuotaService.GetUsage(sub.UserId).Count;

                int canDownload = Math.Max(globalLimit - globalDownloadedCount, 0);
                result = Math.Min(result, canDownload);
            }

            int subLimit = optionManager.GetForSubscription(Options.Subscriptions_MaxCount, sub.Id);
            if (subLimit >= 0)
            {
                var downloadedCount = dataContext.Videos
                    .AsQueryable()
                    .Where(x => x.SubscriptionId == sub.Id)
                    .Where(x => x.DownloadedPath != null)
                    .Count();

                int canDownload = Math.Max(subLimit - downloadedCount, 0);
                result = Math.Min(result, canDownload);
            }

            return (result == int.MaxValue) ? null : (int?)result;
        }

        public long? DetermineMaximumAllowedSize(Subscription sub)
        {
            long result = long.MaxValue;

            long userLimit = optionManager.GetForUser(Options.User_MaxSize, sub.UserId);
            if (userLimit >= 0) userLimit *= 1024L * 1024L;   // option is in MB, sizes are in bytes
            long userQuota = optionManager.GetForUser(Options.User_SizeQuota, sub.UserId);
            if (userQuota >= 0) userQuota *= 1024L * 1024L;
            if (userLimit >= 0 || userQuota >= 0)
            {
                long globalLimit = (userLimit >= 0 && userQuota >= 0)
                    ? Math.Min(userLimit, userQuota)
                    : Math.Max(userLimit, userQuota);

                var globalDownloadedSize = userQuotaService.GetUsage(sub.UserId).Bytes;

                long canDownload = Math.Max(globalLimit - globalDownloadedSize, 0);
                result = Math.Min(result, canDownload);
            }

            long subLimit = optionManager.GetForSubscription(Options.Subscriptions_MaxSize, sub.Id);
            if (subLimit >= 0) subLimit *= 1024L * 1024L;   // option is in MB, sizes are in bytes
            if (subLimit >= 0)
            {
                var downloadedSize = dataContext.Videos
                    .AsQueryable()
                    .Where(x => x.SubscriptionId == sub.Id)
                    .Where(x => x.DownloadedSize != null)
                    .Sum(x => x.DownloadedSize);

                long canDownload = Math.Max(subLimit - downloadedSize.Value, 0);
                result = Math.Min(result, canDownload);
            }

            return (result == long.MaxValue) ? null : (long?)result;
        }

        public async Task ProcessDownloadRules(Subscription sub)
        {
            // Check auto download value
            if (!optionManager.GetForSubscription(Options.Subscriptions_AutoDownload, sub.Id))
                return;

            VideoOrder order = optionManager.GetForSubscription(Options.Subscriptions_DownloadOrder, sub.Id);

            string publishedAfter = optionManager.GetForSubscription(Options.Subscriptions_PublishedAfter, sub.Id);
            string publishedBefore = optionManager.GetForSubscription(Options.Subscriptions_PublishedBefore, sub.Id);

            var filters = SubscriptionFilterExtensions.CompileFilters(
                dataContext.SubscriptionFilters
                    .Where(f => f.SubscriptionId == sub.Id)
                    .ToList()
                    .Select(f => (f.Action, f.Pattern)));

            // Filter server-side, then order + title-filter client-side: EF Core's SQLite
            // provider cannot translate ORDER BY on DateTimeOffset (Published), and the regex
            // title filters can't translate to SQL either. Filtering after ordering keeps the
            // ordered sequence intact and before Take so filtered-out titles don't take a slot.
            var downloadCandidates = dataContext.Videos
                .Where(x => x.SubscriptionId == sub.Id)
                .Where(x => x.DownloadedPath == null)
                .Where(x => !x.IsWatched)
                .Where(x => !x.DownloadSkipped)
                .AsEnumerable()
                .OrderBy(order)
                .Where(v => SubscriptionFilterExtensions.PassesTitleFilters(v.Name, filters))
                // Publish-date window. Un-enriched videos are let through rather than tested: sync
                // stamps them Published = MinValue as a sort placeholder (SynchronizeJob), so testing
                // them here would exclude every flat video forever — and since enrichment only happens
                // on open or download, they could never recover. DownloadVideoJob re-checks the window
                // once EnsureEnriched has filled in the real date.
                .Where(v => v.EnrichedAt == null
                            || PublishDateFilter.PassesDateWindow(v.Published, publishedAfter, publishedBefore));

            int? limit = DetermineMaximumVideoCount(sub);
            if (limit.HasValue)
                downloadCandidates = downloadCandidates.Take(limit.Value);

            long? sizeLimit = DetermineMaximumAllowedSize(sub);
            if (sizeLimit.HasValue && sizeLimit.Value <= 1 * 1024 * 1024) // rarely videos have less than 1mb
                return;

            foreach (var video in downloadCandidates)
            {
                // Dedup: skip a video that already has a pending/in-flight download (e.g. a sync re-run
                // while an earlier deferred download is still queued). Cleared when the attempt completes.
                if (hostThrottle.IsKnown(video.Id))
                    continue;
                hostThrottle.MarkKnown(video.Id);
                // auto: this is the automatic downloader, so the publish-date window applies once the
                // video is enriched. A user-initiated download goes through VideoManager.Download instead.
                await DownloadVideoJob.Schedule(scheduler, video, auto: true);
            }
        }
    }
}
