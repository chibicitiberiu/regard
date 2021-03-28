using Microsoft.Extensions.Logging;
using MoreLinq;
using Regard.Backend.Common.Providers;
using Regard.Backend.Common.Services;
using Regard.Backend.Common.Utils;
using Regard.Backend.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YoutubeDLWrapper;

namespace Regard.Backend.Providers.YouTubeDL
{
    public class YouTubeDLProvider : ISubscriptionProvider, IVideoProvider
    {
        private readonly ILogger logger;
        private readonly IYoutubeDlService ytdlService;

        public string Id => "YtDL";

        public string Name => "YouTubeDL";

        public bool IsInitialized => true;

        public Type ConfigurationType => null;

        public YouTubeDLProvider(ILogger<YouTubeDLProvider> logger, IYoutubeDlService ytdlService)
        {
            this.logger = logger;
            this.ytdlService = ytdlService;
        }

        public async Task<bool> CanHandleSubscriptionUrl(Uri uri)
        {
            try
            {
                var info = await ytdlService.UsingYoutubeDL(async ytdl =>
                    await ytdl.ExtractInformation(uri.ToString(), false));

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
                var info = await ytdlService.UsingYoutubeDL(async ytdl =>
                    await ytdl.ExtractInformation(video.OriginalUrl, false));

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

            // Running "ExtractInformation" here might be quite slow
            var info = await ytdlService.UsingYoutubeDL(async ytdl => 
                await ytdl.ExtractInformation(uri.ToString(), false));

            if (info.Type != YoutubeDLWrapper.UrlType.Playlist && info.Type != YoutubeDLWrapper.UrlType.MultiVideo)
            {
                logger.LogDebug($"Subscription type for {uri}: {info.Type}");
                throw new Exception("Invalid or unsupported URL format!");
            }

            return new Subscription()
            {
                SubscriptionId = info.Id,
                SubscriptionProviderId = Id,
                Name = info.Title,
                Description = info.Description,
                ThumbnailPath = info.Thumbnail?.ToString(),
                OriginalUrl = uri.ToString()
            };
        }

        public async IAsyncEnumerable<Video> FetchVideos(Subscription subscription)
        {
            var info = await ytdlService.UsingYoutubeDL(async ytdl => 
                await ytdl.ExtractInformation(subscription.OriginalUrl, true));

            Queue<UrlInformation> queue = new Queue<UrlInformation>();
            if (info.Entries != null)
                info.Entries.ForEach(queue.Enqueue);

            int index = 0;
            while (queue.Count > 0)
            {
                var entry = queue.Dequeue();
                switch (entry.Type)
                {
                    case UrlType.Playlist:
                    case UrlType.MultiVideo:
                        if (entry.Entries != null)
                            entry.Entries.ForEach(queue.Enqueue);
                        break;

                    case UrlType.Video:
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
                            ThumbnailPath = entry.Thumbnail?.ToString(),
                            UploaderName = entry.Uploader,
                            OriginalUrl = entry.WebpageUrl?.ToString(),
                            Views = entry.ViewCount,
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
                var info = await ytdlService.UsingYoutubeDL(async ytdl => 
                    await ytdl.ExtractInformation(video.OriginalUrl, false));

                if (updateMetadata)
                {
                    video.Name = info.Title;
                    video.Description = info.Description;
                    video.Published = info.Timestamp;
                    video.LastUpdated = DateTimeOffset.Now;
                    video.ThumbnailPath = info.Thumbnail.ToString();
                    video.UploaderName = info.Uploader;
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
