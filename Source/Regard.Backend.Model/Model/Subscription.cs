using Regard.Model;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Regard.Backend.Model
{
    public class Subscription
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [MaxLength(2048)]
        public string OriginalUrl { get; set; }

        /// <summary>
        /// Provider ID
        /// </summary>
        [MaxLength(60)]
        public string SubscriptionProviderId { get; set; }

        /// <summary>
        /// Subscription ID, as defined by the provider
        /// </summary>
        [MaxLength(2048)]
        public string SubscriptionId { get; set; }

        [Required, MaxLength(250)]
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

        public override string ToString()
        {
            return $"({Id}:{Name})";
        }
    }
}
