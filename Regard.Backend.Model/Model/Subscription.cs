using Regard.Model;
using Regard.Backend.Model;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Regard.Backend.Model
{
    public class Subscription
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Provider ID
        /// </summary>
        [NotNull, MaxLength(60)]
        public string SubscriptionProviderId { get; set; }

        /// <summary>
        /// Subscription ID, as defined by the provider
        /// </summary>
        [NotNull, MaxLength(2048)]
        public string SubscriptionId { get; set; }

        [NotNull, MaxLength(250)]
        public string Name { get; set; }

        [MaxLength(2048)]
        public string Description { get; set; }

        public int? ParentFolderId { get; set; }

        public virtual SubscriptionFolder ParentFolder { get; set; }

        [MaxLength(2048)]
        public string ThumbnailPath { get; set; }

        public string UserId { get; set; }

        public virtual UserAccount User { get; set; }
        
        public string ProviderData { get; set; }


        // Setting overrides
        
        public bool? AutoDownload { get; set; }
        
        public int? DownloadMaxCount { get; set; }

        public int? DownloadMaxSize { get; set; }

        public VideoOrder? DownloadOrder { get; set; }
        
        public bool? AutomaticDeleteWatched { get; set; }

        [MaxLength(260)]
        public string DownloadPath { get; set; }

        public override string ToString()
        {
            return $"({Id}:{Name})";
        }
    }
}
