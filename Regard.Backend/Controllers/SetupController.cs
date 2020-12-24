using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Regard.Backend.Model;
using Regard.Backend.Services;
using Regard.Common.API.Response;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SetupController : ControllerBase
    {
        private readonly UserManager<UserAccount> userManager;
        private readonly IPreferencesManager preferencesManager;
        private readonly ApiResponseFactory responseFactory;

        public SetupController(UserManager<UserAccount> userManager, IPreferencesManager preferencesManager, ApiResponseFactory responseFactory)
        {
            this.userManager = userManager;
            this.preferencesManager = preferencesManager;
            this.responseFactory = responseFactory;
        }

        [HttpGet]
        [Route("server_status")]
        public async Task<IActionResult> ServerStatus()
        {
            var users = await userManager.GetUsersInRoleAsync(UserRoles.User);
            var admins = await userManager.GetUsersInRoleAsync(UserRoles.Admin);

            return Ok(responseFactory.Success(new ServerStatusResponse()
            {
                Initialized = await preferencesManager.Get(Preferences.Server_Initialized),
                HaveUsers = users.Count > 0,
                HaveAdmin = admins.Count > 0
            }));
        }

        [HttpPost]
        [Route("initialize")]
        public async Task<IActionResult> ServerInitialize()
        {
            List<string> errors = new List<string>();

            // Check we have an admin user
            var users = await userManager.GetUsersInRoleAsync(UserRoles.User);
            if (users.Count == 0)
                errors.Add("No user registered!");

            var admins = await userManager.GetUsersInRoleAsync(UserRoles.Admin);
            if (admins.Count == 0) 
                errors.Add("No administrator users are present!");

            // Complete setup
            if (errors.Count == 0)
            {
                await preferencesManager.Set(Preferences.Server_Initialized, true);
                return Ok(responseFactory.Success());
            }
            else
            {
                string allErrors = errors.Aggregate("Some errors have been encountered", (x, y) => x + "\n* " + y);
                return StatusCode(StatusCodes.Status405MethodNotAllowed, responseFactory.Error(allErrors));
            }
            
        }
    }
}
