using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.API.Subscriptions
{
    public class SubscriptionFolderDeleteRequest
    {
        public int[] Ids { get; set; }

        public bool KeepSubscriptions { get; set; } = false;
    }
}
