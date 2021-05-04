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
