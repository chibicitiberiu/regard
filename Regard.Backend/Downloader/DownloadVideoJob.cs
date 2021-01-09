using FormatWith;
using Humanizer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quartz;
using Regard.Backend.Common.Services;
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
using System.Threading;
using Humanizer.Bytes;

namespace Regard.Backend.Downloader
{
    public class DownloadVideoJob : JobBase
    {
        protected readonly IConfiguration configuration;
        protected readonly IPreferencesManager preferencesManager;
        protected readonly IYoutubeDlService ytdlService;
        protected readonly IVideoDownloaderService videoDownloader;
        protected readonly IVideoStorageService videoStorage;

        private readonly Regex ProgressRegex = new Regex(@"([\d\.]+)% of ~?([\d\.]+)([KMG]i?B)");
        private readonly Regex MergingRegex = new Regex(@"Merging formats into ""([^""]+)""");
        private readonly Regex AlreadyDownloadedRegex = new Regex(@"\[download\] (.*) has already been downloaded");
        private readonly Regex DestinationRegex = new Regex(@"Destination: (.*)");

        bool shouldRetry = false;
        private string outputPath = null;
        private Video video = null;
        private AsyncLock videoMutex = new AsyncLock();
        private CancellationTokenSource cancellationTokenSrc = new CancellationTokenSource();
        private bool limitsChecked = false;

        protected override int RetryCount => (shouldRetry ? 3 : 0);

        protected override TimeSpan RetryInterval => TimeSpan.FromMinutes(15);

        public int VideoId { get; set; }


        public DownloadVideoJob(ILogger<DownloadVideoJob> logger,
                                DataContext dataContext, 
                                IConfiguration configuration,
                                IPreferencesManager preferencesManager,
                                IYoutubeDlService ytdlService,
                                IVideoDownloaderService videoDownloader,
                                IVideoStorageService videoStorage) : base(logger, dataContext)
        {
            this.configuration = configuration;
            this.preferencesManager = preferencesManager;
            this.ytdlService = ytdlService;
            this.videoDownloader = videoDownloader;
            this.videoStorage = videoStorage;
        }

        protected override async Task ExecuteJob(IJobExecutionContext context)
        {
            VideoId = context.MergedJobDataMap.GetInt("VideoId");
            shouldRetry = false;

            video = dataContext.Videos.Find(VideoId);
            if (video == null)
                throw new ArgumentException($"Download failed - invalid video id {VideoId}.");

            if (video.DownloadedPath != null)
                throw new ArgumentException($"Download failed - video {VideoId} is already downloaded!");

            var opts = ResolveDownloadOptions(video).ToArray();
            shouldRetry = true;

            log.LogInformation("Running youtube-dl with arguments: {0}", string.Join(" ", opts));

            try
            {
                await ytdlService.UsingYoutubeDL(async ytdl =>
                {
                    int resultCode = ytdl.Run(opts,
                        ProcessStdout,
                        ProcessStderr,
                        timeoutMs: 24 * 3600 * 1000,
                        cancellationToken: cancellationTokenSrc.Token);

                    if (resultCode != 0)
                        throw new Exception($"videoId={VideoId}: Download failed!\n");
                });
            }
            catch (OperationCanceledException)
            {
                shouldRetry = false;
                log.LogInformation("Video download was canceled!");
                throw;
            }
            finally
            {
                videoDownloader.OnDownloadFinished(VideoId);
            }

            using (var @lock = await videoMutex.LockAsync())
            {
                video.DownloadedPath = outputPath;
                video.DownloadedSize = await videoStorage.CalculateSize(video);
                await dataContext.SaveChangesAsync();
            }
            
            log.LogInformation($"videoId={VideoId}: Download completed!");
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
                await UpdateOutputPath(Path.ChangeExtension(match.Groups[1].Value, null));
            }
            else if (ProgressRegex.TryMatch(message, out match))
            {
                if (float.TryParse(match.Groups[1].Value, out float percent))
                    videoDownloader.OnVideoDownloading(VideoId, percent / 100f);

                if (!limitsChecked && double.TryParse(match.Groups[2].Value, out double size))
                    ProcessFileSize(size, match.Groups[3].Value);
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
        }

        private IEnumerable<string> ResolveDownloadOptions(Video video)
        {
            yield return "--no-color";

            // TODO: Network Options
            // TODO: Geo Restriction

            #region Download Options

            string limitRate = preferencesManager.GetForSubscription(Preferences.Ytdl_LimitRate, video.SubscriptionId);
            if (limitRate != null)
            {
                yield return "-r";
                yield return limitRate;
            }

            string retries = preferencesManager.GetForSubscription(Preferences.Ytdl_Retries, video.SubscriptionId); 
            if (retries != null)
            {
                yield return "-R";
                yield return retries;
            }

            #endregion

            #region Filesystem Options

            if (preferencesManager.GetForSubscription(Preferences.Ytdl_WriteDescription, video.SubscriptionId))
                yield return "--write-description";

            if (preferencesManager.GetForSubscription(Preferences.Ytdl_WriteInfoJson, video.SubscriptionId))
                yield return "--write-info-json";

            #endregion

            #region Thumbnail images

            if (preferencesManager.GetForSubscription(Preferences.Ytdl_WriteThumbnail, video.SubscriptionId))
                yield return "--write-thumbnail";

            #endregion

            #region Verbosity / Simulation Options

            yield return "--newline";

            bool? callHome = preferencesManager.GetGlobal(Preferences.Ytdl_CallHome);
            if (callHome.HasValue)
                yield return (callHome.Value) ? "-C" : "--no-call-home";

            #endregion

            // TODO: workarounds

            #region Video Format Options

            string format = preferencesManager.GetForSubscription(Preferences.Ytdl_Format, video.SubscriptionId);
            if (format != null)
            {
                yield return "-f";
                yield return format;
            }

            if (preferencesManager.GetForSubscription(Preferences.Ytdl_AllFormats, video.SubscriptionId))
                yield return "--all-formats";

            if (preferencesManager.GetForSubscription(Preferences.Ytdl_PreferFreeFormats, video.SubscriptionId))
                yield return "--prefer-free-formats";

            string mergeOutputFormat = preferencesManager.GetForSubscription(Preferences.Ytdl_MergeOutputFormat, video.SubscriptionId);
            if (mergeOutputFormat != null)
            {
                yield return "--merge-output-format";
                yield return mergeOutputFormat;
            }

            #endregion

            #region Subtitle Options

            if (preferencesManager.GetForSubscription(Preferences.Ytdl_WriteSubtitles, video.SubscriptionId))
                yield return "--write-sub";

            if (preferencesManager.GetForSubscription(Preferences.Ytdl_WriteAutoSub, video.SubscriptionId))
                yield return "--write-auto-sub";

            if (preferencesManager.GetForSubscription(Preferences.Ytdl_AllSubs, video.SubscriptionId))
                yield return "--all-subs";

            string subFormat = preferencesManager.GetForSubscription(Preferences.Ytdl_SubFormat, video.SubscriptionId);
            if (subFormat != null)
            {
                yield return "--sub-format";
                yield return subFormat;
            }

            string subLang = preferencesManager.GetForSubscription(Preferences.Ytdl_SubLang, video.SubscriptionId);
            if (subLang != null)
            {
                yield return "--sub-lang";
                yield return subLang;
            }

            #endregion

            // TODO: maybe add more options?
            yield return "-o";
            outputPath = ResolveOutputPath(video);
            yield return outputPath;

            yield return video.OriginalUrl;
        }

        private string ResolveOutputPath(Video video)
        {
            var sub = dataContext.Subscriptions.Find(video.SubscriptionId);
            string format = preferencesManager.GetForSubscription(Preferences.Subscriptions_DownloadPath, video.SubscriptionId);
            string path = format.FormatWith(new
            {
                DataDirectory = configuration["DataDirectory"],
                DownloadDirectory = configuration["DownloadDirectory"],
                Video = video,
                Subscription = sub,
                FolderPath = GetFolderPath(sub),
                Env = Environment.GetEnvironmentVariables(),
            }, MissingKeyBehaviour.ThrowException);

            // Normalize path
            path = path.Replace('\\', Path.DirectorySeparatorChar);
            path = path.Replace('/', Path.DirectorySeparatorChar);
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
