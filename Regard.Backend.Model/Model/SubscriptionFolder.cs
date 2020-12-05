using RegardBackend.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Model
{
    public class SubscriptionFolder
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [NotNull, MaxLength(64)]
        public string Name { get; set; }

        public string UserId { get; set; }

        public UserAccount User { get; set; }

        public int? ParentId { get; set; }

        public SubscriptionFolder Parent { get; set; }


        // Setting overrides

        public bool? AutoDownload { get; set; }

        public int? DownloadMaxCount { get; set; }

        public VideoOrder? DownloadOrder { get; set; }

        public bool? AutomaticDeleteWatched { get; set; }

        [MaxLength(260)]
        public string DownloadPath { get; set; }
    }
}
