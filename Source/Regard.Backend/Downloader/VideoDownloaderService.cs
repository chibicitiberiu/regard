using Regard.Backend.Common.Utils;
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
        private readonly IPreferencesManager preferencesManager;
        private readonly RegardScheduler scheduler;
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
                                      IPreferencesManager preferencesManager,
                                      RegardScheduler scheduler)
        {
            this.dataContext = dataContext;
            this.preferencesManager = preferencesManager;
            this.scheduler = scheduler;
            this.scheduler.ScheduledVideoDownload += OnVideoQueued;
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

            int userLimit = preferencesManager.GetForUser(Preferences.User_MaxCount, sub.UserId);
            int userQuota = preferencesManager.GetForUser(Preferences.User_CountQuota, sub.UserId);
            if (userLimit >= 0 || userQuota >= 0)
            {
                int globalLimit = (userLimit >= 0 && userQuota >= 0)
                    ? Math.Min(userLimit, userQuota)
                    : Math.Max(userLimit, userQuota);

                var globalDownloadedCount = dataContext.Videos
                    .AsQueryable()
                    .Where(x => x.Subscription.UserId == sub.UserId)
                    .Where(x => x.DownloadedPath != null)
                    .Count();

                int canDownload = Math.Max(globalLimit - globalDownloadedCount, 0);
                result = Math.Min(result, canDownload);
            }

            int subLimit = preferencesManager.GetForSubscription(Preferences.Subscriptions_MaxCount, sub.Id);
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

            long userLimit = preferencesManager.GetForUser(Preferences.User_MaxSize, sub.UserId);
            long userQuota = preferencesManager.GetForUser(Preferences.User_SizeQuota, sub.UserId);
            if (userLimit >= 0 || userQuota >= 0)
            {
                long globalLimit = (userLimit >= 0 && userQuota >= 0)
                    ? Math.Min(userLimit, userQuota)
                    : Math.Max(userLimit, userQuota);

                var globalDownloadedSize = dataContext.Videos
                    .AsQueryable()
                    .Where(x => x.Subscription.UserId == sub.UserId)
                    .Where(x => x.DownloadedSize != null)
                    .Sum(x => x.DownloadedSize);

                long canDownload = Math.Max(globalLimit - globalDownloadedSize.Value, 0);
                result = Math.Min(result, canDownload);
            }

            long subLimit = preferencesManager.GetForSubscription(Preferences.Subscriptions_MaxSize, sub.Id);
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
            if (!preferencesManager.GetForSubscription(Preferences.Subscriptions_AutoDownload, sub.Id))
                return;

            VideoOrder order = preferencesManager.GetForSubscription(Preferences.Subscriptions_DownloadOrder, sub.Id);

            var downloadCandidates = dataContext.Videos
                .AsQueryable()
                .Where(x => x.SubscriptionId == sub.Id)
                .Where(x => x.DownloadedPath == null)
                .Where(x => !x.IsWatched)
                .OrderBy(order);

            int? limit = DetermineMaximumVideoCount(sub);
            if (limit.HasValue)
                downloadCandidates = downloadCandidates.Take(limit.Value);

            long? sizeLimit = DetermineMaximumAllowedSize(sub);
            if (sizeLimit.HasValue && sizeLimit.Value <= 1 * 1024 * 1024) // rarely videos have less than 1mb
                return;

            foreach (var video in downloadCandidates)
                await scheduler.ScheduleDownloadVideo(video.Id);
        }
    }
}
