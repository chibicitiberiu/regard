using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Regard.Backend.Common.Utils;
using Regard.Backend.Services;
using Regard.Common.API.Subscriptions;
using Regard.Backend.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Regard.Common.API.Model;
using MoreLinq;
using MoreLinq.Extensions;
using Regard.Backend.Configuration;

namespace Regard.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubscriptionFolderController : ControllerBase
    {
        private readonly UserManager<UserAccount> userManager;
        private readonly SubscriptionManager subscriptionManager;
        private readonly ApiResponseFactory responseFactory;
        private readonly ApiModelFactory modelFactory;
        private readonly IOptionManager optionManager;

        public SubscriptionFolderController(UserManager<UserAccount> userManager,
                                            SubscriptionManager subscriptionManager,
                                            ApiResponseFactory responseFactory,
                                            ApiModelFactory modelFactory,
                                            IOptionManager optionManager)
        {
            this.userManager = userManager;
            this.subscriptionManager = subscriptionManager;
            this.responseFactory = responseFactory;
            this.modelFactory = modelFactory;
            this.optionManager = optionManager;
        }

        [HttpPost]
        [Route("create")]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] SubscriptionFolderCreateRequest request)
        {
            var user = await userManager.GetUserAsync(User);
            await subscriptionManager.CreateFolder(user, request.Name, request.ParentId);
            return Ok(responseFactory.Success());
        }

        [HttpPost]
        [Route("list")]
        [Authorize]
        public async Task<IActionResult> List([FromBody] SubscriptionFolderListRequest request)
        {
            var user = await userManager.GetUserAsync(User);

            var query = subscriptionManager.GetAllFolders(user);

            if (request.Ids != null)
                query = query.Where(x => request.Ids.Contains(x.Id));

            if (request.ParentIds != null)
                query = query.Where(x => request.ParentIds.Contains(x.ParentId));

            var folders = query
                .OrderBy(x => x.Name)
                .Select(modelFactory.ToApi)
                .ToArray();

            if ((request.Parts & ApiSubscriptionFolder.Parts.Config) != 0)
                AddConfigs(folders);

            if ((request.Parts & ApiSubscriptionFolder.Parts.Stats) != 0)
                AddStatistics(folders);

            return Ok(responseFactory.Success(new SubscriptionFolderListResponse
            {
                Folders = folders
            }));
        }

        private void AddConfigs(ApiSubscriptionFolder[] folders)
        {
            foreach (var folder in folders)
            {
                folder.Config = new ApiSubscriptionFolderConfig();

                if (optionManager.GetForSubscriptionFolderNoResolve(Options.Subscriptions_AutoDownload, folder.Id, out var autoDownload))
                    folder.Config.AutoDownload = autoDownload;

                if (optionManager.GetForSubscriptionFolderNoResolve(Options.Subscriptions_MaxCount, folder.Id, out var maxCount))
                    folder.Config.DownloadMaxCount = maxCount;

                if (optionManager.GetForSubscriptionFolderNoResolve(Options.Subscriptions_DownloadOrder, folder.Id, out var order))
                    folder.Config.DownloadOrder = order;

                if (optionManager.GetForSubscriptionFolderNoResolve(Options.Subscriptions_AutoDeleteWatched, folder.Id, out var autoDel))
                    folder.Config.AutomaticDeleteWatched = autoDel;

                if (optionManager.GetForSubscriptionFolderNoResolve(Options.Subscriptions_DownloadPath, folder.Id, out var path))
                    folder.Config.DownloadPath = path;
            }
        }

        private void AddStatistics(ApiSubscriptionFolder[] folders)
        {
            foreach (var folder in folders)
            {
                folder.Stats = new ApiSubscriptionFolderStats()
                {
                    // TODO:
                    //TotalVideoCount = subscriptionManager.Statistic_TotalVideoCount(sub.Id),
                    //WatchedVideoCount = subscriptionManager.Statistic_WatchedVideoCount(sub.Id),
                    //DownloadedVideoCount = subscriptionManager.Statistic_DownloadedVideoCount(sub.Id),
                    //DiskUsageBytes = subscriptionManager.Statistic_DiskUsage(sub.Id),
                };
            }
        }

        [HttpPost]
        [Route("delete")]
        [Authorize]
        public async Task<IActionResult> Delete([FromBody] SubscriptionFolderDeleteRequest request)
        {
            var user = await userManager.GetUserAsync(User);
            await subscriptionManager.DeleteFolders(user, request.Ids, request.Recursive, request.DeleteDownloadedFiles);
            return Ok(responseFactory.Success());
        }

        [HttpPost]
        [Route("synchronize")]
        [Authorize]
        public async Task<IActionResult> Synchronize([FromBody] SubscriptionFolderSynchronizeRequest request)
        {
            await subscriptionManager.SynchronizeFolder(request.Id);
            return Ok(responseFactory.Success());
        }

        [HttpPost]
        [Route("edit")]
        [Authorize]
        public async Task<IActionResult> Edit([FromBody] SubscriptionFolderEditRequest request)
        {
            var user = await userManager.GetUserAsync(User);

            try
            {
                await subscriptionManager.UpdateFolder(user, request.Id, request.Name, request.ParentFolderId);
            }
            catch (Exception ex)
            {
                return BadRequest(responseFactory.Error(ex.Message));
            }

            // Update settings
            if (request.AutoDownload.HasValue)
                optionManager.SetForSubscriptionFolder(Options.Subscriptions_AutoDownload, request.Id, request.AutoDownload.Value);
            else optionManager.UnsetForSubscriptionFolder(Options.Subscriptions_AutoDownload, request.Id);

            if (request.DownloadMaxCount.HasValue)
                optionManager.SetForSubscriptionFolder(Options.Subscriptions_MaxCount, request.Id, request.DownloadMaxCount.Value);
            else optionManager.UnsetForSubscriptionFolder(Options.Subscriptions_MaxCount, request.Id);

            if (request.DownloadOrder.HasValue)
                optionManager.SetForSubscriptionFolder(Options.Subscriptions_DownloadOrder, request.Id, request.DownloadOrder.Value);
            else optionManager.UnsetForSubscriptionFolder(Options.Subscriptions_DownloadOrder, request.Id);

            if (request.AutomaticDeleteWatched.HasValue)
                optionManager.SetForSubscriptionFolder(Options.Subscriptions_AutoDeleteWatched, request.Id, request.AutomaticDeleteWatched.Value);
            else optionManager.UnsetForSubscriptionFolder(Options.Subscriptions_AutoDeleteWatched, request.Id);

            if (!string.IsNullOrEmpty(request.DownloadPath))
                optionManager.SetForSubscriptionFolder(Options.Subscriptions_DownloadPath, request.Id, request.DownloadPath);
            else optionManager.UnsetForSubscriptionFolder(Options.Subscriptions_DownloadPath, request.Id);

            return Ok(responseFactory.Success());
        }
    }
}
