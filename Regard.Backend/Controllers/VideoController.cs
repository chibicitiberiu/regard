using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Regard.Backend.Common.Utils;
using Regard.Backend.Model;
using Regard.Backend.Services;
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

        public VideoController(UserManager<UserAccount> userManager,
                               VideoManager videoManager,
                               SubscriptionManager subscriptionManager,
                               ApiResponseFactory responseFactory)
        {
            this.userManager = userManager;
            this.videoManager = videoManager;
            this.subscriptionManager = subscriptionManager;
            this.responseFactory = responseFactory;
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
                var sub = subscriptionManager.FindFolder(request.SubscriptionFolderId.Value);
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
            
            return Ok(responseFactory.Success(new VideoListResponse
            {
                TotalCount = itemCount,
                Videos = query.Select(x => x.ToApi()).ToArray(),
            }));
        }
    }
}
