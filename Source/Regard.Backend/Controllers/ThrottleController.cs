using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Regard.Backend.Configuration;
using Regard.Backend.Services;
using Regard.Common.API.Admin;
using System.IO;
using System.Linq;

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

        public ThrottleController(HostThrottle throttle, IOptionManager optionManager,
                                  IConfiguration configuration, ApiResponseFactory responseFactory)
        {
            this.throttle = throttle;
            this.optionManager = optionManager;
            this.configuration = configuration;
            this.responseFactory = responseFactory;
        }

        [HttpGet]
        [Route("status")]
        public IActionResult Status()
        {
            var dataDir = configuration["DataDirectory"];
            var cookiesPath = string.IsNullOrEmpty(dataDir) ? null : Path.Combine(dataDir, "cookies.txt");

            var status = new ApiThrottleStatus
            {
                Enabled = optionManager.GetGlobal(Options.Server_Throttle_Enabled),
                DownloadMinSeconds = optionManager.GetGlobal(Options.Server_Throttle_DownloadMinSeconds),
                DownloadMaxSeconds = optionManager.GetGlobal(Options.Server_Throttle_DownloadMaxSeconds),
                MaxPerHour = optionManager.GetGlobal(Options.Server_Throttle_MaxPerHour),
                MaxPerDay = optionManager.GetGlobal(Options.Server_Throttle_MaxPerDay),
                CookiesConfigured = cookiesPath != null && System.IO.File.Exists(cookiesPath),
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
