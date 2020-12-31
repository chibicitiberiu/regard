using Regard.Common.API.Model;
using Regard.Frontend.Shared.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Subscription
{
    public class SubscriptionFolderViewModel : SubscriptionItemViewModelBase
    {
        public ApiSubscriptionFolder Folder { get; private set; }

        public override string Name => Folder.Name;

        public override int? ParentId => Folder.ParentId;

        public override string ThumbnailUrl => null;

        public override Icons PlaceholderIcon => Icons.Folder;

        public SubscriptionFolderViewModel(ApiSubscriptionFolder folder)
        {
            Folder = folder;
        }
    }
}
