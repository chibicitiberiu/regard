using Regard.Common.API.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.API.Subscriptions
{
    public class SubscriptionListRequest
    {
        public int[] Ids { get; set; }

        public ParentId[] ParentFolderIds { get; set; }

        public ApiSubscription.Parts Parts { get; set; }
    }
}
