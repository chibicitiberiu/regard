using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Regard.Backend.Common.Utils;
using Regard.Backend.Configuration;
using Regard.Backend.Model;
using Regard.Backend.Services;
using Regard.Common.API.Model;
using Regard.Common.API.Subscriptions;
using Regard.Common.SponsorBlock;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VideoController : ControllerBase
    {
        private readonly UserManager<UserAccount> userManager;
        private readonly VideoManager videoManager;
        private readonly SubscriptionManager subscriptionManager;
        private readonly ApiResponseFactory responseFactory;
        private readonly ApiModelFactory modelFactory;
        private readonly IVideoStorageService videoStorage;
        private readonly IOptionManager optionManager;
        private readonly SponsorBlockClient sponsorBlockClient;
        private readonly ReturnYouTubeDislikeClient rydClient;

        public VideoController(UserManager<UserAccount> userManager,
                               VideoManager videoManager,
                               SubscriptionManager subscriptionManager,
                               ApiResponseFactory responseFactory,
                               ApiModelFactory modelFactory,
                               IVideoStorageService videoStorage,
                               IOptionManager optionManager,
                               SponsorBlockClient sponsorBlockClient,
                               ReturnYouTubeDislikeClient rydClient)
        {
            this.userManager = userManager;
            this.videoManager = videoManager;
            this.subscriptionManager = subscriptionManager;
            this.responseFactory = responseFactory;
            this.modelFactory = modelFactory;
            this.videoStorage = videoStorage;
            this.optionManager = optionManager;
            this.sponsorBlockClient = sponsorBlockClient;
            this.rydClient = rydClient;
        }

        [HttpPost]
        [Route("list")]
        [Authorize]
        public async Task<IActionResult> List([FromBody] VideoListRequest request)
        {
            var user = await userManager.GetUserAsync(User);

            var query = videoManager.GetAll(user);

            // Apply filters
            if (request.Ids != null)
                query = query.Where(x => request.Ids.Contains(x.Id));

            if (request.SubscriptionId != null)
                query = query.Where(x => x.SubscriptionId == request.SubscriptionId.Value);

            if (request.SubscriptionFolderId != null)
            {
                var sub = subscriptionManager.GetFolder(user, request.SubscriptionFolderId.Value);
                if (sub == null)
                    return BadRequest(responseFactory.Error("Invalid subscription folder ID."));

                var validSubscriptionIds = subscriptionManager.GetSubscriptionsRecursive(sub).Select(x => x.Id).ToArray();
                query = query.Where(x => validSubscriptionIds.Contains(x.SubscriptionId));
            }

            // WatchState (grid toolbar) takes precedence over the legacy IsWatched tri-state, which is
            // still used by programmatic callers (e.g. the watch-page "Up next" queue).
            if (request.WatchState.HasValue)
            {
                switch (request.WatchState.Value)
                {
                    case Regard.Model.VideoWatchState.Watched:
                        query = query.Where(x => x.IsWatched);
                        break;
                    case Regard.Model.VideoWatchState.Started:
                        query = query.Where(x => !x.IsWatched && x.PlaybackPositionSeconds >= Regard.Model.PlaybackConstants.MinInProgressSeconds);
                        break;
                    case Regard.Model.VideoWatchState.Unwatched:
                        query = query.Where(x => !x.IsWatched && (x.PlaybackPositionSeconds == null || x.PlaybackPositionSeconds < Regard.Model.PlaybackConstants.MinInProgressSeconds));
                        break;
                    case Regard.Model.VideoWatchState.All:
                    default:
                        break;
                }
            }
            else if (request.IsWatched.HasValue)
            {
                query = query.Where(x => x.IsWatched == request.IsWatched.Value);
            }

            if (request.IsDownloaded.HasValue)
            {
                if (request.IsDownloaded.Value)
                    query = query.Where(x => x.DownloadedPath != null);
                else
                    query = query.Where(x => x.DownloadedPath == null);
            }

            // TODO: proper search
            if (request.Query != null)
                query = query.Where(x => x.Name.ToLower().Contains(request.Query.ToLower()));

            // Get the item count here, before applying the limit and offset
            int itemCount = query.Count();

            // Sorting (client-side: EF Core's SQLite provider cannot ORDER BY DateTimeOffset),
            // then limit and offset.
            var videos = query
                .AsEnumerable()
                .OrderBy(request.Order)
                .Skip(request.Offset ?? 0)
                .Take(request.Limit ?? 50)
                .ToArray();

            // Lazy enrichment: when the watch page opens a single video that was listed flat during
            // sync, fetch its full metadata now. Gated to a single-Id request so the multi-video list
            // (and the 50-item Up-next queries) never trigger a burst of yt-dlp extractions.
            if (request.Ids?.Length == 1 && videos.Length == 1)
                await videoManager.EnsureEnriched(videos[0]);

            var apiVideos = new List<ApiVideo>();

            // Embedding is a per-user privacy choice (default off); only expose an embed URL when the
            // user allows it AND the source host is actually embeddable (else the watch page shows the
            // download / watch-on-site placeholder).
            bool embedAllowed = user != null && optionManager.GetForUser(Options.Ui_AllowEmbedding, user.Id);

            foreach (var video in videos)
            {
                var apiVideo = modelFactory.ToApi(video);
                apiVideo.StreamMimeType = await videoStorage.GetMimeType(video);
                apiVideo.EmbedUrl = embedAllowed ? VideoEmbedHelper.GetEmbedUrl(video) : null;
                apiVideos.Add(apiVideo);
            }

            // SponsorBlock in-player skip: only for the single-video watch fetch, only for a YouTube video
            // whose subscription has a "skip" category and whose file wasn't cut at download time (else the
            // original-timeline segments wouldn't align). Fetched live so it reflects the current config.
            if (request.Ids?.Length == 1 && apiVideos.Count == 1 && !apiVideos[0].SponsorsRemoved
                && VideoEmbedHelper.IsYouTube(videos[0]))
            {
                var skipCats = SponsorBlockActions.CategoriesWith(
                    optionManager.GetForSubscription(Options.Sponsorblock_Actions, videos[0].SubscriptionId),
                    SbAction.Skip);
                if (skipCats.Count > 0)
                    apiVideos[0].SponsorSegments = await sponsorBlockClient.GetSkipSegments(videos[0].VideoId, skipCats);
            }

            // Chapters: original-timeline chapters for the single-video watch fetch, deserialized from the
            // stored JSON. Only the watch page renders them, so this stays off the multi-item list path.
            if (request.Ids?.Length == 1 && apiVideos.Count == 1 && !string.IsNullOrEmpty(videos[0].Chapters))
            {
                try
                {
                    apiVideos[0].Chapters = System.Text.Json.JsonSerializer
                        .Deserialize<List<ApiChapter>>(videos[0].Chapters);
                }
                catch { /* malformed blob — leave chapters null */ }
            }

            // Subtitle tracks: sidecar files next to the downloaded video, for the single-video watch
            // fetch only. Reads a few KB of each file to tell auto-generated cues from hand-written ones,
            // so it stays off the multi-item list path.
            if (request.Ids?.Length == 1 && apiVideos.Count == 1 && videos[0].DownloadedPath != null)
            {
                var subtitles = await videoStorage.GetSubtitleFiles(videos[0]);
                if (subtitles.Count > 0)
                {
                    apiVideos[0].SubtitleTracks = subtitles
                        .Select(s => new ApiSubtitleTrack
                        {
                            Lang = s.Lang,
                            Label = SubtitleFile.LabelFor(s.Lang, s.AutoGenerated),
                            AutoGenerated = s.AutoGenerated,
                        })
                        .OrderBy(t => t.AutoGenerated)        // hand-written tracks first
                        .ThenBy(t => t.Label, StringComparer.CurrentCulture)
                        .ToList();
                }
            }

            // ReturnYouTubeDislike: real dislike counts for the single-video watch fetch of a YouTube video,
            // when the server has the feature enabled. Best-effort; leaves the counts null on any failure.
            if (request.Ids?.Length == 1 && apiVideos.Count == 1 && VideoEmbedHelper.IsYouTube(videos[0])
                && optionManager.GetGlobal(Options.ReturnYouTubeDislike_Enabled))
            {
                var votes = await rydClient.GetVotes(videos[0].VideoId);
                if (votes != null)
                {
                    apiVideos[0].Likes = votes.Likes;
                    apiVideos[0].Dislikes = votes.Dislikes;

                    // Derive the 0..1 liked ratio from RYD's own counts. Deliberately NOT votes.Rating:
                    // that field is YouTube's legacy 1..5 star average (verified live — a 97%-liked video
                    // reports 4.90), and Video.Rating is a 0..1 ratio that the watch page multiplies by 5.
                    var ratio = ProviderHelpers.CalculateRating(votes.Likes, votes.Dislikes);
                    apiVideos[0].Rating = ratio;

                    // Persist so the counts survive without re-calling RYD, and so a later metadata
                    // refresh that raises the like count scales the implied dislikes with it.
                    await videoManager.SetVotes(videos[0], votes.Likes, ratio);
                }
            }

            return Ok(responseFactory.Success(new VideoListResponse
            {
                TotalCount = itemCount,
                Videos = apiVideos.ToArray(),
            }));
        }

        [HttpPost]
        [Route("download")]
        [Authorize]
        public async Task<IActionResult> Download([FromBody] VideoDownloadRequest request)
        {
            var user = await userManager.GetUserAsync(User);
            await videoManager.Download(user, request.VideoIds, request.Force);
            return Ok(responseFactory.Success());
        }

        [HttpPost]
        [Route("delete_files")]
        [Authorize]
        public async Task<IActionResult> DeleteFiles([FromBody] VideoDeleteFilesRequest request)
        {
            var user = await userManager.GetUserAsync(User);
            await videoManager.DeleteFiles(user, request.VideoIds);
            return Ok(responseFactory.Success());
        }

        [HttpPost]
        [Route("mark_watched")]
        [Authorize]
        public async Task<IActionResult> MarkWatched([FromBody] VideoMarkWatchedRequest request)
        {
            var user = await userManager.GetUserAsync(User);
            await videoManager.MarkWatched(user, request.VideoIds);
            return Ok(responseFactory.Success());
        }

        [HttpPost]
        [Route("mark_not_watched")]
        [Authorize]
        public async Task<IActionResult> MarkNotWatched([FromBody] VideoMarkNotWatchedRequest request)
        {
            var user = await userManager.GetUserAsync(User);
            // Also clear any resume position so an explicitly-unwatched video starts from the beginning,
            // and cancel any grace-period deletion the (now-undone) watch had scheduled.
            videoManager.Update(user, request.VideoIds,
                video => { video.IsWatched = false; video.PlaybackPositionSeconds = null; video.DeleteScheduledAt = null; });
            return Ok(responseFactory.Success());
        }

        [HttpPost]
        [Route("mark_for_deletion")]
        [Authorize]
        public async Task<IActionResult> MarkForDeletion([FromBody] VideoMarkForDeletionRequest request)
        {
            var user = await userManager.GetUserAsync(User);
            await videoManager.MarkForDeletion(user, request.VideoIds);
            return Ok(responseFactory.Success());
        }

        [HttpPost]
        [Route("unmark_for_deletion")]
        [Authorize]
        public async Task<IActionResult> UnmarkForDeletion([FromBody] VideoUnmarkForDeletionRequest request)
        {
            var user = await userManager.GetUserAsync(User);
            videoManager.UnmarkForDeletion(user, request.VideoIds);
            return Ok(responseFactory.Success());
        }

        [HttpPost]
        [Route("report_progress")]
        [Authorize]
        public async Task<IActionResult> ReportProgress([FromBody] VideoReportProgressRequest request)
        {
            var user = await userManager.GetUserAsync(User);
            videoManager.SetPlaybackPosition(user, request.VideoId, request.PositionSeconds, request.DurationSeconds);
            return Ok(responseFactory.Success());
        }

        [HttpPost]
        [Route("validate")]
        [Authorize]
        public async Task<IActionResult> Validate([FromBody] VideoValidateRequest request)
        {
            try
            {
                var url = new Uri(request.Url);
                await videoManager.ValidateUrl(url);
                return Ok(responseFactory.Success());
            }
            catch(Exception ex)
            {
                return BadRequest(responseFactory.Error(ex.Message));
            }
        }

        [HttpPost]
        [Route("add")]
        [Authorize]
        public async Task<IActionResult> Add([FromBody] VideoAddRequest request)
        {
            try
            {
                var user = await userManager.GetUserAsync(User);
                var url = new Uri(request.Url);
                await videoManager.Add(user, url, request.SubscriptionId);
                return Ok(responseFactory.Success());
            }
            catch (Exception ex)
            {
                return BadRequest(responseFactory.Error(ex.Message));
            }
        }

        [HttpGet]
        [Route("view")]
        [Authorize]
        public async Task<IActionResult> View([FromQuery(Name = "v")] int videoId)
        {
            // Owner-scoped: only the video's own user may stream it (prevents streaming another
            // user's video by guessing its id).
            var user = await userManager.GetUserAsync(User);
            var video = videoManager.GetAll(user).FirstOrDefault(v => v.Id == videoId);
            if (video == null)
                return NotFound();

            if (video.DownloadedPath == null)
                return NotFound();

            var mimeType = await videoStorage.GetMimeType(video);
            if (mimeType == null)
                return NotFound();

            var videoFile = await videoStorage.FindVideoFile(video);
            return PhysicalFile(videoFile, mimeType, true);
        }

        /// <summary>
        /// Serves one sidecar subtitle track as WebVTT.
        ///
        /// SECURITY: <paramref name="lang"/> is only ever COMPARED against the languages discovered on
        /// disk — the path served is the one discovery returned. Never build a path out of it
        /// (Path.Combine(dir, prefix + "." + lang + ".vtt") or similar): that hands an attacker the
        /// filesystem. Ownership is enforced the same way View does it, through videoManager.GetAll(user).
        /// </summary>
        [HttpGet]
        [Route("subtitle")]
        [Authorize]
        public async Task<IActionResult> Subtitle([FromQuery(Name = "v")] int videoId,
                                                  [FromQuery(Name = "lang")] string lang)
        {
            if (string.IsNullOrEmpty(lang))
                return NotFound();

            var user = await userManager.GetUserAsync(User);
            var video = videoManager.GetAll(user).FirstOrDefault(v => v.Id == videoId);
            if (video == null || video.DownloadedPath == null)
                return NotFound();

            var subtitles = await videoStorage.GetSubtitleFiles(video);
            var track = subtitles.FirstOrDefault(s => string.Equals(s.Lang, lang, StringComparison.OrdinalIgnoreCase));
            if (track == null)
                return NotFound();

            // Always transformed rather than sent from disk: SubRip has to become WebVTT, and both kinds
            // get their per-cue placement stripped so a machine-generated track (which YouTube pins to
            // the left edge) renders in the same place as a hand-written one. MimeMapping has no entry
            // for .srt either, so the content type is stated outright instead of looked up.
            string content = await System.IO.File.ReadAllTextAsync(track.Path);
            return Content(SubtitleConverter.ToWebVtt(content, track.Format), "text/vtt");
        }
    }
}
