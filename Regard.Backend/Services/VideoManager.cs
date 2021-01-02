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

        public VideoManager(DataContext dataContext, MessagingService messaging, RegardScheduler scheduler)
        {
            this.dataContext = dataContext;
            this.messaging = messaging;
            this.scheduler = scheduler;
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
    }
}
