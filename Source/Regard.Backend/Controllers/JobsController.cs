using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Regard.Backend.Common.Model;
using Regard.Backend.DB;
using Regard.Backend.Model;
using Regard.Backend.Services;
using Regard.Common.API.Jobs;
using Regard.Common.API.Model;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JobsController : ControllerBase
    {
        private readonly UserManager<UserAccount> userManager;
        private readonly ApiResponseFactory responseFactory;
        private readonly DataContext dataContext;

        public JobsController(UserManager<UserAccount> userManager,
                              ApiResponseFactory responseFactory,
                              DataContext dataContext)
        {
            this.userManager = userManager;
            this.responseFactory = responseFactory;
            this.dataContext = dataContext;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Get([FromQuery] int skip = 0, [FromQuery] int take = 25)
        {
            var user = await userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized(responseFactory.Error("Not authenticated."));

            if (take <= 0 || take > 200)
                take = 25;
            if (skip < 0)
                skip = 0;

            var query = await VisibleJobs(user);

            int total = query.Count();
            // Order by Id (monotonic with Created) — SQLite can't translate ORDER BY on DateTimeOffset.
            var jobs = query
                .OrderByDescending(j => j.Id)
                .Skip(skip)
                .Take(take)
                .ToList()
                .Select(ToApi)     // without the (potentially large) Log
                .ToArray();

            return Ok(responseFactory.Success(new JobListResponse { Jobs = jobs, Total = total }));
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetOne(long id)
        {
            var user = await userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized(responseFactory.Error("Not authenticated."));

            var query = await VisibleJobs(user);
            var job = query.FirstOrDefault(j => j.Id == id);
            if (job == null)
                return NotFound(responseFactory.Error("Job not found."));

            var dto = ToApi(job);
            dto.Log = job.Log;    // detail view includes the full captured log
            return Ok(responseFactory.Success(dto));
        }

        /// <summary>
        /// Jobs visible to the user: their own plus ownerless system jobs; admins see everything.
        /// </summary>
        private async Task<IQueryable<JobInfo>> VisibleJobs(UserAccount user)
        {
            if (await userManager.IsInRoleAsync(user, UserRoles.Admin))
                return dataContext.Jobs.AsQueryable();

            return dataContext.Jobs.Where(j => j.UserId == user.Id || j.UserId == null);
        }

        private static ApiJobInfo ToApi(JobInfo job) => new ApiJobInfo
        {
            Id = job.Id,
            Name = job.Name,
            State = (ApiJobState)(int)job.State,
            Detail = job.Detail,
            Progress = job.Progress,
            Created = job.Created,
            Started = job.Started,
            Completed = job.Completed,
        };
    }
}
