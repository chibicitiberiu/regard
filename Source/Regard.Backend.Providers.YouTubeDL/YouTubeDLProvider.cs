using Microsoft.Extensions.Logging;
using MoreLinq;
using Regard.Backend.Common.Providers;
using Regard.Backend.Common.Services;
using Regard.Backend.Common.Utils;
using Regard.Backend.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YoutubeDLWrapper;

namespace Regard.Backend.Providers.YouTubeDL
{
    public class YouTubeDLProvider : ISubscriptionProvider, IVideoProvider
    {
        private readonly ILogger logger;
        private readonly IYoutubeDlService ytdlService;

        // Interactive extraction (the Add-subscription flow has a user waiting) fails fast; background
        // extraction (sync) can afford to wait. Both retry a couple of times on transient failures.
        private const int InteractiveTimeoutMs = 45 * 1000;
        private const int InteractiveRetries = 1;
        private const int BackgroundTimeoutMs = 5 * 60 * 1000;
        private const int BackgroundRetries = 2;

        public string Id => "YtDL";

        public string Name => "YouTubeDL";

        public bool IsInitialized => true;

        public Type ConfigurationType => null;

        public YouTubeDLProvider(ILogger<YouTubeDLProvider> logger, IYoutubeDlService ytdlService)
        {
            this.logger = logger;
            this.ytdlService = ytdlService;
        }

        public bool CanHandleSubscriptionUrlHint(Uri uri)
        {
            // yt-dlp supports many sites, but for YouTube hosts it's unambiguously the
            // right provider, so claim them up front and let the dispatcher probe us
            // before generic providers (RSS) that would otherwise fetch-and-fail.
            var host = uri.Host;
            return host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase)
                || host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<bool> CanHandleSubscriptionUrl(Uri uri)
        {
            try
            {
                uri = YouTubeUrlHelper.FixYouTubeChannelUri(uri);

                await ytdlService.PaceExtractionAsync(UrlHostKey.Of(uri.ToString()));
                var info = await ytdlService.UsingYoutubeDL(async ytdl =>
                    await ytdl.ExtractInformation(uri.ToString(), false, InteractiveTimeoutMs, retries: InteractiveRetries, extraArgs: ytdlService.GetAntibotArgs()));

                return info.Type == YoutubeDLWrapper.UrlType.Playlist
                    || info.Type == YoutubeDLWrapper.UrlType.MultiVideo;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, $"Cannot handle subscription URL {uri}");
                return false;
            }
        }

        public async Task<bool> CanHandleVideo(Video video)
        {
            try
            {
                await ytdlService.PaceExtractionAsync(UrlHostKey.Of(video.OriginalUrl));
                var info = await ytdlService.UsingYoutubeDL(async ytdl =>
                    await ytdl.ExtractInformation(video.OriginalUrl, false, InteractiveTimeoutMs, retries: InteractiveRetries, extraArgs: ytdlService.GetAntibotArgs()));

                return info.Type == YoutubeDLWrapper.UrlType.Video;

            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, $"Cannot handle video {video} {video.OriginalUrl}");
                return false;
            }
        }

        public Task Configure(object config)
        {
            // NOOP
            return Task.CompletedTask;
        }

        public async Task<Subscription> CreateSubscription(Uri uri)
        {
            // Fixup youtube channel url's (get "uploads" playlist)
            uri = YouTubeUrlHelper.FixYouTubeChannelUri(uri);

            // A user is waiting on the Add-subscription modal, so use the short interactive timeout.
            await ytdlService.PaceExtractionAsync(UrlHostKey.Of(uri.ToString()));
            var info = await ytdlService.UsingYoutubeDL(async ytdl =>
                await ytdl.ExtractInformation(uri.ToString(), false, InteractiveTimeoutMs, retries: InteractiveRetries, extraArgs: ytdlService.GetAntibotArgs()));

            if (info.Type != YoutubeDLWrapper.UrlType.Playlist && info.Type != YoutubeDLWrapper.UrlType.MultiVideo)
            {
                logger.LogDebug($"Subscription type for {uri}: {info.Type}");
                throw new Exception("Invalid or unsupported URL format!");
            }

            // Fetch thumbnail, real channel title. Best-effort and time-boxed: this is a plain page
            // fetch that YouTube may answer slowly or with a consent interstitial, and everything it
            // supplies has a yt-dlp fallback below, so it must never hold up subscription creation.
            IReadOnlyList<KeyValuePair<string, string>> metadata = Array.Empty<KeyValuePair<string, string>>();
            if (uri.Host.EndsWith("youtube.com"))
                metadata = await MetadataScraper.ScrapeMetadataAsync(uri, diagnostic: m => logger.LogInformation(m));

            return new Subscription()
            {
                SubscriptionId = info.Id,
                SubscriptionProviderId = Id,
                Name = GetFirst(metadata, "name", "og:title", "twitter:title") ?? info.Title,
                Description = info.Description,
                ThumbnailPath = GetFirst(metadata, "link:thumbnailUrl", "link:url", "og:image", "twitter:image") ?? info.Thumbnail?.ToString(),
                OriginalUrl = uri.ToString()
            };
        }

        private static string GetFirst(IEnumerable<KeyValuePair<string, string>> items, params string[] keys)
        {
            foreach (var key in keys)
            {
                var search = items.FirstOrDefault(x => x.Key == key && x.Value != null);
                if (search.Key != null)
                    return search.Value;
            }
            return null;
        }

        public async IAsyncEnumerable<Video> FetchVideos(Subscription subscription)
        {
            // Flat listing (fetchVideos: false adds --flat-playlist): returns the channel's entries
            // quickly, newest-first, without a full per-video extraction. Full metadata (duration,
            // description, published date, rating) is filled in later by the sync job — eagerly for the
            // newest few, lazily for the rest. Background timeout + retries.
            await ytdlService.PaceExtractionAsync(UrlHostKey.Of(subscription.OriginalUrl));
            UrlInformation info = await ytdlService.UsingYoutubeDL(async ytdl =>
                await ytdl.ExtractInformation(subscription.OriginalUrl, false, BackgroundTimeoutMs, retries: BackgroundRetries, extraArgs: ytdlService.GetAntibotArgs()));

            if (info == null)
                throw new Exception("Failed to fetch videos (timed out)!");

            Queue<UrlInformation> queue = new Queue<UrlInformation>();
            if (info.Entries != null)
                info.Entries.Where(e => e != null).ForEach(queue.Enqueue);

            int index = 0;
            while (queue.Count > 0)
            {
                var entry = queue.Dequeue();
                switch (entry.Type)
                {
                    case UrlType.Playlist:
                    case UrlType.MultiVideo:
                        if (entry.Entries != null)
                            entry.Entries.Where(e => e != null).ForEach(queue.Enqueue);
                        break;

                    // Flat-playlist entries come back as "url"/"url_transparent" (a reference to be
                    // resolved later), not "video" — handle all three the same way.
                    case UrlType.Video:
                    case UrlType.Url:
                    case UrlType.UrlTransparent:
                        yield return new Video()
                        {
                            SubscriptionProviderId = entry.Id,
                            VideoProviderId = Id,
                            VideoId = entry.Id,
                            Name = entry.Title,
                            Description = entry.Description,
                            Subscription = subscription,
                            PlaylistIndex = index++,
                            Published = (entry.Timestamp != DateTime.MinValue) ? entry.Timestamp : DateTimeOffset.Now,
                            LastUpdated = DateTimeOffset.Now,
                            // Flat entries populate Thumbnails[] rather than the single Thumbnail.
                            ThumbnailPath = (entry.Thumbnail ?? entry.Thumbnails?.LastOrDefault()?.Url)?.ToString(),
                            UploaderName = entry.Uploader,
                            // Flat mode gives "url" (the watch URL); non-flat gives "webpage_url".
                            OriginalUrl = (entry.WebpageUrl ?? entry.Url)?.ToString(),
                            Views = entry.ViewCount,
                            Duration = entry.Duration.HasValue ? (int?)Math.Round(entry.Duration.Value) : null,
                            Rating = ProviderHelpers.CalculateRating(entry.LikeCount, entry.DislikeCount)
                        };
                        break;
                }
            }
        }

        public void Unconfigure()
        {
            // NO-OP
        }

        public async Task UpdateMetadata(IEnumerable<Video> videos, bool updateMetadata, bool updateStatistics)
        {
            foreach (var video in videos)
            {
                await ytdlService.PaceExtractionAsync(UrlHostKey.Of(video.OriginalUrl));
                var info = await ytdlService.UsingYoutubeDL(async ytdl =>
                    await ytdl.ExtractInformation(video.OriginalUrl, false, BackgroundTimeoutMs, retries: BackgroundRetries, extraArgs: ytdlService.GetAntibotArgs()));

                if (updateMetadata)
                {
                    video.Name = info.Title;
                    video.Description = info.Description;
                    video.Published = info.Timestamp;
                    video.LastUpdated = DateTimeOffset.Now;
                    video.ThumbnailPath = info.Thumbnail?.ToString();
                    video.UploaderName = info.Uploader;
                    video.Duration = info.Duration.HasValue ? (int?)Math.Round(info.Duration.Value) : null;

                    // Capture chapters (original-timeline). Serialize with the same field names the API
                    // side reads back (Start/End/Title) using System.Text.Json — the wrapper POCO uses
                    // Newtonsoft snake_case names, so we project rather than serialize it directly.
                    video.Chapters = (info.Chapters != null && info.Chapters.Length > 0)
                        ? System.Text.Json.JsonSerializer.Serialize(
                            info.Chapters.Select(c => new { Start = c.StartTime, End = c.EndTime, c.Title }))
                        : null;
                }

                if (updateStatistics)
                {
                    video.Views = info.ViewCount;
                    video.Rating = ProviderHelpers.CalculateRating(info.LikeCount, info.DislikeCount);
                }
            }
        }
    }
}
