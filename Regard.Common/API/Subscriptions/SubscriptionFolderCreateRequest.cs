using Regard.Common.API.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.API.Subscriptions
{
    public class SubscriptionFolderCreateRequest
    {
        public string Name { get; set; }

        public ParentId ParentId { get; set; }
    }
}
