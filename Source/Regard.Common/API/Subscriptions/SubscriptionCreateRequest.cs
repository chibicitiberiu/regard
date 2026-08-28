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

        /// <summary>
        /// Whether to automatically download this subscription's latest videos. Defaults to true
        /// (the global default); set false in the Add dialog to add a subscription without it
        /// immediately pulling videos. Written as a per-subscription override when false.
        /// </summary>
        public bool AutoDownload { get; set; } = true;

        /// <summary>
        /// When false, creating a subscription that resolves to a channel/playlist the user is
        /// already subscribed to is rejected with a 409 so the UI can warn. Set true to proceed
        /// anyway (a legitimate case: two subscriptions to the same source with different filters).
        /// </summary>
        public bool AllowDuplicate { get; set; }
    }
}
