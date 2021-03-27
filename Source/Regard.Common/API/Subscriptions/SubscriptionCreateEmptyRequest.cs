using Regard.Common.API.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Regard.Common.API.Subscriptions
{
    public class SubscriptionCreateEmptyRequest
    {
        [Required(AllowEmptyStrings = false, ErrorMessage = "Name is required!")]
        public string Name { get; set; }
        
        public int? ParentFolderId { get; set; }
    }
}
