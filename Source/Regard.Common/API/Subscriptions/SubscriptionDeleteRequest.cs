using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.API.Subscriptions
{
    public class SubscriptionDeleteRequest
    {
        public int[] Ids { get; set; }

        public bool DeleteDownloadedFiles { get; set; }
    }
}
