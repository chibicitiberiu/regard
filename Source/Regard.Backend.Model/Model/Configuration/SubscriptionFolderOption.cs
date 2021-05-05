using Regard.Backend.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Regard.Backend.Model
{
    public class SubscriptionFolderOption : IOption
    {
        public string Key { get; set; }

        public string Value { get; set; }

        [Required]
        public int SubscriptionFolderId { get; set; }

        public SubscriptionFolder SubscriptionFolder { get; set; }
    }
}
