using Microsoft.AspNetCore.Components;
using Regard.Common.Utils;
using Regard.Frontend.Shared.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Subscription
{
    public abstract class SubscriptionItemViewModelBase : NotifyPropertyChangedBase
    {
        private bool isContextMenuVisible = false;

        public abstract string Name { get; }

        public abstract int? ParentId { get; }

        public abstract string ThumbnailUrl { get; }

        public abstract Icons PlaceholderIcon { get; }

        public bool IsContextMenuVisible 
        { 
            get => isContextMenuVisible;
            set => SetField(ref isContextMenuVisible, value);
        }

        public ElementReference ContextMenuLinkReference { get; set; }
    }
}
