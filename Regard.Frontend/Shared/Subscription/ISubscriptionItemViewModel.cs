using Regard.Frontend.Shared.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Subscription
{
    public interface ISubscriptionItemViewModel
    {
        string Name { get; }

        int? ParentId { get; }

        string ThumbnailUrl { get; }

        Icons PlaceholderIcon { get; }
    }
}
