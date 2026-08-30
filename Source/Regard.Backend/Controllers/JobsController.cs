using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Regard.Backend.Common.Model;
using Regard.Backend.Common.Utils;
using Regard.Backend.DB;
using Regard.Backend.Downloader;
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
        private readonly RegardScheduler scheduler;
        private readonly HostThrottle hostThrottle;
        private readonly NotificationService notificationService;

        public JobsController(UserManager<UserAccount> userManager,
                              ApiResponseFactory responseFactory,
                              DataContext dataContext,
                              DownloadCancellationRegistry cancellationRegistry,
                              JobTrackerService jobTracker,
                              RegardScheduler scheduler,
                              HostThrottle hostThrottle,
                              NotificationService notificationService)
        {
            this.userManager = userManager;
            this.responseFactory = responseFactory;
            this.dataContext = dataContext;
            this.cancellationRegistry = cancellationRegistry;
            this.jobTracker = jobTracker;
            this.scheduler = scheduler;
            this.hostThrottle = hostThrottle;
            this.notificationService = notificationService;
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

            // A running download has a live yt-dlp process to interrupt.
            if (cancellationRegistry.Cancel(id))
                return Ok(responseFactory.Success(message: "Cancelling…"));

            // Otherwise it may be a download that hasn't started: waiting on the host throttle, or
            // waiting out a retry interval. Both look identical here — State == Scheduled with a pending
            // Quartz trigger — and both are worth being able to stop.
            if (job.State == JobState.Scheduled && IsDownloadJob(job))
            {
                await CancelPendingDownload(job);
                return Ok(responseFactory.Success(message: "Cancelled."));
            }

            return BadRequest(responseFactory.Error("This job can't be cancelled (it isn't a running or queued download)."));
        }

        private static bool IsDownloadJob(JobInfo job)
            => job.Key == nameof(DownloadVideoJob);

        /// <summary>
        /// Cancels a download that never started. Beyond dropping the trigger this has to undo the
        /// bookkeeping that a normal run would have cleaned up: a deferred job returns before
        /// OnAfterExecute, so nothing releases its throttle queue entry or its "already known" marker, and
        /// its "Queued for download" card is keyed by video (so it deliberately outlives the job).
        /// </summary>
        private async Task CancelPendingDownload(JobInfo job)
        {
            await scheduler.TryUnschedule(job);

            int videoId = (int)ReadVideoId(job);
            if (videoId > 0)
            {
                var video = dataContext.Videos.Find(videoId);
                if (video != null)
                {
                    hostThrottle.Dequeue(UrlHostKey.Of(video.OriginalUrl), videoId);

                    // Same meaning as cancelling a running download: excluded from auto-download so the
                    // next-newest video takes its slot, but still downloadable by hand.
                    video.DownloadSkipped = true;
                    await dataContext.SaveChangesAsync();
                }

                await notificationService.Remove(null, $"download:{videoId}");
            }

            // Marks the row Cancelled, which JobBase.Execute now also checks — so if the trigger already
            // fired and this unschedule was too late, the run still bails instead of resurrecting itself.
            jobTracker.OnJobCancelled(job);
        }

        private long ReadVideoId(JobInfo job)
        {
            try
            {
                return job.JobData.TryGetValue("VideoId", out var v) && v != null
                    ? System.Convert.ToInt64(v)
                    : 0;
            }
            catch
            {
                return 0;
            }
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

            // A queued download hasn't started, so there's no live process in the registry — but its
            // trigger can still be dropped, so let the UI offer Cancel. Restricted to downloads: the
            // recurring/maintenance jobs have no cancel path.
            if (job.State == JobState.Scheduled && job.Key == nameof(DownloadVideoJob))
                dto.Cancellable = true;

            return dto;
        }
    }
}
