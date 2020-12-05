using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Shared.Controls
{
    public partial class TreeView<TData>
    {
        public TreeViewNode<TData> Root { get; } = new TreeViewNode<TData>();

        public RenderFragment<TData> ItemTemplate { get; set; }
    }
}
