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
    public class DeleteSubscriptionFolderFilesJob : DeleteFilesJob
    {
        private static readonly string Data_SubscriptionFolderIds = nameof(SubscriptionFolderIds);
        private static readonly string Data_DeleteFolders = nameof(DeleteFolders);

        public int[] SubscriptionFolderIds { get; set; }

        public bool DeleteFolders { get; set; }

        public DeleteSubscriptionFolderFilesJob(IVideoStorageService videoStorage,
                                                SubscriptionManager subscriptionManager,
                                                JobTrackerService jobTrackerService,
                                                ILogger<DeleteFilesJob> logger,
                                                DataContext dataContext)
            : base(videoStorage, subscriptionManager, jobTrackerService, logger, dataContext)
        {
        }

        public static Task Schedule(RegardScheduler scheduler, int[] subscriptionFolderIds, bool deleteFolders)
        {
            return scheduler.Schedule<DeleteSubscriptionFolderFilesJob>(
                name: "Delete files",
                jobData: new Dictionary<string, object>()
                {
                    { Data_SubscriptionFolderIds, subscriptionFolderIds },
                    { Data_DeleteFolders, deleteFolders }
                },
                retryCount: 3,
                retryIntervalSecs: 10 * 60);
        }

        protected override async Task ExecuteJob(IJobExecutionContext context)
        {
            SubscriptionFolderIds = (int[])Job.JobData[Data_SubscriptionFolderIds];
            DeleteFolders = (bool)Job.JobData[Data_DeleteFolders];

            await base.ExecuteJob(context);

            if (DeleteFolders && SubscriptionFolderIds != null && SubscriptionFolderIds.Length > 0)
            {
                var firstFolder = dataContext.SubscriptionFolders
                    .Include(x => x.User)
                    .Where(x => x.Id == SubscriptionFolderIds[0])
                    .First();

                subscriptionManager.DeleteFoldersInternal(firstFolder.User, SubscriptionFolderIds);
            }
        }

        protected override void LogBegin()
        {
            log.LogInformation("Delete files job started for folders {0}, will {1}delete folders.",
                SubscriptionFolderIds.Humanize(), DeleteFolders? "" : "not ");
        }

        protected override void AddAdditionalVideos()
        {
            var folders = dataContext.SubscriptionFolders.AsQueryable()
                .Where(x => SubscriptionFolderIds.Contains(x.Id))
                .ToArray();

            foreach (var folder in folders)
            {
                var subs = dataContext.GetSubscriptionsRecursive(folder)
                    .Select(x => x.Id);

                videosToDelete.AddRange(dataContext.Videos.AsQueryable()
                    .Where(x => subs.Contains(x.SubscriptionId)));
            }

            base.AddAdditionalVideos();
        }
    }
}
