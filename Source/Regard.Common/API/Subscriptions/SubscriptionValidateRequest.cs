using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.API.Subscriptions
{
    public class SubscriptionValidateRequest
    {
        public string Url { get; set; }

        public string Name { get; set; }

        public int? ParentFolderId { get; set; }
    }
}
