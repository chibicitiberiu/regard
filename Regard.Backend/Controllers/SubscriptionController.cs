using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Regard.Backend.Common.Utils;
using Regard.Backend.Services;
using Regard.Common.API;
using Regard.Common.API.Request;
using Regard.Common.API.Response;
using RegardBackend.Model;
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
        public async Task<IActionResult> Validate([FromBody] SubscriptionValidate validate)
        {
            try
            {
                var url = new Uri(validate.Url);
                var result = await subscriptionManager.TestUrl(url);
                return Ok(responseFactory.Success(new SubscriptionValidateResult()
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
        public async Task<IActionResult> Create([FromBody] SubscriptionCreate create)
        {
            try
            {
                var url = new Uri(create.Url);
                var user = await userManager.GetUserAsync(User);

                var result = await subscriptionManager.Create(url, user, create.ParentFolderId);
                return Ok(responseFactory.Success(result.ToResponse()));
            }
            catch (UriFormatException)
            {
                return BadRequest(responseFactory.Error("Invalid URL format!"));
            }
        }

        [HttpGet]
        [Route("list")]
        [Authorize]
        public async Task<IActionResult> List([FromBody] SubscriptionList list)
        {
            var user = await userManager.GetUserAsync(User);
            var subs = subscriptionManager.GetAll(user, list.ParentFolderId)
                .OrderBy(x => x.Name)
                .Select(x => x.ToResponse())
                .ToArray();

            return Ok(responseFactory.Success(subs));
        }
    }
}
