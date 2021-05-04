using Regard.Backend.Model;
using Regard.Backend.Thumbnails;
using Regard.Common.API.Model;
using System;

namespace Regard.Backend.Services
{
    public class ApiModelFactory
    {
        protected readonly ThumbnailService thumbnailService;

        public ApiModelFactory(ThumbnailService thumbnailService)
        {
            this.thumbnailService = thumbnailService;
        }

        public ApiSubscription ToApi(Subscription sub)
        {
            return new ApiSubscription()
            {
                Id = sub.Id,
                Name = sub.Name,
                Description = sub.Description,
                ParentFolderId = sub.ParentFolderId,
                ThumbnailUrl = thumbnailService.GetThumbnail(sub)
            };
        }

        public ApiSubscriptionFolder ToApi(SubscriptionFolder folder)
        {
            return new ApiSubscriptionFolder()
            {
                Id = folder.Id,
                Name = folder.Name,
                ParentId = folder.ParentId
            };
        }

        public ApiVideo ToApi(Video video)
        {
            return new ApiVideo()
            {
                Id = video.Id,
                Name = video.Name,
                Description = video.Description,
                IsWatched = video.IsWatched,
                IsNew = (DateTime.Now - video.Published).TotalDays < 7,
                IsDownloaded = (video.DownloadedPath != null),
                DownloadedSize = video.DownloadedSize,
                SubscriptionId = video.SubscriptionId,
                PlaylistIndex = video.PlaylistIndex,
                Published = video.Published,
                LastUpdated = video.LastUpdated,
                ThumbnailUrl = thumbnailService.GetThumbnail(video),
                UploaderName = video.UploaderName,
                Views = video.Views,
                Rating = video.Rating
            };
        }
    }
}
