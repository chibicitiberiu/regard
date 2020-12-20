using Regard.Common.API.Model;
using Regard.Frontend.Shared.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Subscription
{
    public class SubscriptionFolderViewModel : ISubscriptionItemViewModel
    {
        public ApiSubscriptionFolder Folder { get; private set; }

        public string Name => Folder.Name;

        public int? ParentId => Folder.ParentId;

        public string ThumbnailUrl => null;

        public Icons PlaceholderIcon => Icons.Folder;

        public SubscriptionFolderViewModel(ApiSubscriptionFolder folder)
        {
            Folder = folder;
        }
    }
}
