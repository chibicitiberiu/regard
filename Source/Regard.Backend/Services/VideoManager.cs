using MoreLinq;
using Regard.Backend.Common.Utils;
using Regard.Backend.Configuration;
using Regard.Backend.DB;
using Regard.Backend.Downloader;
using Regard.Backend.Jobs;
using Regard.Backend.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    #region Events

    public class VideoUpdatedEventArgs
    { 
        /// <summary>
        /// User who initiated the operation
        /// </summary>
        public UserAccount User { get; set; }

        /// <summary>
        /// Updated video
        /// </summary>
        public Video Video { get; set; }
    }


    #endregion

    public class VideoManager
    {
        private readonly DataContext dataContext;
        private readonly RegardScheduler scheduler;
        private readonly IProviderManager providerManager;
        private readonly IOptionManager optionManager;

        public event EventHandler<VideoUpdatedEventArgs> VideoUpdated;

        public VideoManager(DataContext dataContext,
                            RegardScheduler scheduler,
                            IProviderManager providerManager,
                            IOptionManager optionManager)
        {
            this.dataContext = dataContext;
            this.scheduler = scheduler;
            this.providerManager = providerManager;
            this.optionManager = optionManager;
        }

        public Video Get(int id)
        {
            return dataContext.Videos.Find(id);
        }

        public IQueryable<Video> GetAll(UserAccount userAccount)
        {
            return dataContext.Videos.AsQueryable()
                .Where(x => x.Subscription.UserId == userAccount.Id);
        }

        public void Update(UserAccount user, int[] videoIds, Action<Video> updateMethod)
        {
            var vids = dataContext.Videos.AsQueryable()
                .Where(v => videoIds.Contains(v.Id))
                .Where(v => v.Subscription.UserId == user.Id);
                
            vids.ForEach(updateMethod);
            dataContext.SaveChanges();

            if (VideoUpdated != null)
            {
                foreach (var video in vids)
                    VideoUpdated.Invoke(this, new VideoUpdatedEventArgs() { User = user, Video = video });
            }
        }

        /// <summary>
        /// Marks the given videos as watched. For downloaded videos whose subscription has
        /// Subscriptions_DeleteWatched enabled, deletes the files and refills the download window.
        /// </summary>
        public async Task MarkWatched(UserAccount user, int[] videoIds)
        {
            // Set the flag (+ notify the frontend) via the shared update path.
            Update(user, videoIds, video => video.IsWatched = true);

            // Forward auto-delete: delete downloaded files (and refill) for subs that opt in.
            var toDelete = dataContext.Videos.AsQueryable()
                .Where(v => videoIds.Contains(v.Id))
                .Where(v => v.Subscription.UserId == user.Id)
                .Where(v => v.DownloadedPath != null)
                .ToList()
                .Where(v => optionManager.GetForSubscription(Options.Subscriptions_DeleteWatched, v.SubscriptionId))
                .Select(v => v.Id)
                .ToArray();

            if (toDelete.Length > 0)
                await DeleteWatchedFilesJob.Schedule(scheduler, toDelete);
        }

        public async Task Download(UserAccount user, int[] videoIds)
        {
            // This verifies that only user's videos are downloaded
            var videosToDownload = dataContext.Videos.AsQueryable()
                .Where(v => videoIds.Contains(v.Id))
                .Where(v => v.Subscription.UserId == user.Id)
                .ToList();

            // A manual download clears a prior cancel/skip so the video is no longer excluded.
            bool anyUnskipped = false;
            foreach (var video in videosToDownload)
                if (video.DownloadSkipped)
                {
                    video.DownloadSkipped = false;
                    anyUnskipped = true;
                }
            if (anyUnskipped)
                await dataContext.SaveChangesAsync();

            foreach (var video in videosToDownload)
                await DownloadVideoJob.Schedule(scheduler, video);
        }

        public async Task DeleteFiles(UserAccount user, int[] videoIds)
        {
            // This verifies that only user's videos are deleted
            var vids = dataContext.Videos.AsQueryable()
                .Where(v => videoIds.Contains(v.Id))
                .Where(v => v.Subscription.UserId == user.Id)
                .ToList();

            // Reverse: for subscriptions that opt in, mark the video watched so it isn't re-downloaded.
            bool changed = false;
            foreach (var video in vids)
            {
                if (optionManager.GetForSubscription(Options.Subscriptions_MarkDeletedAsWatched, video.SubscriptionId))
                {
                    video.IsWatched = true;
                    changed = true;
                }
            }
            if (changed)
                dataContext.SaveChanges();

            await DeleteFilesJob.Schedule(scheduler, vids.Select(v => v.Id).ToArray());
        }

        public async Task Add(UserAccount user, Uri url, int subscriptionId)
        {
            var sub = dataContext.Subscriptions.Find(subscriptionId);
            if (sub == null)
                throw new ArgumentException("Invalid subscription ID!");

            if (sub.UserId != user.Id)
                throw new UnauthorizedAccessException("Not authorized to modify subscription!");

            var video = new Video() 
            { 
                OriginalUrl = url.ToString(),
                Subscription = sub,
            };

            var provider = await providerManager.FindForVideo(video).FirstOrDefaultAsync();
            if (provider == null)
                throw new Exception("Invalid/unsupported URL");

            await provider.UpdateMetadata(Enumerable.Repeat(video, 1), true, true);
            dataContext.Videos.Add(video);
            await dataContext.SaveChangesAsync();
            // TODO: send notification
        }

        public async Task ValidateUrl(Uri url)
        {
            var video = new Video() { OriginalUrl = url.ToString() };
            bool found = await providerManager.FindForVideo(video).AnyAsync();

            if (!found)
                throw new Exception("Invalid/unsupported URL");
        }

        public void OnDownloadProgress(int videoId, float percent)
        {
            // TODO
        }
    }
}
