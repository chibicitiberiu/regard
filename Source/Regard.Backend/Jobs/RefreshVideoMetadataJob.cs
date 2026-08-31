using Microsoft.Extensions.Logging;
using Quartz;
using Regard.Backend.Common.Model;
using Regard.Backend.Common.Services;
using Regard.Backend.Common.Utils;
using Regard.Backend.Configuration;
using Regard.Backend.DB;
using Regard.Backend.Model;
using Regard.Backend.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Regard.Backend.Jobs
{
    /// <summary>
    /// Refreshes one video's metadata on demand — view count, likes, title, description, duration,
    /// chapters — plus its like ratio from Return YouTube Dislike.
    ///
    /// This exists because "Fetch subtitles" only refreshes metadata as a side effect of actually
    /// fetching something: a video whose subtitles are already complete short-circuits before yt-dlp
    /// runs, so the numbers stay stale. And <see cref="RefreshMetadataJob"/> deliberately won't touch a
    /// video until its age-based interval says it's due, which can be three months for an old one.
    /// Someone looking at a stale view count needs a way to say "now".
    ///
    /// Unlike the background job this does NOT defer for downloads: a person clicked it and is waiting.
    /// It is a single paced extraction, so it delays a download by seconds at worst — which is also why
    /// the action is per-video and has no "refresh everything" counterpart.
    /// </summary>
    [ResumeAfterRestart]
    public class RefreshVideoMetadataJob : JobBase
    {
        private readonly IOptionManager optionManager;
        private readonly IYoutubeDlService ytdlService;
        private readonly ReturnYouTubeDislikeClient rydClient;
        private readonly VideoManager videoManager;

        private static readonly string Data_VideoId = nameof(VideoId);

        public int VideoId { get; private set; } = -1;

        private Video video;
        private bool refreshed;

        public RefreshVideoMetadataJob(ILogger<RefreshVideoMetadataJob> log,
                                       DataContext dataContext,
                                       JobTrackerService jobTrackerService,
                                       IOptionManager optionManager,
                                       IYoutubeDlService ytdlService,
                                       ReturnYouTubeDislikeClient rydClient,
                                       VideoManager videoManager) : base(log, dataContext, jobTrackerService)
        {
            this.optionManager = optionManager;
            this.ytdlService = ytdlService;
            this.rydClient = rydClient;
            this.videoManager = videoManager;
        }

        public static Task Schedule(RegardScheduler scheduler, Video video)
        {
            return scheduler.Schedule<RefreshVideoMetadataJob>(
                name: $"Refresh metadata for {video}",
                jobData: new Dictionary<string, object>() { { Data_VideoId, video.Id } },
                retryCount: 1,
                retryIntervalSecs: 5 * 60);
        }

        protected override JobNotification GetOngoingNotification()
            => new JobNotification
            {
                Title = "Refreshing metadata",
                Text = video?.Name,
                // No VideoDbId: the grid keys its download pie off it, and this is not a download.
            };

        protected override JobNotification GetSuccessNotification()
            => (video == null || !refreshed) ? null : new JobNotification
            {
                Title = "Metadata refreshed",
                Text = video.Name,
            };

        protected override JobNotification GetFailureNotification(Exception ex)
            => video == null ? null : new JobNotification
            {
                Title = "Could not refresh metadata",
                Text = video.Name,
            };

        protected override async Task ExecuteJob(IJobExecutionContext context)
        {
            if (Job.JobData.TryGetValue(Data_VideoId, out object videoId))
                VideoId = Convert.ToInt32(videoId);

            video = dataContext.Videos.Find(VideoId);
            if (video == null)
            {
                Job.RetryCount = 0;
                throw new ArgumentException($"Refresh failed - invalid video id {VideoId}.");
            }

            long? viewsBefore = (long?)video.Views;

            await ytdlService.PaceExtractionAsync(UrlHostKey.Of(video.OriginalUrl));
            refreshed = await videoManager.RefreshMetadataNow(video);

            if (!refreshed)
            {
                JobLog("Could not fetch fresh metadata for this video.", MessageSeverity.Warning);
                return;
            }

            // The like ratio comes from Return YouTube Dislike, not yt-dlp — YouTube stopped publishing
            // dislike counts in 2021. Different host, different budget, so it costs nothing here.
            if (VideoEmbedHelper.IsYouTube(video)
                && optionManager.GetGlobal(Options.ReturnYouTubeDislike_Enabled))
            {
                try
                {
                    var votes = await rydClient.GetVotes(video.VideoId);
                    if (votes != null)
                    {
                        // votes.Rating is YouTube's legacy 1..5 star average; Video.Rating is a 0..1
                        // ratio that the watch page multiplies by 5. Storing the wrong one is silent.
                        await videoManager.SetVotes(video, votes.Likes,
                            ProviderHelpers.CalculateRating(votes.Likes, votes.Dislikes));
                    }
                }
                catch (Exception ex)
                {
                    log.LogWarning(ex, "videoId={0}: rating refresh failed", VideoId);
                }
            }

            JobLog(viewsBefore.HasValue && (long?)video.Views != viewsBefore
                ? $"Refreshed: {viewsBefore:N0} → {video.Views:N0} views."
                : "Refreshed.");
        }
    }
}
