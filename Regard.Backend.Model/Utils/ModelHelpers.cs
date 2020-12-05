using Regard.Backend.Model;
using Regard.Common.API.Response;
using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Backend.Common.Utils
{
    public static class ModelHelpers
    {
        public static SubscriptionResponse ToResponse(this Subscription @this)
        {
            return new SubscriptionResponse()
            {
                Id = @this.Id,
                Name = @this.Name,
                Description = @this.Description,
                ParentFolderId = @this.ParentFolderId,
                ThumbnailUrl = @this.ThumbnailPath
            };
        }
    }
}
