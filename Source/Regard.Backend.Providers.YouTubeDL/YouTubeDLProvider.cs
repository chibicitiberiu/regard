using Regard.Backend.Common.Providers;
using Regard.Backend.Common.Services;
using Regard.Backend.Common.Utils;
using Regard.Backend.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Regard.Backend.Providers.YouTubeDL
{
    public class YouTubeDLProvider : ISubscriptionProvider, IVideoProvider
    {
        private readonly IYoutubeDlService ytdlService;

        public string Id => "YtDL";

        public string Name => "YouTubeDL";

        public bool IsInitialized => true;

        public Type ConfigurationType => null;

        public YouTubeDLProvider(IYoutubeDlService ytdlService)
        {
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
            catch (Exception)
            {
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
            // Running "ExtractInformation" here might be quite slow
            var info = await ytdlService.UsingYoutubeDL(async ytdl => 
                await ytdl.ExtractInformation(uri.ToString(), false));

            if (info.Type != YoutubeDLWrapper.UrlType.Playlist && info.Type != YoutubeDLWrapper.UrlType.MultiVideo)
                throw new Exception("Invalid or unsupported URL format!");

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

            int index = 0;
            foreach (var video in info.Entries)
            {
                yield return new Video()
                {
                    SubscriptionProviderId = video.Id,
                    VideoProviderId = Id,
                    VideoId = video.Id,
                    Name = video.Title,
                    Description = video.Description,
                    Subscription = subscription,
                    PlaylistIndex = index++,
                    Published = video.Timestamp,
                    LastUpdated = DateTimeOffset.Now,
                    ThumbnailPath = video.Thumbnail.ToString(),
                    UploaderName = video.Uploader,
                    OriginalUrl = video.WebpageUrl.ToString(),
                    Views = video.ViewCount,
                    Rating = ProviderHelpers.CalculateRating(video.LikeCount, video.DislikeCount)
                };
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
