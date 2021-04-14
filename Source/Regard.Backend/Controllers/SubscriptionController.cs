using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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

namespace Regard.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubscriptionController : ControllerBase
    {
        private readonly UserManager<UserAccount> userManager;
        private readonly SubscriptionManager subscriptionManager;
        private readonly ApiResponseFactory responseFactory;
        private readonly IPreferencesManager preferencesManager;

        public SubscriptionController(UserManager<UserAccount> userManager,
                                      SubscriptionManager subscriptionManager,
                                      ApiResponseFactory responseFactory,
                                      IPreferencesManager preferencesManager)
        {
            this.userManager = userManager;
            this.subscriptionManager = subscriptionManager;
            this.responseFactory = responseFactory;
            this.preferencesManager = preferencesManager;
        }

        [HttpPost]
        [Route("validate")]
        [Authorize]
        public async Task<IActionResult> Validate([FromBody] SubscriptionValidateRequest request)
        {
            try
            {
                string provider = null;

                if (request.Url != null)
                {
                    var url = new Uri(request.Url);
                    provider = await subscriptionManager.TestUrl(url);
                }

                if (request.Name != null)
                {
                    subscriptionManager.ValidateName(request.Name, request.ParentFolderId);
                }

                return Ok(responseFactory.Success(new SubscriptionValidateResponse()
                {
                    ProviderName = provider,
                }));
            }
            catch (UriFormatException)
            {
                return BadRequest(responseFactory.Error("Invalid URL format!"));
            }
            catch (ArgumentNullException)
            {
                return BadRequest(responseFactory.Error("Missing URL argument!"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(responseFactory.Error(ex.Message));
            }
            catch (Exception ex)
            {
                return BadRequest(responseFactory.Error(ex.Message, ex.Message + "\n" + ex.StackTrace));
            }
        }

        [HttpPost]
        [Route("create")]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] SubscriptionCreateRequest request)
        {
            try
            {
                var url = new Uri(request.Url);
                var user = await userManager.GetUserAsync(User);

                var result = await subscriptionManager.Create(user, url, request.ParentFolderId);
                return Ok(responseFactory.Success(result.ToApi()));
            }
            catch (UriFormatException)
            {
                return BadRequest(responseFactory.Error("Invalid URL format!"));
            }
        }

        [HttpPost]
        [Route("create_empty")]
        [Authorize]
        public async Task<IActionResult> CreateEmpty([FromBody] SubscriptionCreateEmptyRequest request)
        {
            try
            {
                var user = await userManager.GetUserAsync(User);
                var result = await subscriptionManager.CreateEmpty(user, request.Name, request.ParentFolderId);
                return Ok(responseFactory.Success(result.ToApi()));
            }
            catch (Exception ex)
            {
                return BadRequest(responseFactory.Error(ex.Message));
            }
        }

        [HttpPost]
        [Route("list")]
        [Authorize]
        public async Task<IActionResult> List([FromBody] SubscriptionListRequest request)
        {
            var user = await userManager.GetUserAsync(User);

            var query = subscriptionManager.GetAll(user);

            if (request.Ids != null)
                query = query.Where(x => request.Ids.Contains(x.Id));

            if (request.ParentFolderIds != null)
                query = query.Where(x => request.ParentFolderIds.Contains(x.ParentFolderId));

            var subscriptions = query
                .OrderBy(x => x.Name)
                .Select(x => x.ToApi())
                .ToArray();

            if ((request.Parts & ApiSubscription.Parts.Config) != 0)
                AddConfigs(subscriptions);

            if ((request.Parts & ApiSubscription.Parts.Stats) != 0)
                AddStatistics(subscriptions);

            return Ok(responseFactory.Success(new SubscriptionListResponse
            {
                Subscriptions = subscriptions
            }));
        }

        private void AddConfigs(ApiSubscription[] subscriptions)
        {
            foreach (var sub in subscriptions)
            {
                sub.Config = new ApiSubscriptionConfig();

                if (preferencesManager.GetForSubscriptionNoResolve(Preferences.Subscriptions_AutoDownload, sub.Id, out var autoDownload))
                    sub.Config.AutoDownload = autoDownload;

                if (preferencesManager.GetForSubscriptionNoResolve(Preferences.Subscriptions_MaxCount, sub.Id, out var maxCount))
                    sub.Config.DownloadMaxCount = maxCount;

                if (preferencesManager.GetForSubscriptionNoResolve(Preferences.Subscriptions_DownloadOrder, sub.Id, out var order))
                    sub.Config.DownloadOrder = order;

                if (preferencesManager.GetForSubscriptionNoResolve(Preferences.Subscriptions_AutoDeleteWatched, sub.Id, out var autoDel))
                    sub.Config.AutomaticDeleteWatched = autoDel;

                if (preferencesManager.GetForSubscriptionNoResolve(Preferences.Subscriptions_DownloadPath, sub.Id, out var path))
                    sub.Config.DownloadPath = path;
            }
        }

        private void AddStatistics(ApiSubscription[] subscriptions)
        {
            foreach (var sub in subscriptions)
            {
                sub.Stats = new ApiSubscriptionStats()
                {
                    TotalVideoCount = subscriptionManager.Statistic_TotalVideoCount(sub.Id),
                    WatchedVideoCount = subscriptionManager.Statistic_WatchedVideoCount(sub.Id),
                    DownloadedVideoCount = subscriptionManager.Statistic_DownloadedVideoCount(sub.Id),
                    DiskUsageBytes = subscriptionManager.Statistic_DiskUsage(sub.Id),
                };
            }
        }

        [HttpPost]
        [Route("delete")]
        [Authorize]
        public async Task<IActionResult> Delete([FromBody] SubscriptionDeleteRequest request)
        {
            var user = await userManager.GetUserAsync(User);
            await subscriptionManager.Delete(user, request.Ids, request.DeleteDownloadedFiles);
            return Ok(responseFactory.Success());
        }

        [HttpPost]
        [Route("synchronize")]
        [Authorize]
        public async Task<IActionResult> Synchronize([FromBody] SubscriptionSynchronizeRequest request)
        {
            await subscriptionManager.SynchronizeSubscription(request.Id);
            return Ok(responseFactory.Success());
        }

        [HttpPost]
        [Route("synchronize_all")]
        [Authorize]
        public async Task<IActionResult> SynchronizeAll()
        {
            await subscriptionManager.SynchronizeAll();
            return Ok(responseFactory.Success());
        }

        [HttpPost]
        [Route("edit")]
        [Authorize]
        public async Task<IActionResult> Edit([FromBody] SubscriptionEditRequest request)
        {
            var user = await userManager.GetUserAsync(User);

            try
            {
                await subscriptionManager.Update(user, request.Id, request.Name, request.Description, request.ParentFolderId);
            }
            catch (Exception ex)
            {
                return BadRequest(responseFactory.Error(ex.Message));
            }

            // Update settings
            if (request.AutoDownload.HasValue)
                preferencesManager.SetForSubscription(Preferences.Subscriptions_AutoDownload, request.Id, request.AutoDownload.Value);
            else preferencesManager.UnsetForSubscription(Preferences.Subscriptions_AutoDownload, request.Id);

            if (request.DownloadMaxCount.HasValue)
                preferencesManager.SetForSubscription(Preferences.Subscriptions_MaxCount, request.Id, request.DownloadMaxCount.Value);
            else preferencesManager.UnsetForSubscription(Preferences.Subscriptions_MaxCount, request.Id);

            if (request.DownloadOrder.HasValue)
                preferencesManager.SetForSubscription(Preferences.Subscriptions_DownloadOrder, request.Id, request.DownloadOrder.Value);
            else preferencesManager.UnsetForSubscription(Preferences.Subscriptions_DownloadOrder, request.Id);

            if (request.AutomaticDeleteWatched.HasValue)
                preferencesManager.SetForSubscription(Preferences.Subscriptions_AutoDeleteWatched, request.Id, request.AutomaticDeleteWatched.Value);
            else preferencesManager.UnsetForSubscription(Preferences.Subscriptions_AutoDeleteWatched, request.Id);

            if (!string.IsNullOrEmpty(request.DownloadPath))
                preferencesManager.SetForSubscription(Preferences.Subscriptions_DownloadPath, request.Id, request.DownloadPath);
            else preferencesManager.UnsetForSubscription(Preferences.Subscriptions_DownloadPath, request.Id);

            return Ok(responseFactory.Success());
        }
    }
}
