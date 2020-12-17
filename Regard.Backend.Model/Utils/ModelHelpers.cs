using Regard.Backend.Model;
using Regard.Common.API.Model;

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
    }
}
