using Microsoft.Extensions.Logging;
using Quartz;
using Regard.Backend.DB;
using Regard.Backend.Jobs;
using Regard.Backend.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Thumbnails
{
    public class FetchThumbnailsJob : JobBase
    {
        protected readonly ThumbnailService thumbnailService;

        public FetchThumbnailsJob(ILogger<FetchThumbnailsJob> logger,
                                  DataContext dataContext,
                                  JobTrackerService jobTrackerService,
                                  ThumbnailService thumbnailService)
            : base(logger, dataContext, jobTrackerService)
        {
            this.thumbnailService = thumbnailService;
        }

        public static Task Schedule(RegardScheduler scheduler, DateTimeOffset start, TimeSpan interval)
        {
            return scheduler.Schedule<FetchThumbnailsJob>(
                name: "Fetch thumbnails",
                start: start,
                repeatInterval: interval,
                retryCount: 0);
        }

        protected override async Task ExecuteJob(IJobExecutionContext context)
        {
            int countSubs = 0, countVids = 0;

            log.LogInformation("Fetching thumbnails started...");

            var subs = dataContext.Subscriptions.AsQueryable()
                .Where(x => x.ThumbnailPath.StartsWith("http"))
                .ToArray();

            foreach (var sub in subs)
            {
                try
                {
                    await thumbnailService.Fetch(sub);
                    dataContext.SaveChanges();
                    ++countSubs;
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Error fetching thumbnail for subscription {0}", sub);
                }
            }

            var videos = dataContext.Videos.AsQueryable()
                .Where(x => x.ThumbnailPath.StartsWith("http"))
                .ToArray();

            foreach (var video in videos)
            {
                try
                {
                    await thumbnailService.Fetch(video);
                    dataContext.SaveChanges();
                    ++countVids;
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Error fetching thumbnail for video {0}", video);
                }
            }

            log.LogInformation("Finished fetching thumbnails ({0} subscriptions, {1} videos)", countSubs, countSubs);
        }
    }
}
