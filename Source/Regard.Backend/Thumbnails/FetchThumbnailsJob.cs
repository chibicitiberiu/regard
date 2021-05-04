using Microsoft.Extensions.Logging;
using Quartz;
using Regard.Backend.DB;
using Regard.Backend.Jobs;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Thumbnails
{
    public class FetchThumbnailsJob : JobBase
    {
        protected readonly ThumbnailService thumbnailService;

        protected override int RetryCount => throw new NotImplementedException();

        protected override TimeSpan RetryInterval => throw new NotImplementedException();

        public FetchThumbnailsJob(ILogger<FetchThumbnailsJob> logger,
                                  DataContext dataContext,
                                  ThumbnailService thumbnailService)
            : base(logger, dataContext)
        {
            this.thumbnailService = thumbnailService;
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
