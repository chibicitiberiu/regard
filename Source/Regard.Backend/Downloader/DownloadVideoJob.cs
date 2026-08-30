using FormatWith;
using Humanizer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quartz;
using Regard.Backend.Common.Model;
using Regard.Backend.Common.Services;
using Regard.Backend.Common.Utils;
using Regard.Common.SponsorBlock;
using Regard.Backend.DB;
using Regard.Backend.Model;
using Regard.Backend.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Regard.Common.Utils;
using Nito.AsyncEx;
using Regard.Backend.Jobs;
using Regard.Backend.Metadata;
using System.Threading;
using Humanizer.Bytes;
using Regard.Backend.Configuration;

namespace Regard.Backend.Downloader
{
    // Resume after a restart: one-off, tied to a specific video, and idempotent (no-ops if the file is
    // already present). A stranded download otherwise waits for the next global sync to re-queue it.
    [ResumeAfterRestart]
    public class DownloadVideoJob : JobBase
    {
        protected readonly IConfiguration configuration;
        protected readonly IOptionManager optionManager;
        protected readonly IYoutubeDlService ytdlService;
        protected readonly IVideoDownloaderService videoDownloader;
        protected readonly IVideoStorageService videoStorage;
        protected readonly MetadataService metadataService;
        protected readonly UserQuotaService userQuotaService;
        protected readonly DownloadCancellationRegistry cancellationRegistry;
        protected readonly VideoManager videoManager;
        protected readonly HostThrottle hostThrottle;
        protected readonly NotificationService notificationService;

        // Host whose download slot this run reserved (released in OnAfterExecute); null when deferred.
        private string reservedHost = null;

        // Matches yt-dlp's progress line, e.g.
        //   [download]  45.2% of ~  12.34MiB at    1.23MiB/s ETA 00:12 (frag 3/17)
        // Groups 1-3 (percent, size, unit) are deliberately unchanged from the original pattern: the
        // progress pie and the size-quota guard both read them, so widening must not disturb them. Speed
        // and ETA are appended as OPTIONAL groups — yt-dlp omits them at the start of a download and
        // prints "Unknown B/s" / "ETA Unknown" when it can't estimate, so they have to tolerate absence
        // rather than make the whole line fail to match (which would kill the pie).
        private readonly Regex ProgressRegex = new Regex(
            @"([\d\.]+)% of\s+~?\s*([\d\.]+)([KMG]i?B)" +
            @"(?:\s+at\s+(?:([\d\.]+\s*[KMG]?i?B/s)|Unknown\s*B/s))?" +
            @"(?:\s+ETA\s+(?:([\d:]+)|Unknown))?");
        private readonly Regex MergingRegex = new Regex(@"Merging formats into ""([^""]+)""");
        private readonly Regex AlreadyDownloadedRegex = new Regex(@"\[download\] (.*) has already been downloaded");
        private readonly Regex DestinationRegex = new Regex(@"Destination: (.*)");

        private static readonly string Data_VideoId = nameof(VideoId);
        private static readonly string Data_Forced = "Forced";

        private string outputPath = null;
        private Video video = null;
        private AsyncLock videoMutex = new AsyncLock();
        private CancellationTokenSource cancellationTokenSrc = new CancellationTokenSource();
        private DownloadCancellationRegistry.CancelContext cancelContext;
        private bool limitsChecked = false;
        private int lastReportedPercent = -1;

        /// <summary>
        /// "Download again": ignore the already-downloaded no-op and re-fetch. Read from job data in
        /// both ShouldDefer and ExecuteJob, and consumed (cleared from job data) once the old files are
        /// gone — see PrepareForcedRedownload for why it must not stick around.
        /// </summary>
        private bool forced = false;

        /// <summary>Set when this run took the already-downloaded no-op, to suppress the success card.</summary>
        private bool noOpped = false;

        // Latest figures scraped off yt-dlp's progress line, for the notification card and the Job Log.
        private string lastSpeed = null;
        private string lastEta = null;
        private string lastTotalSize = null;
        private DateTime lastProgressReport = DateTime.MinValue;
        private static readonly TimeSpan ProgressReportInterval = TimeSpan.FromSeconds(1);

        private string DescribeProgress() => FormatProgress(lastSpeed, lastEta, lastTotalSize);

        /// <summary>
        /// "1.23MiB/s · ETA 00:42 · 512.00MiB" — whatever yt-dlp actually gave us. Any of the three can be
        /// missing: yt-dlp omits speed and ETA at the start of a download and prints "Unknown" when it
        /// can't estimate, and the parser deliberately drops those rather than showing "at Unknown".
        /// Falls back to plain "Downloading" so the card is never blank.
        /// </summary>
        public static string FormatProgress(string speed, string eta, string totalSize)
        {
            var parts = new List<string>(3);
            if (!string.IsNullOrWhiteSpace(speed)) parts.Add(speed.Trim());
            if (!string.IsNullOrWhiteSpace(eta)) parts.Add("ETA " + eta.Trim());
            if (!string.IsNullOrWhiteSpace(totalSize)) parts.Add(totalSize.Trim());
            return parts.Count > 0 ? string.Join(" · ", parts) : "Downloading";
        }

        /// <summary>The bell card's text: the video name, plus whatever progress figures we have.</summary>
        public static string FormatCard(string videoName, string progress)
        {
            bool haveStats = progress != "Downloading";
            if (string.IsNullOrEmpty(videoName))
                return haveStats ? progress : null;
            return haveStats ? $"{videoName} — {progress}" : videoName;
        }

        public int VideoId { get; set; }


        public DownloadVideoJob(ILogger<DownloadVideoJob> logger,
                                DataContext dataContext,
                                JobTrackerService jobTrackerService,
                                IConfiguration configuration,
                                IOptionManager optionManager,
                                IYoutubeDlService ytdlService,
                                IVideoDownloaderService videoDownloader,
                                IVideoStorageService videoStorage,
                                MetadataService metadataService,
                                UserQuotaService userQuotaService,
                                DownloadCancellationRegistry cancellationRegistry,
                                VideoManager videoManager,
                                HostThrottle hostThrottle,
                                NotificationService notificationService) : base(logger, dataContext, jobTrackerService)
        {
            this.configuration = configuration;
            this.optionManager = optionManager;
            this.ytdlService = ytdlService;
            this.videoDownloader = videoDownloader;
            this.videoStorage = videoStorage;
            this.metadataService = metadataService;
            this.userQuotaService = userQuotaService;
            this.cancellationRegistry = cancellationRegistry;
            this.videoManager = videoManager;
            this.hostThrottle = hostThrottle;
            this.notificationService = notificationService;
        }

        private static string QueuedNotificationKey(int videoId) => $"download:{videoId}";

        protected override async Task<DateTimeOffset?> ShouldDefer(IJobExecutionContext context)
        {
            if (Job.JobData.TryGetValue(Data_VideoId, out object vidObj))
                VideoId = Convert.ToInt32(vidObj);
            forced = ReadForcedFlag();

            // Load into the `video` field (not a local) so the "Downloading" ongoing notification, posted
            // by OnJobStarted BEFORE ExecuteJob runs, already has the video's name.
            video = dataContext.Videos.Find(VideoId);

            // A forced re-download still has to reserve a throttle slot like any other: skipping the
            // reservation here would let it bypass HostThrottle entirely and never release a slot.
            if (video == null || (video.DownloadedPath != null && !forced))
                return null;   // invalid / already downloaded — let ExecuteJob take its error/no-op path

            string host = UrlHostKey.Of(video.OriginalUrl);
            if (hostThrottle.TryReserveDownload(host, VideoId, out var retryAt))
            {
                reservedHost = host;   // released in OnAfterExecute
                return null;           // proceed now
            }

            // Deferred: surface a persistent per-video "Queued for download" notification (keyed by video,
            // so it survives the reschedule cycle) with the queue position. No minute estimate — the wait
            // is a scheduling artefact, not the download's duration, so "~1 min" only ever misled.
            int pos = hostThrottle.QueuePosition(host, VideoId);
            string detail = pos > 1
                ? $"{video.Name} — position {pos} in the {host} download queue"
                : $"{video.Name} — waiting for a {host} download slot";
            _ = notificationService.PostOrUpdate(
                null, QueuedNotificationKey(VideoId),
                "Queued for download", detail,
                NotificationSeverity.Info, progress: null, ongoing: true,
                videoDbId: VideoId, jobId: Job.Id,
                // Cancellable even though nothing is running yet: the pending trigger can be dropped,
                // and waiting out a long throttle queue is exactly when a user wants to call it off.
                primaryAction: NotificationPrimaryAction.None, cancellable: true);

            return retryAt;
        }

        protected override void OnAfterExecute()
        {
            if (reservedHost != null)
            {
                hostThrottle.ReleaseDownload(reservedHost);
                reservedHost = null;
            }
            // A download attempt ran to completion — clear the dedup flag so this video can be scheduled
            // again later. (Deferred/rescheduled runs don't reach here, so they stay "known" while pending.)
            hostThrottle.ClearKnown(VideoId);
        }

        /// <summary>
        /// Queues a download. <paramref name="forced"/> is the user's "Download again": it makes the job
        /// wipe the video's existing files and re-fetch instead of no-opping. Never pass it from
        /// automatic download or restart reconciliation — the no-op is what makes those idempotent.
        /// </summary>
        public static Task Schedule(RegardScheduler scheduler, Video video, bool forced = false)
        {
            var jobData = new Dictionary<string, object>()
            {
                { Data_VideoId, video.Id }
            };

            // Only written when true, so every pre-existing job row (and every non-forced schedule)
            // reads back as false without needing a migration — JobDataJson is free-form.
            if (forced)
                jobData[Data_Forced] = true;

            return scheduler.Schedule<DownloadVideoJob>(
                name: forced ? $"Re-download video {video}" : $"Download video {video}",
                jobData: jobData,
                retryCount: 3,
                retryIntervalSecs: 15 * 60);
        }

        // Live "in progress" notification. video is only loaded once ExecuteJob runs, so the very first
        // (pre-load) tick just says "Downloading"; every progress tick after that carries the title.
        protected override JobNotification GetOngoingNotification()
            => new JobNotification
            {
                Title = "Downloading",
                // Speed/ETA/size ride in the card text: Notification has no spare column for them, and
                // adding one would mean a migration in both contexts for something purely cosmetic.
                Text = DescribeCard(),
                VideoDbId = VideoId,
            };

        private string DescribeCard() => FormatCard(video?.Name, DescribeProgress());

        // "Download complete" — click opens the (now downloaded) video. Suppressed when the run took the
        // already-downloaded no-op: the job legitimately succeeds, but announcing a completed download
        // that never happened is how "Download again" appeared to work while doing nothing.
        protected override JobNotification GetSuccessNotification()
            => (video == null || noOpped) ? null : new JobNotification
            {
                Title = "Download complete",
                Text = video.Name,
                VideoDbId = video.Id,
                PrimaryAction = NotificationPrimaryAction.OpenVideo,
            };

        // "Download failed" — Error + VideoDbId makes the bell show a Retry button; the body click goes
        // to the job logs. Null-guarded: an early failure (invalid id) throws before video is loaded.
        protected override JobNotification GetFailureNotification(Exception ex)
            => video == null ? null : new JobNotification
            {
                Title = "Download failed",
                Text = video.Name,
                VideoDbId = video.Id,
            };

        /// <summary>
        /// Reads the "Download again" flag. Job data round-trips through JSON, so the value comes back as
        /// a boxed long/JsonElement rather than a bool — hence Convert rather than a cast. A missing key
        /// (every job row created before this feature, and every automatic download) reads as false.
        /// </summary>
        private bool ReadForcedFlag()
        {
            try
            {
                return Job.JobData.TryGetValue(Data_Forced, out var value)
                    && value != null
                    && Convert.ToBoolean(value);
            }
            catch (Exception ex)
            {
                // Never let a malformed flag break a download; the safe reading is "not forced".
                log.LogWarning(ex, "videoId={0}: could not read the forced flag, treating as false", VideoId);
                return false;
            }
        }

        /// <summary>
        /// Clears the way for a genuine re-download: delete the old files, forget the download state, and
        /// consume the flag.
        ///
        /// Two things here are load-bearing and easy to get wrong.
        ///
        /// The sweep covers TWO paths. Video.DownloadedPath is written only when a download *succeeds*,
        /// so a download that died midway leaves a .part behind that VideoStorageService.GetFiles cannot
        /// see. The freshly-resolved output path finds those. Missing this is precisely the reported bug:
        /// the files were "missing or incomplete" and a re-download quietly resumed the stale fragment.
        /// Deleting rather than passing --force-overwrites also handles the case where the output
        /// template changed (a renamed subscription) and the old files sit at a different path.
        ///
        /// The flag is consumed. TryResume (restart reconciliation) and JobRetryService both re-fire the
        /// SAME JobInfo row, so a flag left in job data would survive into later runs — and a crash right
        /// after a completed forced download would then delete that finished file and fetch it again.
        /// Clearing it makes "forced" a one-shot instruction.
        /// </summary>
        private async Task PrepareForcedRedownload()
        {
            JobLog($"Re-downloading video {VideoId}: removing any existing files first.");

            int deleted = 0;
            try
            {
                // Order matters: sweep before clearing DownloadedPath, because GetFiles keys off it and
                // would find nothing afterwards.
                if (video.DownloadedPath != null)
                    deleted += await videoStorage.DeleteAt(video.DownloadedPath);

                var resolved = ResolveOutputPath(video);
                if (resolved != null && resolved != video.DownloadedPath)
                    deleted += await videoStorage.DeleteAt(resolved);
            }
            catch (Exception ex)
            {
                // A failed sweep is not fatal — yt-dlp may still overwrite. Say so and carry on.
                log.LogWarning(ex, "videoId={0}: could not fully clear previous download", VideoId);
                JobLog("Could not fully remove the previous download; continuing anyway.",
                       Regard.Backend.Common.Model.MessageSeverity.Warning);
            }

            JobLog(deleted > 0
                ? $"Removed {deleted} existing file(s)."
                : "No existing files found to remove.");

            video.DownloadedPath = null;
            video.DownloadedSize = null;

            // A pending "mark for deletion" is dropped on purpose: the user just asked for this video
            // back, and leaving the mark would let the deletion sweep remove the fresh download. Logged
            // rather than silent, because DeleteScheduledAt is pushed live and the badge will visibly
            // clear.
            if (video.DeleteScheduledAt != null)
            {
                JobLog("Cleared this video's pending deletion — it's being downloaded again.");
                video.DeleteScheduledAt = null;
            }

            // Consume the flag so retries and restart-resume behave as a plain download, and do it in
            // the SAME save as the state clear. Two saves would leave a window where a kill -9 lands
            // with the files gone but Forced still true on disk.
            forced = false;
            Job.JobData.Remove(Data_Forced);
            dataContext.Jobs.Update(Job);

            await dataContext.SaveChangesAsync();
        }

        protected override async Task ExecuteJob(IJobExecutionContext context)
        {
            if (Job.JobData.TryGetValue(Data_VideoId, out object videoId))
                VideoId = Convert.ToInt32(videoId);
            forced = ReadForcedFlag();

            video = dataContext.Videos.Find(VideoId);

            if (video == null)
            {
                Job.RetryCount = 0;
                throw new ArgumentException($"Download failed - invalid video id {VideoId}.");
            }

            if (forced)
            {
                await PrepareForcedRedownload();
            }
            else if (video.DownloadedPath != null)
            {
                // Already downloaded (a duplicate schedule, or it got fetched between queueing and running).
                // Nothing to do — succeed quietly instead of failing the job.
                noOpped = true;
                JobLog($"Video {VideoId} is already downloaded — nothing to do.");
                log.LogInformation("videoId={0}: already downloaded, skipping (no-op)", VideoId);
                return;
            }

            // Proceeding (throttle slot reserved): clear any "Queued for download" card — the job's live
            // "Downloading" notification now takes over.
            _ = notificationService.Remove(null, QueuedNotificationKey(VideoId));

            // Hard-quota gate: block (and explain) before spending any bandwidth if the user is
            // already at/over their count or size quota. Manual downloads otherwise bypass the count
            // quota entirely (only size was checked, mid-download).
            var quotaSub = dataContext.Subscriptions.Find(video.SubscriptionId);
            if (quotaSub != null)
            {
                var (countQuota, sizeQuotaBytes) = userQuotaService.GetHardQuota(quotaSub.UserId);
                var usage = userQuotaService.GetUsage(quotaSub.UserId);

                if (countQuota.HasValue && usage.Count >= countQuota.Value)
                {
                    Job.RetryCount = 0;
                    var msg = $"Can't download: video quota reached ({usage.Count} / {countQuota.Value} videos). " +
                              "Delete some downloads or ask an admin to raise your quota.";
                    JobLog(msg, Regard.Backend.Common.Model.MessageSeverity.Error);
                    throw new Exception(msg);
                }
                if (sizeQuotaBytes.HasValue && usage.Bytes >= sizeQuotaBytes.Value)
                {
                    Job.RetryCount = 0;
                    var msg = $"Can't download: storage quota reached ({usage.Bytes.Bytes()} / {sizeQuotaBytes.Value.Bytes()}). " +
                              "Delete some downloads or ask an admin to raise your quota.";
                    JobLog(msg, Regard.Backend.Common.Model.MessageSeverity.Error);
                    throw new Exception(msg);
                }
            }

            // Videos listed flat during sync (EnrichedAt == null) need full metadata before we build the
            // output path / NFO. This covers every download path — including auto-download with
            // DownloadOrder=Oldest, which targets the older, still-flat videos. No-op once enriched.
            await videoManager.EnsureEnriched(video);

            var opts = ResolveDownloadOptions(video).ToArray();

            log.LogInformation("Running youtube-dl with arguments: {0}", string.Join(" ", opts));

            int idleTimeoutMs = optionManager.GetGlobal(Options.Ytdl_IdleTimeout) * 60 * 1000;

            // Register a cancellation context so the API can cancel this specific download. It shares
            // its token source with the size-quota abort below, and its UserCancelled flag tells the two
            // apart in the catch.
            cancelContext = cancellationRegistry.Register(Job.Id);
            cancellationTokenSrc = cancelContext.Cts;

            try
            {
                await ytdlService.UsingYoutubeDL(ytdl =>
                {
                    int resultCode = ytdl.Run(opts,
                        ProcessStdout,
                        ProcessStderr,
                        timeoutMs: 24 * 3600 * 1000,
                        cancellationToken: cancellationTokenSrc.Token,
                        idleTimeoutMs: idleTimeoutMs);

                    if (resultCode != 0)
                        throw new Exception($"videoId={VideoId}: Download failed!\n");

                    return Task.CompletedTask;
                });
            }
            catch (OperationCanceledException)
            {
                Job.RetryCount = 0;

                if (cancelContext.UserCancelled)
                {
                    // User cancel: flag the video so auto-download skips it and the next-newest takes
                    // its slot; it stays visible and can be downloaded manually later.
                    log.LogInformation("videoId={0}: download cancelled by user; marking as skipped.", VideoId);
                    using (await videoMutex.LockAsync())
                    {
                        video.DownloadSkipped = true;
                        await dataContext.SaveChangesAsync();
                    }
                    JobLog("Download cancelled; video skipped (won't auto-download). Download it manually to retry.", Regard.Backend.Common.Model.MessageSeverity.Warning);
                    throw new JobCancelledException();
                }

                log.LogInformation("videoId={0}: download stopped (quota/limit).", VideoId);
                throw;
            }
            finally
            {
                cancellationRegistry.Unregister(Job.Id);
                videoDownloader.OnDownloadFinished(VideoId);
            }

            using (var @lock = await videoMutex.LockAsync())
            {
                video.DownloadedPath = outputPath;
                video.DownloadedSize = await videoStorage.CalculateSize(video);
                // Record whether SponsorBlock cut segments out of this file, so the in-player Skip never
                // trusts original-timeline segments against a shortened file (see Video.SponsorsRemoved).
                video.SponsorsRemoved = VideoEmbedHelper.IsYouTube(video)
                    && SponsorBlockActions.CategoriesWith(
                            optionManager.GetForSubscription(Options.Sponsorblock_Actions, video.SubscriptionId),
                            SbAction.Remove).Count > 0;
                await dataContext.SaveChangesAsync();
            }

            // The "now downloaded" state reaches connected clients through the live change feed
            // (ChangeFeedInterceptor), which observes the SaveChanges above.

            if (configuration.GetValue<bool>("Metadata:Enabled"))
                await WriteEpisodeMetadata();

            log.LogInformation($"videoId={VideoId}: Download completed!");
        }

        /// <summary>
        /// Writes the Jellyfin episode NFO next to the downloaded file and renames yt-dlp's
        /// thumbnail (written via --write-thumbnail) to the &lt;basename&gt;-thumb.jpg convention.
        /// Best-effort: never fails the (already-committed) download.
        /// </summary>
        private async Task WriteEpisodeMetadata()
        {
            try
            {
                var sub = dataContext.Subscriptions.Find(video.SubscriptionId);
                await metadataService.WriteEpisodeNfo(video, sub, outputPath);

                var thumbSrc = new[] { ".jpg", ".jpeg", ".png", ".webp" }
                    .Select(ext => outputPath + ext)
                    .FirstOrDefault(File.Exists);
                if (thumbSrc != null)
                    File.Move(thumbSrc, outputPath + "-thumb" + Path.GetExtension(thumbSrc), overwrite: true);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "videoId={0}: failed to write Jellyfin metadata.", VideoId);
            }
        }

        private async Task UpdateOutputPath(string newOutputPath)
        {
            using var @lock = await videoMutex.LockAsync();
            outputPath = newOutputPath;
            if (video.DownloadedPath != null && video.DownloadedPath != newOutputPath)
            {
                video.DownloadedPath = newOutputPath;
                video.DownloadedSize = await videoStorage.CalculateSize(video);
                await dataContext.SaveChangesAsync();
            }
        }

        private async void ProcessStdout(string message)
        {
            if (message == null)
                return;

            log.LogInformation($"videoId={VideoId}: {message}");

            Match match;
            if (DestinationRegex.TryMatch(message, out match)
                || MergingRegex.TryMatch(message, out match)
                || AlreadyDownloadedRegex.TryMatch(message, out match))
            {
                JobLog(message);
                await UpdateOutputPath(Path.ChangeExtension(match.Groups[1].Value, null));
            }
            else if (ProgressRegex.TryMatch(message, out match))
            {
                // Optional groups: absent early in a download and when yt-dlp prints "Unknown".
                lastSpeed = match.Groups[4].Success ? match.Groups[4].Value.Trim() : null;
                lastEta = match.Groups[5].Success ? match.Groups[5].Value.Trim() : null;
                lastTotalSize = $"{match.Groups[2].Value}{match.Groups[3].Value}";

                if (float.TryParse(match.Groups[1].Value, out float percent))
                {
                    videoDownloader.OnVideoDownloading(VideoId, percent / 100f);

                    // Throttle: yt-dlp emits many progress lines/sec, and every push writes a
                    // notification row, so rate-limit by time rather than per line. One update a second
                    // keeps the speed/ETA readable without turning a download into ~100 DB writes; the
                    // whole-percent check keeps the pie moving on slow downloads that would otherwise sit
                    // still between ticks.
                    int p = (int)percent;
                    var now = DateTime.UtcNow;
                    if (p != lastReportedPercent || now - lastProgressReport >= ProgressReportInterval)
                    {
                        lastReportedPercent = p;
                        lastProgressReport = now;
                        ReportProgress(percent / 100f, DescribeProgress());
                    }
                }

                if (!limitsChecked && double.TryParse(match.Groups[2].Value, out double size))
                    ProcessFileSize(size, match.Groups[3].Value);
            }
            else
            {
                JobLog(message);
            }
        }

        private void ProcessFileSize(double size, string unit)
        {
            // Get size in bytes
            unit = unit.ToLower();
            
            int mul = 1;
            if (unit[0] == 'k') mul = 1024;
            else if (unit[0] == 'm') mul = 1024 * 1024;
            else if (unit[0] == 'g') mul = 1024 * 1024 * 1024;

            long sizeBytes = Convert.ToInt64(size * mul);

            // Check if it is within limits
            var sub = dataContext.Subscriptions.Find(video.SubscriptionId);
            var maxSize = videoDownloader.DetermineMaximumAllowedSize(sub);
            
            if (maxSize.HasValue && sizeBytes > maxSize.Value)
            {
                log.LogError($"Stopping download of {VideoId}, as the video has {sizeBytes.Bytes()} which would go above the allowed limit of {maxSize.Value.Bytes()}");
                JobLog($"Download stopped: this video is {sizeBytes.Bytes()}, which would exceed your remaining storage quota ({maxSize.Value.Bytes()}). " +
                       "Delete some downloads or ask an admin to raise your quota.", Regard.Backend.Common.Model.MessageSeverity.Error);

                // Cancel download
                cancellationTokenSrc.Cancel();
            }

            limitsChecked = true;
        }

        private void ProcessStderr(string message)
        {
            if (message == null)
                return;

            log.LogError($"videoId={VideoId}: {message}");
            JobLog(message, Regard.Backend.Common.Model.MessageSeverity.Error);
        }

        private IEnumerable<string> ResolveDownloadOptions(Video video)
        {
            yield return "--color";
            yield return "no_color";

            // Network / anti-bot options (server-wide): cookies + inter-request sleep, and a randomized
            // per-download sleep so a batch doesn't hammer YouTube back-to-back.
            foreach (var arg in YtdlAntibotArgs.Build(optionManager, ytdlService.ImpersonateTargets, log))
                yield return arg;

            if (optionManager.GetGlobal(Options.Server_Throttle_Enabled))
            {
                int sleepMin = optionManager.GetGlobal(Options.Server_Ytdl_SleepInterval);
                int sleepMax = optionManager.GetGlobal(Options.Server_Ytdl_MaxSleepInterval);
                if (sleepMin > 0)
                {
                    yield return "--sleep-interval";
                    yield return sleepMin.ToString();
                    if (sleepMax > sleepMin)
                    {
                        yield return "--max-sleep-interval";
                        yield return sleepMax.ToString();
                    }
                }
            }
            // TODO: Geo Restriction

            #region Download Options

            // Per-subscription bandwidth cap, else the server-wide default (a real viewer doesn't saturate).
            string limitRate = optionManager.GetForSubscription(Options.Ytdl_LimitRate, video.SubscriptionId);
            if (string.IsNullOrWhiteSpace(limitRate))
                limitRate = optionManager.GetGlobal(Options.Server_Ytdl_LimitRate);
            if (!string.IsNullOrWhiteSpace(limitRate))
            {
                yield return "-r";
                yield return limitRate;
            }

            string retries = optionManager.GetForSubscription(Options.Ytdl_Retries, video.SubscriptionId); 
            if (retries != null)
            {
                yield return "-R";
                yield return retries;
            }

            #endregion

            #region Filesystem Options

            if (optionManager.GetForSubscription(Options.Ytdl_WriteDescription, video.SubscriptionId))
                yield return "--write-description";

            if (optionManager.GetForSubscription(Options.Ytdl_WriteInfoJson, video.SubscriptionId))
                yield return "--write-info-json";

            #endregion

            #region Thumbnail images

            bool metadataEnabled = configuration.GetValue<bool>("Metadata:Enabled");

            if (optionManager.GetForSubscription(Options.Ytdl_WriteThumbnail, video.SubscriptionId) || metadataEnabled)
                yield return "--write-thumbnail";

            if (metadataEnabled)
            {
                // Jellyfin episode images must be JPEG; convert whatever YouTube serves (often webp).
                yield return "--convert-thumbnails";
                yield return "jpg";
            }

            #endregion

            #region Verbosity / Simulation Options

            yield return "--newline";

            #endregion

            // TODO: workarounds

            #region Video Format Options

            // Format selector: an explicit raw override wins; otherwise compose one from the
            // structured resolution/codec options.
            string rawFormat = optionManager.GetForSubscription(Options.Ytdl_Format, video.SubscriptionId);
            string format;
            if (!string.IsNullOrWhiteSpace(rawFormat))
            {
                format = rawFormat;
            }
            else
            {
                int maxRes = optionManager.GetForSubscription(Options.Ytdl_MaxResolution, video.SubscriptionId);
                string exVideo = optionManager.GetForSubscription(Options.Ytdl_ExcludedVideoCodecs, video.SubscriptionId);
                string exAudio = optionManager.GetForSubscription(Options.Ytdl_ExcludedAudioCodecs, video.SubscriptionId);

                var vf = new StringBuilder();
                if (maxRes > 0)
                    vf.Append($"[height<={maxRes}]");
                foreach (var codec in SplitCodecs(exVideo))
                    vf.Append($"[vcodec!*={codec}]");

                var af = new StringBuilder();
                foreach (var codec in SplitCodecs(exAudio))
                    af.Append($"[acodec!*={codec}]");

                // Prefer the filtered separate streams, then a filtered combined stream, then a bare
                // fallback so the selector can never match zero formats (the final /best carries no
                // codec filter on purpose: downloading something beats failing).
                format = $"bestvideo{vf}+bestaudio{af}/best{vf}/best";
            }

            yield return "-f";
            yield return format;

            if (optionManager.GetForSubscription(Options.Ytdl_PreferFreeFormats, video.SubscriptionId))
                yield return "--prefer-free-formats";

            // If a transcode target is set, merge straight into that container (a free remux for the
            // merge case) and apply the chosen conversion; otherwise emit the configured merge format.
            string transcodeTarget = optionManager.GetForSubscription(Options.Ytdl_TranscodeVideo, video.SubscriptionId);
            if (!string.IsNullOrWhiteSpace(transcodeTarget))
            {
                yield return "--merge-output-format";
                yield return transcodeTarget;

                string mode = optionManager.GetForSubscription(Options.Ytdl_TranscodeMode, video.SubscriptionId);
                yield return string.Equals(mode, "recode", StringComparison.OrdinalIgnoreCase)
                    ? "--recode-video"
                    : "--remux-video";
                yield return transcodeTarget;
            }
            else
            {
                string mergeOutputFormat = optionManager.GetForSubscription(Options.Ytdl_MergeOutputFormat, video.SubscriptionId);
                if (mergeOutputFormat != null)
                {
                    yield return "--merge-output-format";
                    yield return mergeOutputFormat;
                }
            }

            #endregion

            #region Subtitle Options

            bool writeSubs = optionManager.GetForSubscription(Options.Ytdl_WriteSubtitles, video.SubscriptionId);
            bool writeAutoSubs = optionManager.GetForSubscription(Options.Ytdl_WriteAutoSub, video.SubscriptionId);

            if (writeSubs)
                yield return "--write-subs";

            if (writeAutoSubs)
                yield return "--write-auto-subs";

            // Language + format only make sense when we're actually writing subtitles. Emitting them
            // unconditionally (the options have non-null defaults) sent yt-dlp noise on every download,
            // and "all languages" was silently overridden by the default --sub-langs en that followed
            // it. Gate on enabled, and keep "all" mutually exclusive with a specific language list.
            if (writeSubs || writeAutoSubs)
            {
                if (optionManager.GetForSubscription(Options.Ytdl_AllSubs, video.SubscriptionId))
                {
                    yield return "--sub-langs";
                    yield return "all";
                }
                else
                {
                    string subLang = optionManager.GetForSubscription(Options.Ytdl_SubLang, video.SubscriptionId);
                    if (!string.IsNullOrWhiteSpace(subLang))
                    {
                        yield return "--sub-langs";
                        yield return subLang;
                    }
                }

                string subFormat = optionManager.GetForSubscription(Options.Ytdl_SubFormat, video.SubscriptionId);
                if (!string.IsNullOrWhiteSpace(subFormat))
                {
                    yield return "--sub-format";
                    yield return subFormat;
                }
            }

            #endregion

            #region SponsorBlock (YouTube only)

            // Chapter/Remove are applied here by yt-dlp; the Skip action is non-destructive and handled
            // in the player (SponsorSegments enrichment + Watch page), so it's ignored at download time.
            if (VideoEmbedHelper.IsYouTube(video))
            {
                string sbActions = optionManager.GetForSubscription(Options.Sponsorblock_Actions, video.SubscriptionId);
                var chapterCats = SponsorBlockActions.CategoriesWith(sbActions, SbAction.Chapter);
                var removeCats = SponsorBlockActions.CategoriesWith(sbActions, SbAction.Remove);

                if (chapterCats.Count > 0)
                {
                    yield return "--sponsorblock-mark";
                    yield return string.Join(",", chapterCats);
                }

                if (removeCats.Count > 0)
                {
                    yield return "--sponsorblock-remove";
                    yield return string.Join(",", removeCats);

                    // A cut file needs its sidecar subtitles re-timed to match. yt-dlp's ModifyChapters PP
                    // only re-times formats it can parse (srt/vtt/ass); the default "best" can yield json3
                    // for auto-subs. If subs are being written, force a convertible format so they stay synced.
                    if (optionManager.GetForSubscription(Options.Ytdl_WriteSubtitles, video.SubscriptionId)
                        || optionManager.GetForSubscription(Options.Ytdl_WriteAutoSub, video.SubscriptionId))
                    {
                        yield return "--convert-subs";
                        yield return "srt";
                    }
                }
            }

            #endregion

            // TODO: maybe add more options?
            yield return "-o";
            outputPath = ResolveOutputPath(video);
            yield return outputPath;

            yield return video.OriginalUrl;
        }

        /// <summary>
        /// Splits a comma-separated codec-token list into trimmed, non-empty tokens.
        /// </summary>
        private static IEnumerable<string> SplitCodecs(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
                yield break;
            foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                yield return part;
        }

        private string ResolveOutputPath(Video video)
        {
            var sub = dataContext.Subscriptions.Find(video.SubscriptionId);
            string format = optionManager.GetForSubscription(Options.Subscriptions_DownloadPath, video.SubscriptionId);
            string path = format.FormatWith(new
            {
                DataDirectory = configuration["DataDirectory"],
                DownloadDirectory = configuration["DownloadDirectory"],
                Video = video,
                Subscription = sub,
                FolderPath = GetFolderPath(sub),
                EpisodeCode = metadataService.EpisodeCode(video),
                Env = Environment.GetEnvironmentVariables(),
            }, MissingKeyBehaviour.ThrowException);

            // Normalize path
            path = path.Replace('\\', Path.DirectorySeparatorChar);
            path = path.Replace('/', Path.DirectorySeparatorChar);

            // Collapse repeated separators left by an empty template segment — a root-level
            // subscription has a blank {FolderPath}, so the default template yields "videos//CGP Grey".
            // Harmless to the OS, but it keeps the stored DownloadedPath clean. Preserve a single
            // leading separator so an absolute DownloadDirectory stays absolute.
            var sep = Path.DirectorySeparatorChar;
            bool rooted = path.StartsWith(sep);
            path = string.Join(sep, path.Split(sep, StringSplitOptions.RemoveEmptyEntries));
            if (rooted)
                path = sep + path;

            path = MakeValidPath(path);
            return path;
        }

        private string GetFolderPath(Subscription sub)
        {
            IList<string> items = new List<string>();
            int? parentId = sub.ParentFolderId;

            while (parentId.HasValue)
            {
                var folder = dataContext.SubscriptionFolders.Find(parentId.Value);
                items.Add(MakeValidPath(folder.Name, invalidChars: Path.GetInvalidFileNameChars()));
                parentId = folder.ParentId;
            }

            return string.Join(Path.DirectorySeparatorChar, items.Reverse());
        }

        /// <summary>Replaces characters in <c>text</c> that are not allowed in 
        /// file names with the specified replacement character.</summary>
        /// <param name="text">Text to make into a valid filename. The same string is returned if it is valid already.</param>
        /// <param name="replacement">Replacement character, or null to simply remove bad characters.</param>
        /// <returns>A string that can be used as a filename. If the output string would otherwise be empty, returns "_".</returns>
        private static string MakeValidPath(string text, char? replacement = '_', char[] invalidChars = null)
        {
            text = text.Trim();

            StringBuilder sb = new StringBuilder(text.Length);
            var invalids = invalidChars ?? Path.GetInvalidPathChars();
            bool changed = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (invalids.Contains(c))
                {
                    changed = true;
                    var repl = replacement ?? '\0';
                    if (repl != '\0')
                        sb.Append(repl);
                }
                else
                    sb.Append(c);
            }
            if (sb.Length == 0)
                return "_";
            return changed ? sb.ToString() : text;
        }
    }
}
