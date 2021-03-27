using Regard.Common.API.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.API.Subscriptions
{
    public class SubscriptionFolderListRequest
    {
        public int[] Ids { get; set; }

        public ParentId[] ParentIds { get; set; }

        public ApiSubscriptionFolder.Parts Parts { get; set; }
    }
}
