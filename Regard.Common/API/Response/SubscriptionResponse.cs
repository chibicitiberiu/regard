using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.API.Response
{
    public class SubscriptionResponse
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public int? ParentFolderId { get; set; }

        public string ThumbnailUrl { get; set; }
    }
}
