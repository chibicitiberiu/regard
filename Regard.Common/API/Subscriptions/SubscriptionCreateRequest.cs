using Regard.Common.API.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.API.Subscriptions
{
    public class SubscriptionCreateRequest
    {
        public string Url { get; set; }
        
        public ParentId ParentFolderId { get; set; }
    }
}
