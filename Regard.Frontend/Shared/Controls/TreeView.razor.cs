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
        private TreeViewNode<Model> selectedItem = null;

        public virtual TreeViewNode<Model> Root { get; } = new TreeViewNode<Model>();

        public TreeViewNode<Model> SelectedItem 
        {
            get => selectedItem;
            set
            {
                if (selectedItem != value)
                {
                    if (selectedItem != null)
                        selectedItem.IsSelected = false;

                    selectedItem = value;

                    if (selectedItem != null)
                        selectedItem.IsSelected = true;

                    SelectedItemChanged.InvokeAsync(selectedItem);
                }
            }
        }

        [Parameter]
        public RenderFragment<TreeViewNode<Model>> ItemTemplate { get; set; }

        [Parameter]
        public EventCallback<TreeViewNode<Model>> ItemClicked { get; set; }

        [Parameter]
        public EventCallback<TreeViewNode<Model>> SelectedItemChanged { get; set; }

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

        private async Task OnItemClicked(TreeViewNode<Model> item)
        {
            await ItemClicked.InvokeAsync(item);
            SelectedItem = item;
        }
    }
}
