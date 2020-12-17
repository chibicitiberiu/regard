using Regard.Common.API.Model;
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

        public SubscriptionFolderViewModel(ApiSubscriptionFolder folder)
        {
            Folder = folder;
        }
    }
}
