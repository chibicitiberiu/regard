using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.API.Request
{
    public class SubscriptionCreate
    {
        public string Url { get; set; }
        
        public int? ParentFolderId { get; set; }
    }
}
