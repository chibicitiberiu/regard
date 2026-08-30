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
using Regard.Backend.Configuration;
using Regard.Backend.DB;
using Regard.Backend.Downloader;
using Regard.Backend.Jobs;
using Regard.Backend.Thumbnails;

namespace Regard.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubscriptionController : ControllerBase
    {
        private readonly UserManager<UserAccount> userManager;
        private readonly SubscriptionManager subscriptionManager;
        private readonly ApiResponseFactory responseFactory;
        private readonly ApiModelFactory modelFactory;
        private readonly IOptionManager optionManager;
        private readonly DataContext dataContext;
        private readonly IVideoDownloaderService videoDownloader;
        private readonly RegardScheduler scheduler;
        private readonly ThumbnailService thumbnailService;

        public SubscriptionController(UserManager<UserAccount> userManager,
                                      SubscriptionManager subscriptionManager,
                                      ApiResponseFactory responseFactory,
                                      ApiModelFactory modelFactory,
                                      IOptionManager optionManager,
                                      DataContext dataContext,
                                      IVideoDownloaderService videoDownloader,
                                      RegardScheduler scheduler,
                                      ThumbnailService thumbnailService)
        {
            this.userManager = userManager;
            this.subscriptionManager = subscriptionManager;
            this.responseFactory = responseFactory;
            this.modelFactory = modelFactory;
            this.optionManager = optionManager;
            this.dataContext = dataContext;
            this.videoDownloader = videoDownloader;
            this.scheduler = scheduler;
            this.thumbnailService = thumbnailService;
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

                // Deferred: this returns as soon as the row exists. Provider resolution, metadata and the
                // first sync all happen in ResolveSubscriptionJob, and reach the client over the live
                // change feed — the dialog used to sit for minutes waiting on yt-dlp.
                var result = await subscriptionManager.CreateDeferred(user, url, request.ParentFolderId, request.AllowDuplicate, request.AutoDownload);
                return Ok(responseFactory.Success(modelFactory.ToApi(result)));
            }
            catch (UriFormatException)
            {
                return BadRequest(responseFactory.Error("Invalid URL format!"));
            }
            catch (DuplicateSubscriptionException ex)
            {
                // 409 so the UI can tell this apart from a hard failure and offer "create anyway".
                return Conflict(responseFactory.Error(ex.Message));
            }
            catch (Exception ex)
            {
                // Surface a clean message instead of a bare 500 (e.g. no provider can handle the URL,
                // or a provider failed while resolving it).
                return BadRequest(responseFactory.Error("Could not add subscription: " + ex.Message, ex.ToString()));
            }
        }

        [HttpPost]
        [Route("import")]
        [Authorize]
        public async Task<IActionResult> Import([FromBody] SubscriptionImportRequest request)
        {
            var user = await userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized(responseFactory.Error("Not authenticated."));

            // Parse synchronously so we can report the batch size and reject empty input right away;
            // the slow per-URL adds run in a background job (progress in the bell, results in Job Log).
            var tree = SubscriptionImportParser.Parse(request.Content ?? "");
            int count = SubscriptionImportParser.CountFeeds(tree);
            if (count == 0)
                return BadRequest(responseFactory.Error("No subscriptions found in the input."));

            var treeJson = JsonUtils.Serialize(tree);
            await ImportSubscriptionsJob.Schedule(scheduler, user.Id, treeJson,
                request.ParentFolderId, request.AllowDuplicate, request.AutoDownload);

            return Ok(responseFactory.Success(new SubscriptionImportResponse() { Count = count }));
        }

        [HttpPost]
        [Route("create_empty")]
        [Authorize]
        public async Task<IActionResult> CreateEmpty([FromBody] SubscriptionCreateEmptyRequest request)
        {
            try
            {
                var user = await userManager.GetUserAsync(User);
                var result = subscriptionManager.CreateEmpty(user, request.Name, request.ParentFolderId);
                return Ok(responseFactory.Success(modelFactory.ToApi(result)));
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
                .Select(modelFactory.ToApi)
                .ToArray();

            if ((request.Parts & ApiSubscription.Parts.Config) != 0)
                AddConfigs(subscriptions, user.Id);

            if ((request.Parts & ApiSubscription.Parts.Stats) != 0)
                AddStatistics(subscriptions);

            return Ok(responseFactory.Success(new SubscriptionListResponse
            {
                Subscriptions = subscriptions
            }));
        }

        [HttpPost]
        [Route("set_icon")]
        [Authorize]
        public async Task<IActionResult> SetIcon([FromBody] ApiSetSubscriptionIconRequest request)
        {
            const int MaxBytes = 5 * 1024 * 1024;

            var user = await userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized(responseFactory.Error("Not authenticated."));

            var sub = subscriptionManager.Get(user, request.Id);
            if (sub == null)
                return BadRequest(responseFactory.Error("Subscription not found."));

            if (string.IsNullOrEmpty(request.IconBase64))
                return BadRequest(responseFactory.Error("No image provided."));

            byte[] bytes;
            try { bytes = Convert.FromBase64String(request.IconBase64); }
            catch (FormatException) { return BadRequest(responseFactory.Error("Invalid image data.")); }

            if (bytes.Length == 0 || bytes.Length > MaxBytes)
                return BadRequest(responseFactory.Error("Image must be between 1 byte and 5 MB."));

            try
            {
                // subscriptionManager.Get returned a tracked entity from the same scoped DataContext, so
                // setting the path + SaveChanges persists it.
                sub.ThumbnailPath = thumbnailService.SetCustom(sub, bytes, request.FileName);

                // The stored path is stable (s{id}/thumb.ext), so replacing a PNG with another PNG leaves
                // the string identical and EF would record no change — meaning the live change feed would
                // stay silent and clients would keep showing the old icon (the file content and its
                // cache-busting mtime did change). Force the property dirty so the update is broadcast.
                dataContext.Entry(sub).Property(x => x.ThumbnailPath).IsModified = true;
                dataContext.SaveChanges();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(responseFactory.Error(ex.Message));
            }

            return Ok(responseFactory.Success(modelFactory.ToApi(sub)));
        }

        // Resolve an option from a subscription's PARENT scope (parent folder → user → global → default),
        // i.e. the value it inherits when its own override is unset.
        private TValue ResolveInherited<TValue>(OptionDefinition<TValue> pref, ApiSubscription sub, string userId)
            => sub.ParentFolderId.HasValue
                ? optionManager.GetForSubscriptionFolder(pref, sub.ParentFolderId.Value)
                : optionManager.GetForUser(pref, userId);

        private void AddConfigs(ApiSubscription[] subscriptions, string userId)
        {
            foreach (var sub in subscriptions)
            {
                sub.Config = new ApiSubscriptionConfig();

                if (optionManager.GetForSubscriptionNoResolve(Options.Subscriptions_AutoDownload, sub.Id, out var autoDownload))
                    sub.Config.AutoDownload = autoDownload;

                if (optionManager.GetForSubscriptionNoResolve(Options.Subscriptions_MaxCount, sub.Id, out var maxCount))
                    sub.Config.DownloadMaxCount = maxCount;

                if (optionManager.GetForSubscriptionNoResolve(Options.Subscriptions_DownloadOrder, sub.Id, out var order))
                    sub.Config.DownloadOrder = order;

                if (optionManager.GetForSubscriptionNoResolve(Options.Subscriptions_MarkDeletedAsWatched, sub.Id, out var markDel))
                    sub.Config.MarkDeletedAsWatched = markDel;

                if (optionManager.GetForSubscriptionNoResolve(Options.Subscriptions_DeleteWatched, sub.Id, out var delWatched))
                    sub.Config.DeleteWatched = delWatched;

                if (optionManager.GetForSubscriptionNoResolve(Options.Subscriptions_DeleteGracePeriod, sub.Id, out var grace))
                    sub.Config.DeleteGracePeriod = grace;

                if (optionManager.GetForSubscriptionNoResolve(Options.Subscriptions_DownloadPath, sub.Id, out var path))
                    sub.Config.DownloadPath = path;

                if (optionManager.GetForSubscriptionNoResolve(Options.Ytdl_WriteSubtitles, sub.Id, out var writeSubs))
                    sub.Config.WriteSubtitles = writeSubs;

                if (optionManager.GetForSubscriptionNoResolve(Options.Ytdl_WriteAutoSub, sub.Id, out var writeAutoSub))
                    sub.Config.WriteAutoSub = writeAutoSub;

                if (optionManager.GetForSubscriptionNoResolve(Options.Ytdl_AllSubs, sub.Id, out var allSubs))
                    sub.Config.AllSubs = allSubs;

                if (optionManager.GetForSubscriptionNoResolve(Options.Ytdl_SubFormat, sub.Id, out var subFormat))
                    sub.Config.SubFormat = subFormat;

                if (optionManager.GetForSubscriptionNoResolve(Options.Ytdl_SubLang, sub.Id, out var subLang))
                    sub.Config.SubLang = subLang;

                if (optionManager.GetForSubscriptionNoResolve(Options.Sponsorblock_Actions, sub.Id, out var sbActions))
                    sub.Config.SponsorblockActions = sbActions;

                sub.Config.Filters = dataContext.SubscriptionFilters
                    .Where(f => f.SubscriptionId == sub.Id)
                    .ToList()
                    .Select(f => new ApiSubscriptionFilter { Action = f.Action, Pattern = f.Pattern })
                    .ToList();

                sub.Config.AutoDownloadDefault = ResolveInherited(Options.Subscriptions_AutoDownload, sub, userId);
                sub.Config.DownloadOrderDefault = ResolveInherited(Options.Subscriptions_DownloadOrder, sub, userId);
                sub.Config.DeleteWatchedDefault = ResolveInherited(Options.Subscriptions_DeleteWatched, sub, userId);
                sub.Config.MarkDeletedAsWatchedDefault = ResolveInherited(Options.Subscriptions_MarkDeletedAsWatched, sub, userId);
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
            var user = await userManager.GetUserAsync(User);
            var sub = subscriptionManager.Get(user, request.Id);
            await subscriptionManager.SynchronizeSubscription(sub);
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
        [Route("move")]
        [Authorize]
        public async Task<IActionResult> Move([FromBody] SubscriptionMoveRequest request)
        {
            var user = await userManager.GetUserAsync(User);
            try
            {
                subscriptionManager.MoveSubscription(user, request.Id, request.ParentFolderId);
            }
            catch (Exception ex)
            {
                return BadRequest(responseFactory.Error(ex.Message));
            }
            return Ok(responseFactory.Success());
        }

        [HttpPost]
        [Route("edit")]
        [Authorize]
        public async Task<IActionResult> Edit([FromBody] SubscriptionEditRequest request)
        {
            var user = await userManager.GetUserAsync(User);

            // Validate filter regexes up front: the option writes below persist eagerly, so a
            // late rejection would leave a partial apply.
            if (request.Filters != null)
            {
                foreach (var f in request.Filters)
                {
                    if (string.IsNullOrEmpty(f.Pattern))
                        return BadRequest(responseFactory.Error("A filter pattern cannot be empty."));
                    try { _ = new System.Text.RegularExpressions.Regex(f.Pattern); }
                    catch (ArgumentException)
                    { return BadRequest(responseFactory.Error($"Invalid filter pattern: {f.Pattern}")); }
                }
            }

            if (Regard.Common.SponsorBlock.SponsorBlockActions.HasRemoveSkipConflict(request.SponsorblockActions))
                return BadRequest(responseFactory.Error(
                    "SponsorBlock: Remove and Skip can't be combined (Remove cuts the file, which shifts the "
                    + "timestamps the in-player Skip relies on)."));

            try
            {
                subscriptionManager.Update(user, request.Id, request.Name, request.Description, request.ParentFolderId);
            }
            catch (Exception ex)
            {
                return BadRequest(responseFactory.Error(ex.Message));
            }

            // Update settings
            if (request.AutoDownload.HasValue)
                optionManager.SetForSubscription(Options.Subscriptions_AutoDownload, request.Id, request.AutoDownload.Value);
            else optionManager.UnsetForSubscription(Options.Subscriptions_AutoDownload, request.Id);

            if (request.DownloadMaxCount.HasValue)
                optionManager.SetForSubscription(Options.Subscriptions_MaxCount, request.Id, request.DownloadMaxCount.Value);
            else optionManager.UnsetForSubscription(Options.Subscriptions_MaxCount, request.Id);

            if (request.DownloadOrder.HasValue)
                optionManager.SetForSubscription(Options.Subscriptions_DownloadOrder, request.Id, request.DownloadOrder.Value);
            else optionManager.UnsetForSubscription(Options.Subscriptions_DownloadOrder, request.Id);

            if (request.MarkDeletedAsWatched.HasValue)
                optionManager.SetForSubscription(Options.Subscriptions_MarkDeletedAsWatched, request.Id, request.MarkDeletedAsWatched.Value);
            else optionManager.UnsetForSubscription(Options.Subscriptions_MarkDeletedAsWatched, request.Id);

            if (request.DeleteWatched.HasValue)
                optionManager.SetForSubscription(Options.Subscriptions_DeleteWatched, request.Id, request.DeleteWatched.Value);
            else optionManager.UnsetForSubscription(Options.Subscriptions_DeleteWatched, request.Id);

            if (request.DeleteGracePeriod.HasValue)
                optionManager.SetForSubscription(Options.Subscriptions_DeleteGracePeriod, request.Id, request.DeleteGracePeriod.Value);
            else optionManager.UnsetForSubscription(Options.Subscriptions_DeleteGracePeriod, request.Id);

            if (!string.IsNullOrEmpty(request.DownloadPath))
                optionManager.SetForSubscription(Options.Subscriptions_DownloadPath, request.Id, request.DownloadPath);
            else optionManager.UnsetForSubscription(Options.Subscriptions_DownloadPath, request.Id);

            if (request.WriteSubtitles.HasValue)
                optionManager.SetForSubscription(Options.Ytdl_WriteSubtitles, request.Id, request.WriteSubtitles.Value);
            else optionManager.UnsetForSubscription(Options.Ytdl_WriteSubtitles, request.Id);

            if (request.WriteAutoSub.HasValue)
                optionManager.SetForSubscription(Options.Ytdl_WriteAutoSub, request.Id, request.WriteAutoSub.Value);
            else optionManager.UnsetForSubscription(Options.Ytdl_WriteAutoSub, request.Id);

            if (request.AllSubs.HasValue)
                optionManager.SetForSubscription(Options.Ytdl_AllSubs, request.Id, request.AllSubs.Value);
            else optionManager.UnsetForSubscription(Options.Ytdl_AllSubs, request.Id);

            if (!string.IsNullOrEmpty(request.SubFormat))
                optionManager.SetForSubscription(Options.Ytdl_SubFormat, request.Id, request.SubFormat);
            else optionManager.UnsetForSubscription(Options.Ytdl_SubFormat, request.Id);

            if (!string.IsNullOrEmpty(request.SubLang))
                optionManager.SetForSubscription(Options.Ytdl_SubLang, request.Id, request.SubLang);
            else optionManager.UnsetForSubscription(Options.Ytdl_SubLang, request.Id);

            if (!string.IsNullOrEmpty(request.SponsorblockActions))
                optionManager.SetForSubscription(Options.Sponsorblock_Actions, request.Id, request.SponsorblockActions);
            else optionManager.UnsetForSubscription(Options.Sponsorblock_Actions, request.Id);

            // Replace the subscription's title filters (dedicated table, not the option store)
            if (request.Filters != null)
            {
                dataContext.SubscriptionFilters.RemoveRange(
                    dataContext.SubscriptionFilters.Where(f => f.SubscriptionId == request.Id));
                dataContext.SubscriptionFilters.AddRange(request.Filters.Select(f => new SubscriptionFilter
                {
                    SubscriptionId = request.Id,
                    Action = f.Action,
                    Pattern = f.Pattern,
                }));
                dataContext.SaveChanges();
            }

            return Ok(responseFactory.Success());
        }

        [HttpPost]
        [Route("filter_preview")]
        [Authorize]
        public async Task<IActionResult> FilterPreview([FromBody] SubscriptionFilterPreviewRequest request)
        {
            var user = await userManager.GetUserAsync(User);
            var sub = subscriptionManager.Get(user, request.SubscriptionId);
            if (sub == null)
                return BadRequest(responseFactory.Error("Invalid subscription ID."));

            var compiled = SubscriptionFilterExtensions.CompileFilters(
                (request.Filters ?? new List<ApiSubscriptionFilter>()).Select(f => (f.Action, f.Pattern)));

            var order = optionManager.GetForSubscription(Options.Subscriptions_DownloadOrder, sub.Id);

            var ordered = dataContext.Videos
                .Where(x => x.SubscriptionId == sub.Id)
                .AsEnumerable()
                .OrderBy(order)
                .ToList();

            // Compute the download window exactly as ProcessDownloadRules would.
            var windowIds = new HashSet<int>();
            long? sizeLimit = videoDownloader.DetermineMaximumAllowedSize(sub);
            if (!(sizeLimit.HasValue && sizeLimit.Value <= 1 * 1024 * 1024))
            {
                int? limit = videoDownloader.DetermineMaximumVideoCount(sub);
                var passing = ordered.Where(v => v.DownloadedPath == null && !v.IsWatched
                    && SubscriptionFilterExtensions.PassesTitleFilters(v.Name, compiled));
                if (limit.HasValue)
                    passing = passing.Take(limit.Value);
                foreach (var v in passing)
                    windowIds.Add(v.Id);
            }

            const int cap = 500;
            var items = ordered.Take(cap).Select(v => new FilterPreviewItem
            {
                Name = v.Name,
                IsDownloaded = v.DownloadedPath != null,
                IsWatched = v.IsWatched,
                PassesFilters = SubscriptionFilterExtensions.PassesTitleFilters(v.Name, compiled),
                InWindow = windowIds.Contains(v.Id),
            }).ToList();

            return Ok(responseFactory.Success(new SubscriptionFilterPreviewResponse
            {
                Videos = items,
                Truncated = ordered.Count > cap,
            }));
        }
    }
}
