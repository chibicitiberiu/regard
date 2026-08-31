using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Quartz;
using Regard.Backend.Common.Model;
using Regard.Backend.Common.Services;
using Regard.Backend.Common.Utils;
using Regard.Backend.Configuration;
using Regard.Backend.DB;
using Regard.Backend.Downloader;
using Regard.Backend.Model;
using Regard.Backend.Services;
using Regard.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YoutubeDLWrapper;

namespace Regard.Backend.Jobs
{
    /// <summary>
    /// Re-runs yt-dlp against an already-downloaded video to fetch the sidecars it is missing —
    /// subtitles, primarily — without touching the media file.
    ///
    /// The case this exists for: videos downloaded before subtitles were switched on have an .mp4 and
    /// nothing else, and the only way to get captions for them used to be a full re-download.
    ///
    /// Resumable after a restart: it is one-off, tied to a video, and idempotent — yt-dlp skips subtitle
    /// files that already exist, so a second run is a no-op rather than a re-fetch.
    /// </summary>
    [ResumeAfterRestart]
    public class ReprocessVideoJob : JobBase
    {
        private readonly IOptionManager optionManager;
        private readonly IYoutubeDlService ytdlService;
        private readonly IVideoStorageService videoStorage;
        private readonly HostThrottle hostThrottle;

        private static readonly string Data_VideoId = nameof(VideoId);
        private static readonly string Data_Auto = "Auto";

        /// <summary>
        /// yt-dlp's stored default is "best", which can resolve to json3. SubtitleFile only recognises
        /// vtt and srt, so a json3 sidecar would sit on disk invisible to the player AND to the
        /// "does this video have subtitles?" check — making the automatic sweep re-fetch it forever.
        /// </summary>
        private const string SubFormatPreference = "vtt/srt/best";

        public int VideoId { get; private set; } = -1;

        private Video video;
        private bool auto;
        private bool noOpped;
        private readonly List<string> writtenSubtitles = new();

        public ReprocessVideoJob(ILogger<ReprocessVideoJob> log,
                                 DataContext dataContext,
                                 JobTrackerService jobTrackerService,
                                 IOptionManager optionManager,
                                 IYoutubeDlService ytdlService,
                                 IVideoStorageService videoStorage,
                                 HostThrottle hostThrottle) : base(log, dataContext, jobTrackerService)
        {
            this.optionManager = optionManager;
            this.ytdlService = ytdlService;
            this.videoStorage = videoStorage;
            this.hostThrottle = hostThrottle;
        }

        /// <summary>
        /// Queues a sidecar refetch. <paramref name="auto"/> marks a job the background sweep created
        /// rather than a person: those stay silent in the notification bell and step aside for downloads.
        /// </summary>
        public static Task Schedule(RegardScheduler scheduler, Video video, bool auto = false)
        {
            var jobData = new Dictionary<string, object>()
            {
                { Data_VideoId, video.Id }
            };

            // Written only when true, so an absent key reads as false without a migration — JobDataJson
            // is free-form. Same convention as DownloadVideoJob's Forced/Auto flags.
            if (auto)
                jobData[Data_Auto] = true;

            return scheduler.Schedule<ReprocessVideoJob>(
                name: $"Fetch subtitles for {video}",
                jobData: jobData,
                retryCount: 1,
                retryIntervalSecs: 10 * 60);
        }

        /// <summary>
        /// A sweep-created job steps aside while a download is running or queued on the same host.
        /// Extractions and downloads share the throttle's pacing floor, so running one now would push a
        /// waiting download further out. Returning a time reschedules the trigger and frees the worker.
        /// A user-initiated job never defers — someone is waiting for it.
        /// </summary>
        protected override Task<DateTimeOffset?> ShouldDefer(IJobExecutionContext context)
        {
            if (Job.JobData.TryGetValue(Data_VideoId, out object videoId))
                VideoId = Convert.ToInt32(videoId);

            if (!ReadAutoFlag())
                return Task.FromResult<DateTimeOffset?>(null);

            var v = dataContext.Videos.Find(VideoId);
            if (v == null)
                return Task.FromResult<DateTimeOffset?>(null);

            if (hostThrottle.HasDownloadPressure(UrlHostKey.Of(v.OriginalUrl)))
                return Task.FromResult<DateTimeOffset?>(DateTimeOffset.UtcNow.AddMinutes(15));

            return Task.FromResult<DateTimeOffset?>(null);
        }

        // Silent for sweeps, visible for a click. The type is in NotifiableJobTypes so a user-initiated
        // run shows progress; an unattended one would just be noise.
        protected override JobNotification GetOngoingNotification()
            => auto ? null : new JobNotification
            {
                Title = "Fetching subtitles",
                Text = video?.Name,
                // Deliberately no VideoDbId: the video grid keys its download pie off it
                // (VideoList.DownloadNotification), and a subtitle fetch must not look like a download.
            };

        protected override JobNotification GetSuccessNotification()
        {
            if (auto || video == null || noOpped)
                return null;

            return new JobNotification
            {
                Title = writtenSubtitles.Count > 0 ? "Subtitles downloaded" : "No new subtitles found",
                Text = writtenSubtitles.Count > 0
                    ? $"{video.Name} ({string.Join(", ", writtenSubtitles)})"
                    : video.Name,
            };
        }

        protected override JobNotification GetFailureNotification(Exception ex)
            => (auto || video == null) ? null : new JobNotification
            {
                Title = "Could not fetch subtitles",
                Text = video.Name,
            };

        private bool ReadAutoFlag()
        {
            try
            {
                return Job.JobData.TryGetValue(Data_Auto, out var value)
                    && value != null
                    && Convert.ToBoolean(value);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "videoId={0}: could not read the auto flag, treating as false", VideoId);
                return false;
            }
        }

        protected override async Task ExecuteJob(IJobExecutionContext context)
        {
            if (Job.JobData.TryGetValue(Data_VideoId, out object videoId))
                VideoId = Convert.ToInt32(videoId);
            auto = ReadAutoFlag();

            video = dataContext.Videos.Find(VideoId);
            if (video == null)
            {
                Job.RetryCount = 0;
                throw new ArgumentException($"Reprocess failed - invalid video id {VideoId}.");
            }

            // The inverse of DownloadVideoJob's guard: this job REQUIRES a media file to sit beside.
            if (video.DownloadedPath == null)
            {
                noOpped = true;
                JobLog($"{video.Name} isn't downloaded — nothing to reprocess.");
                return;
            }

            // SponsorBlock's Remove action cut segments out of the file on disk, so the media is shorter
            // than the timeline YouTube serves cues against. With --skip-download there is no file for
            // yt-dlp's ModifyChapters pass to re-time against, so anything fetched here would be
            // permanently out of sync. Refusing is the honest answer.
            if (video.SponsorsRemoved)
            {
                noOpped = true;
                Job.RetryCount = 0;
                JobLog("This video was downloaded with SponsorBlock segments cut out, so freshly-fetched "
                     + "subtitles would not line up with the file. Skipping.", MessageSeverity.Warning);
                return;
            }

            var before = (await videoStorage.GetSubtitleFiles(video)).Select(s => s.Lang).ToList();
            bool needs = SubtitleNeeds.NeedsSubtitles(
                before,
                optionManager.GetForSubscription(Options.Ytdl_SubLang, video.SubscriptionId),
                optionManager.GetForSubscription(Options.Ytdl_WriteSubtitles, video.SubscriptionId),
                optionManager.GetForSubscription(Options.Ytdl_WriteAutoSub, video.SubscriptionId),
                optionManager.GetForSubscription(Options.Ytdl_AllSubs, video.SubscriptionId));

            if (!needs)
            {
                noOpped = true;
                JobLog(before.Count > 0
                    ? $"Already has subtitles ({string.Join(", ", before)}) — nothing to fetch."
                    : "Subtitles are turned off for this subscription — nothing to fetch.");
                return;
            }

            string host = UrlHostKey.Of(video.OriginalUrl);
            await ytdlService.PaceExtractionAsync(host);

            var opts = ResolveReprocessOptions(video).ToArray();
            log.LogInformation("videoId={0}: reprocessing with arguments: {1}", VideoId, string.Join(" ", opts));

            int idleTimeoutMs = optionManager.GetGlobal(Options.Ytdl_IdleTimeout) * 60 * 1000;

            await ytdlService.UsingYoutubeDL(ytdl =>
            {
                // Deliberately NOT DownloadVideoJob.ProcessStdout: that handler treats a "Destination:"
                // line as the media file and rewrites Video.DownloadedPath from it. yt-dlp prints exactly
                // such a line for every subtitle it writes, so reusing it would leave DownloadedPath
                // pointing at "<title>.en". Verified against live output.
                ytdl.Run(opts, ProcessStdout, ProcessStderr,
                         timeoutMs: 30 * 60 * 1000,
                         idleTimeoutMs: idleTimeoutMs);
                return Task.CompletedTask;
            });

            // The exit code is ignored on purpose. --ignore-errors is passed so that one language failing
            // (YouTube rate-limits the caption endpoint per request) doesn't abort the run and lose both
            // the languages that did succeed and the info-json written afterwards. What actually landed on
            // disk is the answer, so read that instead.
            await ApplyResults();
        }

        private async Task ApplyResults()
        {
            var after = (await videoStorage.GetSubtitleFiles(video)).Select(s => s.Lang).ToList();
            writtenSubtitles.Clear();
            writtenSubtitles.AddRange(after);

            string infoJsonPath = video.DownloadedPath + ".info.json";
            bool keepInfoJson = optionManager.GetForSubscription(Options.Ytdl_WriteInfoJson, video.SubscriptionId);

            bool metadataApplied = false;
            try
            {
                if (File.Exists(infoJsonPath))
                {
                    metadataApplied = ApplyInfoJson(infoJsonPath);
                    if (!keepInfoJson)
                        File.Delete(infoJsonPath);
                }
                else
                {
                    JobLog("yt-dlp wrote no info-json, so metadata was left as it was.");
                }
            }
            catch (Exception ex)
            {
                // Subtitles are the point; a metadata hiccup must not fail the job.
                log.LogWarning(ex, "videoId={0}: could not apply the info-json", VideoId);
                JobLog("Could not read the metadata yt-dlp wrote; subtitles are unaffected.",
                       MessageSeverity.Warning);
            }

            // New sidecars change the on-disk footprint that counts against the user's quota.
            video.DownloadedSize = await videoStorage.CalculateSize(video);
            // Note: DownloadedPath is never reassigned here — no media was fetched.
            await dataContext.SaveChangesAsync();

            if (writtenSubtitles.Count > 0)
                JobLog($"Subtitles on disk: {string.Join(", ", writtenSubtitles)}"
                     + (metadataApplied ? "; metadata refreshed." : "."));
            else
                JobLog("yt-dlp returned no subtitles for this video.");
        }

        /// <summary>
        /// Folds yt-dlp's info-json into the video row. This is what makes the run pay for itself: the
        /// same extraction that fetched the subtitles also carries current metadata, so refreshing it
        /// costs no extra request.
        /// </summary>
        private bool ApplyInfoJson(string path)
        {
            var serializer = JsonSerializer.CreateDefault();
            serializer.MissingMemberHandling = MissingMemberHandling.Ignore;

            UrlInformation info;
            using (var reader = new JsonTextReader(new StreamReader(path)))
                info = serializer.Deserialize<UrlInformation>(reader);

            if (info == null)
                return false;

            if (!string.IsNullOrWhiteSpace(info.Title))
                video.Name = info.Title.Truncate(video.GetPropertyMaxLength("Name") ?? int.MaxValue);
            video.Description = info.Description;
            if (info.Timestamp != default)
                video.Published = info.Timestamp;
            video.UploaderName = info.Uploader ?? video.UploaderName;
            video.Duration = info.Duration.HasValue ? (int?)Math.Round(info.Duration.Value) : video.Duration;
            video.Views = info.ViewCount ?? video.Views;
            video.Likes = info.LikeCount ?? video.Likes;

            // Same projection as YouTubeDLProvider.UpdateMetadata: the wrapper POCO uses Newtonsoft
            // snake_case names, so the API side's {Start,End,Title} shape is written explicitly.
            video.Chapters = (info.Chapters != null && info.Chapters.Length > 0)
                ? System.Text.Json.JsonSerializer.Serialize(
                    info.Chapters.Select(c => new { Start = c.StartTime, End = c.EndTime, c.Title }))
                : video.Chapters;

            video.LastUpdated = DateTimeOffset.Now;
            video.EnrichedAt = DateTimeOffset.UtcNow;
            return true;
        }

        private void ProcessStdout(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            // Only the lines that say something happened; the progress spam for a 20 KB file is noise.
            if (line.Contains("Writing video subtitles", StringComparison.OrdinalIgnoreCase)
                || line.Contains("has already been downloaded", StringComparison.OrdinalIgnoreCase))
                JobLog(line.Trim());
        }

        private void ProcessStderr(string line)
        {
            if (!string.IsNullOrWhiteSpace(line))
                JobLog(line.Trim(), MessageSeverity.Warning);
        }

        /// <summary>
        /// The sidecar-only argument set.
        ///
        /// Everything to do with media is absent on purpose. --skip-download does not stop yt-dlp
        /// *resolving* formats, so a restrictive -f selector that matches nothing still fails the whole
        /// run even though no media is wanted; and remux/recode/merge are post-processors for a file that
        /// will not exist. SponsorBlock is excluded for the reason given in ExecuteJob.
        /// </summary>
        private IEnumerable<string> ResolveReprocessOptions(Video video)
            => ComposeArgs(
                antibotArgs: YtdlAntibotArgs.Build(optionManager, ytdlService.ImpersonateTargets, log,
                                                   subscriptionId: video.SubscriptionId),
                sleepArgs: YtdlCommonArgs.ServerSleep(optionManager),
                subtitleArgs: YtdlCommonArgs.Subtitles(optionManager, video.SubscriptionId, SubFormatPreference),
                retries: optionManager.GetForSubscription(Options.Ytdl_Retries, video.SubscriptionId),
                // video.DownloadedPath, NOT a freshly-resolved output path. If the subscription was
                // renamed since the download, ResolveOutputPath would render a different prefix and the
                // sidecars would land next to a file that isn't there — where subtitle discovery, which
                // keys off DownloadedPath, would never look for them.
                outputPath: video.DownloadedPath,
                url: video.OriginalUrl);

        /// <summary>
        /// Assembles the argument list from already-resolved values. Split out from the option reads so
        /// the *shape* can be asserted without a container: what this must never contain is as important
        /// as what it does.
        /// </summary>
        public static IEnumerable<string> ComposeArgs(
            IEnumerable<string> antibotArgs,
            IEnumerable<string> sleepArgs,
            IEnumerable<string> subtitleArgs,
            string retries,
            string outputPath,
            string url)
        {
            yield return "--color";
            yield return "no_color";

            // Without this, a single language failing (a 429 on the caption endpoint is common) raises
            // and yt-dlp abandons the video before writing the info-json — losing the metadata AND the
            // languages that did succeed. Verified against yt-dlp's own _write_subtitles error path,
            // where the non-ignoreerrors branch raises DownloadError and process_info returns early.
            yield return "--ignore-errors";

            yield return "--skip-download";

            foreach (var arg in antibotArgs ?? Enumerable.Empty<string>())
                yield return arg;

            foreach (var arg in sleepArgs ?? Enumerable.Empty<string>())
                yield return arg;

            if (retries != null)
            {
                yield return "-R";
                yield return retries;
            }

            yield return "--newline";

            foreach (var arg in subtitleArgs ?? Enumerable.Empty<string>())
                yield return arg;

            // Always written, then deleted afterwards unless the subscription actually wants it kept.
            // This is how one extraction yields both subtitles and fresh metadata.
            yield return "--write-info-json";

            yield return "-o";
            yield return outputPath;

            yield return url;
        }
    }
}
