using Regard.Backend.Model;
using Regard.Common.API.Model;
using Regard.Model;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Regard.Backend.Common.Utils
{
    public static class ModelHelpers
    {
        public static ApiSubscription ToApi(this Subscription @this)
        {
            return new ApiSubscription()
            {
                Id = @this.Id,
                Name = @this.Name,
                Description = @this.Description,
                ParentFolderId = @this.ParentFolderId,
                ThumbnailUrl = @this.ThumbnailPath
            };
        }

        public static ApiSubscriptionFolder ToApi(this SubscriptionFolder @this)
        {
            return new ApiSubscriptionFolder()
            {
                Id = @this.Id,
                Name = @this.Name,
                ParentId = @this.ParentId
            };
        }

        public static ApiVideo ToApi(this Video @this)
        {
            return new ApiVideo()
            {
                Id = @this.Id,
                Name = @this.Name,
                Description = @this.Description,
                IsWatched = @this.IsWatched,
                IsNew = (DateTime.Now - @this.Published).TotalDays < 7,
                IsDownloaded = (@this.DownloadedPath != null),
                DownloadedSize = @this.DownloadedSize,
                SubscriptionId = @this.SubscriptionId,
                PlaylistIndex = @this.PlaylistIndex,
                Published = @this.Published,
                LastUpdated = @this.LastUpdated,
                ThumbnailUrl = @this.ThumbnailPath, // TODO
                UploaderName = @this.UploaderName,
                Views = @this.Views,
                Rating = @this.Rating
            };
        }

        public static IQueryable<Video> OrderBy(this IQueryable<Video> @this, VideoOrder? videoOrder)
        {
            if (!videoOrder.HasValue)
                return @this;

            return videoOrder.Value switch
            {
                VideoOrder.Newest => @this.OrderByDescending(x => x.Published),
                VideoOrder.Oldest => @this.OrderBy(x => x.Published),
                VideoOrder.Playlist => @this.OrderBy(x => x.PlaylistIndex),
                VideoOrder.ReversePlaylist => @this.OrderByDescending(x => x.PlaylistIndex),
                VideoOrder.Popularity => @this.OrderByDescending(x => x.Views),
                VideoOrder.Rating => @this.OrderByDescending(x => x.Rating),
                VideoOrder.Name => @this.OrderBy(x => x.Name),
                _ => throw new NotImplementedException(),
            };
        }

        public static int? GetPropertyMaxLength(this object @object, string propertyName)
        {
            var prop = @object.GetType().GetProperty(propertyName);
            var attr = Attribute.GetCustomAttribute(prop, typeof(MaxLengthAttribute));
            if (attr != null && attr is MaxLengthAttribute maxLengthAttr)
                return maxLengthAttr.Length;

            return null;
        }
    }
}
