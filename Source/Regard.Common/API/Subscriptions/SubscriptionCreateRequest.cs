using Regard.Common.API.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Regard.Common.API.Subscriptions
{
    public class SubscriptionCreateRequest
    {
        [Required(AllowEmptyStrings = false, ErrorMessage = "URL is required!")]
        public string Url { get; set; }
        
        public int? ParentFolderId { get; set; }
    }
}
