using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Regard.Backend.Model;
using Regard.Backend.Services;
using Regard.Common.API.Notifications;
using System.Threading.Tasks;

namespace Regard.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController : ControllerBase
    {
        private readonly UserManager<UserAccount> userManager;
        private readonly ApiResponseFactory responseFactory;
        private readonly NotificationService notificationService;

        public NotificationsController(UserManager<UserAccount> userManager,
                                       ApiResponseFactory responseFactory,
                                       NotificationService notificationService)
        {
            this.userManager = userManager;
            this.responseFactory = responseFactory;
            this.notificationService = notificationService;
        }

        [HttpGet("recent")]
        [Authorize]
        public async Task<IActionResult> Recent([FromQuery] int take = 50)
        {
            var user = await userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized(responseFactory.Error("Not authenticated."));

            if (take <= 0 || take > 200)
                take = 50;

            bool isAdmin = await userManager.IsInRoleAsync(user, UserRoles.Admin);
            var items = notificationService.GetRecent(user.Id, isAdmin, take);
            return Ok(responseFactory.Success(new NotificationListResponse { Notifications = items }));
        }

        [HttpPost("{id}/dismiss")]
        [Authorize]
        public async Task<IActionResult> Dismiss(long id)
        {
            var user = await userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized(responseFactory.Error("Not authenticated."));

            bool isAdmin = await userManager.IsInRoleAsync(user, UserRoles.Admin);
            if (!await notificationService.Dismiss(id, user.Id, isAdmin))
                return NotFound(responseFactory.Error("Notification not found."));

            return Ok(responseFactory.Success(message: "Dismissed."));
        }

        [HttpPost("clear")]
        [Authorize]
        public async Task<IActionResult> Clear()
        {
            var user = await userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized(responseFactory.Error("Not authenticated."));

            bool isAdmin = await userManager.IsInRoleAsync(user, UserRoles.Admin);
            int cleared = notificationService.ClearAll(user.Id, isAdmin);
            return Ok(responseFactory.Success(message: $"Cleared {cleared} notification(s)."));
        }
    }
}
