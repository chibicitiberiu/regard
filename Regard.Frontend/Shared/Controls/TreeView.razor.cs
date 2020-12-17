using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Controls
{
    public partial class TreeView<Model>
    {
        public TreeViewNode<Model> Root { get; } = new TreeViewNode<Model>();

        [Parameter]
        public RenderFragment<TreeViewNode<Model>> ItemTemplate { get; set; }

        public TreeView()
        {
            Root.ChildPropertyChanged += OnTreePropertyChanged;
            Root.TreeChanged += OnTreeChanged;
        }

        private void OnTreeChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            StateHasChanged();
        }

        private void OnTreePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            StateHasChanged();
        }
    }
}
