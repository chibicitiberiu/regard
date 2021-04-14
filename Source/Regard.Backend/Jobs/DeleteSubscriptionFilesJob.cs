using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;
using Regard.Backend.DB;
using Regard.Backend.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Jobs
{
    public class DeleteSubscriptionFilesJob : DeleteFilesJob
    {
        public int[] SubscriptionIds { get; set; }

        public bool DeleteSubscriptions { get; set; }

        public DeleteSubscriptionFilesJob(IVideoStorageService videoStorage,
                                          SubscriptionManager subscriptionManager,
                                          ILogger<DeleteFilesJob> logger,
                                          DataContext dataContext)
            : base(videoStorage, subscriptionManager, logger, dataContext)
        {
        }

        protected override async Task ExecuteJob(IJobExecutionContext context)
        {
            SubscriptionIds = (int[])context.MergedJobDataMap.Get("SubscriptionIds");
            DeleteSubscriptions = context.MergedJobDataMap.GetBoolean("DeleteSubscriptions");
            await base.ExecuteJob(context);

            if (DeleteSubscriptions && SubscriptionIds != null && SubscriptionIds.Length > 0)
            {
                var firstSub = dataContext.Subscriptions
                        .Include(x => x.User)
                        .Where(x => x.Id == SubscriptionIds[0])
                        .First();

                await subscriptionManager.DeleteInternal(firstSub.User, SubscriptionIds);
            }
        }

        protected override void LogBegin()
        {
            log.LogInformation("Delete files job started for subscriptions {0}, will {1}delete subscriptions.", 
                SubscriptionIds.Humanize(), DeleteSubscriptions ? "" : "not ");
        }

        protected override void AddAdditionalVideos()
        {
            if (SubscriptionIds != null)
            {
                videosToDelete.AddRange(dataContext.Videos.AsQueryable()
                        .Where(x => SubscriptionIds.Contains(x.SubscriptionId))
                        .Where(x => x.DownloadedPath != null));
            }

            base.AddAdditionalVideos();
        }
    }
}
