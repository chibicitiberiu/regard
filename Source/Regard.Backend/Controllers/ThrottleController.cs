using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Regard.Backend.Model;
using Microsoft.Extensions.Configuration;
using Regard.Backend.Configuration;
using Regard.Backend.Services;
using Regard.Common.API.Admin;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Controllers
{
    /// <summary>Read-only throttle status for any signed-in user (transparency). Editing is admin-only.</summary>
    [ApiController]
    [Route("api/throttle")]
    [Authorize]
    public class ThrottleController : ControllerBase
    {
        private readonly HostThrottle throttle;
        private readonly IOptionManager optionManager;
        private readonly IConfiguration configuration;
        private readonly ApiResponseFactory responseFactory;

        private readonly UserManager<UserAccount> userManager;

        public ThrottleController(HostThrottle throttle, IOptionManager optionManager,
                                  IConfiguration configuration, ApiResponseFactory responseFactory,
                                  UserManager<UserAccount> userManager)
        {
            this.throttle = throttle;
            this.optionManager = optionManager;
            this.configuration = configuration;
            this.responseFactory = responseFactory;
            this.userManager = userManager;
        }

        [HttpGet]
        [Route("status")]
        public async Task<IActionResult> Status()
        {
            // Report the cookies that would ACTUALLY be used for this caller: their own jar if they have
            // one, otherwise the server-wide file. Recomputing a hard-coded DataDirectory/cookies.txt
            // here (as this did) ignores both the per-user jar and the REGARD_YTDL_COOKIES_FILE override,
            // so the Settings banner told people "no cookies" while downloads were happily using some.
            var user = await userManager.GetUserAsync(User);
            var cookiesPath = user != null
                ? optionManager.GetForUser(Options.Server_Ytdl_CookiesFile, user.Id)
                : optionManager.GetGlobal(Options.Server_Ytdl_CookiesFile);

            var status = new ApiThrottleStatus
            {
                Enabled = optionManager.GetGlobal(Options.Server_Throttle_Enabled),
                DownloadMinSeconds = optionManager.GetGlobal(Options.Server_Throttle_DownloadMinSeconds),
                DownloadMaxSeconds = optionManager.GetGlobal(Options.Server_Throttle_DownloadMaxSeconds),
                MaxPerHour = optionManager.GetGlobal(Options.Server_Throttle_MaxPerHour),
                MaxPerDay = optionManager.GetGlobal(Options.Server_Throttle_MaxPerDay),
                CookiesConfigured = !string.IsNullOrWhiteSpace(cookiesPath) && System.IO.File.Exists(cookiesPath),
                Hosts = throttle.GetStatus().Select(h => new ApiThrottleHost
                {
                    Host = h.Host,
                    InFlight = h.InFlight,
                    Queued = h.Queued,
                    UsedLastHour = h.UsedLastHour,
                    UsedLastDay = h.UsedLastDay,
                    NextSlot = h.NextSlotUtc,
                }).ToList(),
            };
            return Ok(responseFactory.Success(status));
        }
    }
}
