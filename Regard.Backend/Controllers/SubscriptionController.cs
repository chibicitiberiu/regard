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

namespace Regard.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubscriptionController : ControllerBase
    {
        private readonly UserManager<UserAccount> userManager;
        private readonly SubscriptionManager subscriptionManager;
        private readonly ApiResponseFactory responseFactory;

        public SubscriptionController(UserManager<UserAccount> userManager, SubscriptionManager subscriptionManager, ApiResponseFactory responseFactory)
        {
            this.userManager = userManager;
            this.subscriptionManager = subscriptionManager;
            this.responseFactory = responseFactory;
        }

        [HttpPost]
        [Route("validate")]
        [Authorize]
        public async Task<IActionResult> Validate([FromBody] SubscriptionValidateRequest request)
        {
            try
            {
                var url = new Uri(request.Url);
                var result = await subscriptionManager.TestUrl(url);
                return Ok(responseFactory.Success(new SubscriptionValidateResponse()
                {
                    ProviderName = result,
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

                var result = await subscriptionManager.Create(url, user, request.ParentFolderId);
                return Ok(responseFactory.Success(result.ToApi()));
            }
            catch (UriFormatException)
            {
                return BadRequest(responseFactory.Error("Invalid URL format!"));
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

            return Ok(responseFactory.Success(new SubscriptionListResponse
            {
                Subscriptions = query
                    .OrderBy(x => x.Name)
                    .Select(x => x.ToApi())
                    .ToArray(),
            }));
        }

        [HttpPost]
        [Route("delete")]
        [Authorize]
        public async Task<IActionResult> Delete([FromBody] SubscriptionDeleteRequest request)
        {
            var user = await userManager.GetUserAsync(User);

            await subscriptionManager.DeleteSubscriptions(user, request.Ids, request.DeleteDownloadedFiles);

            return Ok(responseFactory.Success());
        }
    }
}
