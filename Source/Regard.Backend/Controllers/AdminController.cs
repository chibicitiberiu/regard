using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Regard.Backend.Configuration;
using Regard.Backend.Jobs;
using Regard.Backend.Model;
using Regard.Backend.Services;
using Regard.Common.API.Admin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = UserRoles.Admin)]
    public class AdminController : ControllerBase
    {
        private const long BytesPerGb = 1024L * 1024L * 1024L;
        private const long MbPerGb = 1024L;

        private readonly UserManager<UserAccount> userManager;
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly IOptionManager optionManager;
        private readonly UserQuotaService quotaService;
        private readonly RegardScheduler scheduler;
        private readonly ApiResponseFactory responseFactory;

        public AdminController(UserManager<UserAccount> userManager,
                               RoleManager<IdentityRole> roleManager,
                               IOptionManager optionManager,
                               UserQuotaService quotaService,
                               RegardScheduler scheduler,
                               ApiResponseFactory responseFactory)
        {
            this.userManager = userManager;
            this.roleManager = roleManager;
            this.optionManager = optionManager;
            this.quotaService = quotaService;
            this.scheduler = scheduler;
            this.responseFactory = responseFactory;
        }

        // ---- Server settings ---------------------------------------------------------------

        [HttpGet]
        [Route("settings")]
        public IActionResult GetServerSettings()
        {
            int countQuota = optionManager.GetGlobal(Options.User_CountQuota);
            long sizeQuotaMb = optionManager.GetGlobal(Options.User_SizeQuota);

            var settings = new ApiServerSettings
            {
                AllowRegistrations = optionManager.GetGlobal(Options.Server_AllowRegistrations),
                DefaultVideoQuota = countQuota >= 0 ? countQuota : (int?)null,
                DefaultStorageQuotaGb = sizeQuotaMb >= 0 ? sizeQuotaMb / (double)MbPerGb : (double?)null,
                JobHistoryRetentionDays = optionManager.GetGlobal(Options.Server_JobHistoryRetentionDays),
            };
            return Ok(responseFactory.Success(settings));
        }

        [HttpPost]
        [Route("settings")]
        public IActionResult SaveServerSettings([FromBody] ApiServerSettings request)
        {
            optionManager.SetGlobal(Options.Server_AllowRegistrations, request.AllowRegistrations);
            optionManager.SetGlobal(Options.User_CountQuota, request.DefaultVideoQuota ?? -1);
            optionManager.SetGlobal(Options.User_SizeQuota,
                request.DefaultStorageQuotaGb.HasValue ? (long)(request.DefaultStorageQuotaGb.Value * MbPerGb) : -1);
            optionManager.SetGlobal(Options.Server_JobHistoryRetentionDays, request.JobHistoryRetentionDays);
            return Ok(responseFactory.Success());
        }

        // ---- User management ---------------------------------------------------------------

        [HttpGet]
        [Route("users")]
        public async Task<IActionResult> GetUsers()
        {
            var result = new List<ApiAdminUser>();
            foreach (var user in userManager.Users.ToList())
            {
                var usage = quotaService.GetUsage(user.Id);

                int? countOverride = optionManager.GetForUserNoResolve(Options.User_CountQuota, user.Id, out int cv) ? cv : (int?)null;
                double? sizeOverrideGb = optionManager.GetForUserNoResolve(Options.User_SizeQuota, user.Id, out long sv) ? sv / (double)MbPerGb : (double?)null;

                result.Add(new ApiAdminUser
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    IsAdmin = await userManager.IsInRoleAsync(user, UserRoles.Admin),
                    IsDisabled = await userManager.IsLockedOutAsync(user),
                    VideoCount = usage.Count,
                    UsedBytes = usage.Bytes,
                    VideoQuotaOverride = countOverride >= 0 ? countOverride : null,
                    StorageQuotaOverrideGb = sizeOverrideGb,
                });
            }
            return Ok(responseFactory.Success(result));
        }

        [HttpPost]
        [Route("users/role")]
        public async Task<IActionResult> SetUserRole([FromBody] SetUserRoleRequest request)
        {
            var user = await userManager.FindByIdAsync(request.UserId);
            if (user == null)
                return BadRequest(responseFactory.Error("User does not exist."));

            if (!request.IsAdmin)
            {
                // Guard: never demote yourself or the last remaining admin.
                if (user.Id == userManager.GetUserId(User))
                    return BadRequest(responseFactory.Error("You can't remove your own admin role."));
                if (await IsLastAdmin(user))
                    return BadRequest(responseFactory.Error("Can't remove the last administrator."));

                await userManager.RemoveFromRoleAsync(user, UserRoles.Admin);
            }
            else
            {
                if (!await roleManager.RoleExistsAsync(UserRoles.Admin))
                    await roleManager.CreateAsync(new IdentityRole(UserRoles.Admin));
                if (!await userManager.IsInRoleAsync(user, UserRoles.Admin))
                    await userManager.AddToRoleAsync(user, UserRoles.Admin);
            }
            return Ok(responseFactory.Success());
        }

        [HttpPost]
        [Route("users/enabled")]
        public async Task<IActionResult> SetUserEnabled([FromBody] SetUserEnabledRequest request)
        {
            var user = await userManager.FindByIdAsync(request.UserId);
            if (user == null)
                return BadRequest(responseFactory.Error("User does not exist."));

            if (!request.Enabled && user.Id == userManager.GetUserId(User))
                return BadRequest(responseFactory.Error("You can't disable your own account."));

            if (request.Enabled)
            {
                await userManager.SetLockoutEndDateAsync(user, null);
            }
            else
            {
                await userManager.SetLockoutEnabledAsync(user, true);
                await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            }
            return Ok(responseFactory.Success());
        }

        [HttpPost]
        [Route("users/quota")]
        public async Task<IActionResult> SetUserQuota([FromBody] SetUserQuotaRequest request)
        {
            var user = await userManager.FindByIdAsync(request.UserId);
            if (user == null)
                return BadRequest(responseFactory.Error("User does not exist."));

            if (request.VideoQuota.HasValue)
                optionManager.SetForUser(Options.User_CountQuota, user.Id, request.VideoQuota.Value);
            else
                optionManager.UnsetForUser(Options.User_CountQuota, user.Id);

            if (request.StorageQuotaGb.HasValue)
                optionManager.SetForUser(Options.User_SizeQuota, user.Id, (long)(request.StorageQuotaGb.Value * MbPerGb));
            else
                optionManager.UnsetForUser(Options.User_SizeQuota, user.Id);

            return Ok(responseFactory.Success());
        }

        [HttpPost]
        [Route("users/delete")]
        public async Task<IActionResult> DeleteUser([FromBody] DeleteUserRequest request)
        {
            var user = await userManager.FindByIdAsync(request.UserId);
            if (user == null)
                return BadRequest(responseFactory.Error("User does not exist."));

            if (user.Id == userManager.GetUserId(User))
                return BadRequest(responseFactory.Error("You can't delete your own account."));
            if (await IsLastAdmin(user))
                return BadRequest(responseFactory.Error("Can't delete the last administrator."));

            await DeleteUserJob.Schedule(scheduler, userManager.GetUserId(User), user.Id);
            return Ok(responseFactory.Success());
        }

        /// <summary>True if the given user is an admin and no other admin exists.</summary>
        private async Task<bool> IsLastAdmin(UserAccount user)
        {
            if (!await userManager.IsInRoleAsync(user, UserRoles.Admin))
                return false;
            var admins = await userManager.GetUsersInRoleAsync(UserRoles.Admin);
            return admins.Count(a => a.Id != user.Id) == 0;
        }
    }
}
