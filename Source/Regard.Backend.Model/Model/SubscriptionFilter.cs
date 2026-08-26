using Regard.Model;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Regard.Backend.Model
{
    public class SubscriptionFilter
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int SubscriptionId { get; set; }

        public virtual Subscription Subscription { get; set; }

        public FilterAction Action { get; set; }

        [MaxLength(1000)]
        public string Pattern { get; set; }
    }
}
