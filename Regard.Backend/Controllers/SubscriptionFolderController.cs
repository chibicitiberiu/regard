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

namespace Regard.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubscriptionFolderController : ControllerBase
    {
        private readonly UserManager<UserAccount> userManager;
        private readonly SubscriptionManager subscriptionManager;
        private readonly ApiResponseFactory responseFactory;

        public SubscriptionFolderController(UserManager<UserAccount> userManager, SubscriptionManager subscriptionManager, ApiResponseFactory responseFactory)
        {
            this.userManager = userManager;
            this.subscriptionManager = subscriptionManager;
            this.responseFactory = responseFactory;
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

            return Ok(responseFactory.Success(new SubscriptionFolderListResponse
            {
                Folders = query
                    .OrderBy(x => x.Name)
                    .Select(x => x.ToApi())
                    .ToArray()
            }));
        }
    }
}
