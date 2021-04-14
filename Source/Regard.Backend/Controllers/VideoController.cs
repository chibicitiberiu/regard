using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Regard.Backend.Common.Utils;
using Regard.Backend.Model;
using Regard.Backend.Services;
using Regard.Common.API.Model;
using Regard.Common.API.Subscriptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VideoController : ControllerBase
    {
        private readonly UserManager<UserAccount> userManager;
        private readonly VideoManager videoManager;
        private readonly SubscriptionManager subscriptionManager;
        private readonly ApiResponseFactory responseFactory;
        private readonly IVideoStorageService videoStorage;

        public VideoController(UserManager<UserAccount> userManager,
                               VideoManager videoManager,
                               SubscriptionManager subscriptionManager,
                               ApiResponseFactory responseFactory,
                               IVideoStorageService videoStorage)
        {
            this.userManager = userManager;
            this.videoManager = videoManager;
            this.subscriptionManager = subscriptionManager;
            this.responseFactory = responseFactory;
            this.videoStorage = videoStorage;
        }

        [HttpPost]
        [Route("list")]
        [Authorize]
        public async Task<IActionResult> List([FromBody] VideoListRequest request)
        {
            var user = await userManager.GetUserAsync(User);

            var query = videoManager.GetAll(user);

            // Apply filters
            if (request.Ids != null)
                query = query.Where(x => request.Ids.Contains(x.Id));

            if (request.SubscriptionId != null)
                query = query.Where(x => x.SubscriptionId == request.SubscriptionId.Value);

            if (request.SubscriptionFolderId != null)
            {
                var sub = subscriptionManager.GetFolder(request.SubscriptionFolderId.Value);
                if (sub == null)
                    return BadRequest(responseFactory.Error("Invalid subscription folder ID."));

                var validSubscriptionIds = subscriptionManager.GetSubscriptionsRecursive(sub).Select(x => x.Id).ToArray();
                query = query.Where(x => validSubscriptionIds.Contains(x.SubscriptionId));
            }

            if (request.IsWatched.HasValue)
                query = query.Where(x => x.IsWatched == request.IsWatched.Value);

            if (request.IsDownloaded.HasValue)
            {
                if (request.IsDownloaded.Value)
                    query = query.Where(x => x.DownloadedPath != null);
                else
                    query = query.Where(x => x.DownloadedPath == null);
            }

            // TODO: proper search
            if (request.Query != null)
                query = query.Where(x => x.Name.ToLower().Contains(request.Query.ToLower()));

            // Get the item count here, before applying the limit and offset
            int itemCount = query.Count();

            // Sorting, limit and offset
            query = query.OrderBy(request.Order)
                .Skip(request.Offset ?? 0)
                .Take(request.Limit ?? 50);

            // Obtain mime types
            var videos = query.ToArray();
            var apiVideos = new List<ApiVideo>();

            foreach (var video in videos)
            {
                var apiVideo = video.ToApi();
                apiVideo.StreamMimeType = await videoStorage.GetMimeType(video);
                apiVideos.Add(apiVideo);
            }

            return Ok(responseFactory.Success(new VideoListResponse
            {
                TotalCount = itemCount,
                Videos = apiVideos.ToArray(),
            }));
        }

        [HttpPost]
        [Route("download")]
        [Authorize]
        public async Task<IActionResult> Download([FromBody] VideoDownloadRequest request)
        {
            var user = await userManager.GetUserAsync(User);
            await videoManager.Download(user, request.VideoIds);
            return Ok(responseFactory.Success());
        }

        [HttpPost]
        [Route("delete_files")]
        [Authorize]
        public async Task<IActionResult> DeleteFiles([FromBody] VideoDeleteFilesRequest request)
        {
            var user = await userManager.GetUserAsync(User);
            await videoManager.DeleteFiles(user, request.VideoIds);
            return Ok(responseFactory.Success());
        }

        [HttpPost]
        [Route("mark_watched")]
        [Authorize]
        public async Task<IActionResult> MarkWatched([FromBody] VideoMarkWatchedRequest request)
        {
            var user = await userManager.GetUserAsync(User);
            await videoManager.Update(user, request.VideoIds, video => video.IsWatched = true);
            return Ok(responseFactory.Success());
        }

        [HttpPost]
        [Route("mark_not_watched")]
        [Authorize]
        public async Task<IActionResult> MarkNotWatched([FromBody] VideoMarkNotWatchedRequest request)
        {
            var user = await userManager.GetUserAsync(User);
            await videoManager.Update(user, request.VideoIds, video => video.IsWatched = false);
            return Ok(responseFactory.Success());
        }

        [HttpPost]
        [Route("validate")]
        [Authorize]
        public async Task<IActionResult> Validate([FromBody] VideoValidateRequest request)
        {
            try
            {
                var url = new Uri(request.Url);
                await videoManager.ValidateUrl(url);
                return Ok(responseFactory.Success());
            }
            catch(Exception ex)
            {
                return BadRequest(responseFactory.Error(ex.Message));
            }
        }

        [HttpPost]
        [Route("add")]
        [Authorize]
        public async Task<IActionResult> Add([FromBody] VideoAddRequest request)
        {
            try
            {
                var user = await userManager.GetUserAsync(User);
                var url = new Uri(request.Url);
                await videoManager.Add(user, url, request.SubscriptionId);
                return Ok(responseFactory.Success());
            }
            catch (Exception ex)
            {
                return BadRequest(responseFactory.Error(ex.Message));
            }
        }

        [HttpGet]
        [Route("view")]
        [Authorize]
        public async Task<IActionResult> View([FromQuery(Name = "v")] int videoId)
        {
            var video = videoManager.Get(videoId);
            if (video == null)
                return NotFound();

            if (video.DownloadedPath == null)
                return NotFound();

            var mimeType = await videoStorage.GetMimeType(video);
            if (mimeType == null)
                return NotFound();

            var videoFile = await videoStorage.FindVideoFile(video);
            return PhysicalFile(videoFile, mimeType, true);
        }
    }
}
