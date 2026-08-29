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
        private readonly DownloadCancellationRegistry cancellationRegistry;
        private readonly JobTrackerService jobTracker;

        public JobsController(UserManager<UserAccount> userManager,
                              ApiResponseFactory responseFactory,
                              DataContext dataContext,
                              DownloadCancellationRegistry cancellationRegistry,
                              JobTrackerService jobTracker)
        {
            this.userManager = userManager;
            this.responseFactory = responseFactory;
            this.dataContext = dataContext;
            this.cancellationRegistry = cancellationRegistry;
            this.jobTracker = jobTracker;
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

            // Order by last state change, newest first, with running jobs pinned on top: an active
            // download shows first, what just finished sits right below, and stale jobs sink — so the
            // things you care about aren't buried pages down. SQLite can't translate ORDER BY on a
            // DateTimeOffset, so pull just the ordering keys (no big Log column), sort + page in memory,
            // then fetch the chosen page's full rows.
            var order = query
                .Select(j => new { j.Id, j.State, j.Created, j.Started, j.Completed })
                .ToList()
                .OrderBy(k => k.State == JobState.Running ? 0 : 1)
                .ThenByDescending(k => k.Completed ?? k.Started ?? k.Created)
                .Skip(skip)
                .Take(take)
                .Select(k => k.Id)
                .ToList();

            var byId = query.Where(j => order.Contains(j.Id)).ToList().ToDictionary(j => j.Id);
            var jobs = order
                .Select(id => ToApi(byId[id]))     // in page order, without the (potentially large) Log
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
            // Detail view includes the full captured log. A running job hasn't persisted its Log yet
            // (that happens at completion), so stream the tracker's live buffer instead.
            dto.Log = (job.State == JobState.Running ? jobTracker.GetLive(job.Id)?.Log : null) ?? job.Log;
            return Ok(responseFactory.Success(dto));
        }

        [HttpPost("{id}/cancel")]
        [Authorize]
        public async Task<IActionResult> Cancel(long id)
        {
            var user = await userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized(responseFactory.Error("Not authenticated."));

            var query = await VisibleJobs(user);
            var job = query.FirstOrDefault(j => j.Id == id);
            if (job == null)
                return NotFound(responseFactory.Error("Job not found."));

            // Only live, cancellable jobs (running downloads) can be cancelled.
            if (!cancellationRegistry.Cancel(id))
                return BadRequest(responseFactory.Error("This job can't be cancelled (it isn't a running download)."));

            return Ok(responseFactory.Success(message: "Cancelling…"));
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

        private ApiJobInfo ToApi(JobInfo job)
        {
            var dto = new ApiJobInfo
            {
                Id = job.Id,
                Name = job.Name,
                State = (ApiJobState)(int)job.State,
                Detail = job.Detail,
                Progress = job.Progress,
                Created = job.Created,
                Started = job.Started,
                Completed = job.Completed,
                NextRun = job.NextRun,
            };

            // Progress/Detail are [NotMapped], so a running job's live values live only in the tracker.
            // Overlay them so the Job Log can show real progress/step (and the current output) live.
            if (job.State == JobState.Running && jobTracker.GetLive(job.Id) is JobTrackerService.JobLiveState live)
            {
                dto.Progress = live.Progress;
                dto.Detail = live.Detail;
                dto.Cancellable = cancellationRegistry.IsCancellable(job.Id);
            }

            return dto;
        }
    }
}
