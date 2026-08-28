using FormatWith;
using Humanizer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quartz;
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

        private readonly Regex ProgressRegex = new Regex(@"([\d\.]+)% of ~?([\d\.]+)([KMG]i?B)");
        private readonly Regex MergingRegex = new Regex(@"Merging formats into ""([^""]+)""");
        private readonly Regex AlreadyDownloadedRegex = new Regex(@"\[download\] (.*) has already been downloaded");
        private readonly Regex DestinationRegex = new Regex(@"Destination: (.*)");

        private static readonly string Data_VideoId = nameof(VideoId);

        private string outputPath = null;
        private Video video = null;
        private AsyncLock videoMutex = new AsyncLock();
        private CancellationTokenSource cancellationTokenSrc = new CancellationTokenSource();
        private DownloadCancellationRegistry.CancelContext cancelContext;
        private bool limitsChecked = false;
        private int lastReportedPercent = -1;

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
                                VideoManager videoManager) : base(logger, dataContext, jobTrackerService)
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
        }

        public static Task Schedule(RegardScheduler scheduler, Video video)
        {
            return scheduler.Schedule<DownloadVideoJob>(
                name: $"Download video {video}",
                jobData: new Dictionary<string, object>()
                {
                    { Data_VideoId, video.Id }
                },
                retryCount: 3,
                retryIntervalSecs: 15 * 60);
        }

        protected override async Task ExecuteJob(IJobExecutionContext context)
        {
            if (Job.JobData.TryGetValue(Data_VideoId, out object videoId))
                VideoId = Convert.ToInt32(videoId);

            video = dataContext.Videos.Find(VideoId);

            if (video == null)
            {
                Job.RetryCount = 0;
                throw new ArgumentException($"Download failed - invalid video id {VideoId}.");
            }

            if (video.DownloadedPath != null)
            {
                Job.RetryCount = 0;
                throw new ArgumentException($"Download failed - video {VideoId} is already downloaded!");
            }

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
                if (float.TryParse(match.Groups[1].Value, out float percent))
                {
                    videoDownloader.OnVideoDownloading(VideoId, percent / 100f);

                    // Throttle: yt-dlp emits many progress lines/sec; push to the bell only when the
                    // whole percent changes. Progress ticks are deliberately kept out of the job log.
                    int p = (int)percent;
                    if (p != lastReportedPercent)
                    {
                        lastReportedPercent = p;
                        ReportProgress(percent / 100f, "Downloading");
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

            // TODO: Network Options
            // TODO: Geo Restriction

            #region Download Options

            string limitRate = optionManager.GetForSubscription(Options.Ytdl_LimitRate, video.SubscriptionId);
            if (limitRate != null)
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
