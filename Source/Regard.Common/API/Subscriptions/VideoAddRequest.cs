using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Regard.Common.API.Subscriptions
{
    public class VideoAddRequest
    {
        [Required(AllowEmptyStrings = false, ErrorMessage = "URL is required!")]

        public string Url { get; set; }

        public int SubscriptionId { get; set; }
    }
}
