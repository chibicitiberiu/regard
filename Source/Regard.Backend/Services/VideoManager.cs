using MoreLinq;
using Regard.Backend.Common.Utils;
using Regard.Backend.DB;
using Regard.Backend.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    public class VideoManager
    {
        private readonly DataContext dataContext;
        private readonly MessagingService messaging;
        private readonly RegardScheduler scheduler;
        private readonly IProviderManager providerManager;

        private Dictionary<int, Video> videoProgress;

        public VideoManager(DataContext dataContext,
                            MessagingService messaging,
                            RegardScheduler scheduler,
                            IProviderManager providerManager)
        {
            this.dataContext = dataContext;
            this.messaging = messaging;
            this.scheduler = scheduler;
            this.providerManager = providerManager;
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

        public async Task Update(UserAccount user, int[] videoIds, Action<Video> updateMethod)
        {
            var vids = dataContext.Videos.AsQueryable()
                .Where(v => videoIds.Contains(v.Id))
                .Where(v => v.Subscription.UserId == user.Id);
                
            vids.ForEach(updateMethod);
            await dataContext.SaveChangesAsync();

            await vids.ToAsyncEnumerable()
                .ForEachAwaitAsync(async x => await messaging.NotifyVideoUpdated(user, x.ToApi()));
        }

        public async Task Download(UserAccount user, int[] videoIds)
        {
            // This verifies that only user's videos are downloaded
            await dataContext.Videos.AsQueryable()
                .Where(v => videoIds.Contains(v.Id))
                .Where(v => v.Subscription.UserId == user.Id)
                .Select(x => x.Id)
                .ToAsyncEnumerable()
                .ForEachAwaitAsync(scheduler.ScheduleDownloadVideo);
        }

        public async Task DeleteFiles(UserAccount user, int[] videoIds)
        {
            // This verifies that only user's videos are deleted
            var vids = dataContext.Videos.AsQueryable()
                .Where(v => videoIds.Contains(v.Id))
                .Where(v => v.Subscription.UserId == user.Id)
                .Select(x => x.Id)
                .ToArray();

            await scheduler.ScheduleDeleteFiles(vids);
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
