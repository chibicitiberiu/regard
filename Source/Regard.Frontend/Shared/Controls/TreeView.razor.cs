using Microsoft.AspNetCore.Components;
using Regard.Common.Utils;
using Regard.Common.Utils.Collections;
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

        private TreeViewNode<Model> draggedItem = null;
        private TreeViewNode<Model> dragOverItem = null;
        private bool dragOverRoot = false;

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

                    StateHasChanged();
                }
            }
        }

        [Parameter]
        public RenderFragment<TreeViewNode<Model>> ItemTemplate { get; set; }

        /// <summary>Optional content rendered inside the tree, above the rows (e.g. a pinned "Home" row).</summary>
        [Parameter]
        public RenderFragment HeaderContent { get; set; }

        [Parameter]
        public EventCallback<TreeViewNode<Model>> ItemClicked { get; set; }

        [Parameter]
        public EventCallback<TreeViewNode<Model>> SelectedItemChanged { get; set; }

        /// <summary>Enables HTML5 drag-and-drop of rows. Off by default so other tree uses are unaffected.</summary>
        [Parameter]
        public bool EnableDragDrop { get; set; } = false;

        /// <summary>
        /// Validity check for a drop, given (dragged, target). Target is null for the tree root. Controls
        /// both the drop-target highlight and whether <see cref="ItemDropped"/> fires. Defaults to allow.
        /// </summary>
        [Parameter]
        public Func<TreeViewNode<Model>, TreeViewNode<Model>, bool> CanDrop { get; set; }

        /// <summary>Raised on a valid drop with (dragged, target); target is null for the tree root.</summary>
        [Parameter]
        public EventCallback<(TreeViewNode<Model> Dragged, TreeViewNode<Model> Target)> ItemDropped { get; set; }

        /// <summary>Label shown on the root drop zone that appears while dragging.</summary>
        [Parameter]
        public string RootDropLabel { get; set; } = "Move to top level";

        /// <summary>Only show the root drop zone when dropping the dragged item at root is a valid move.</summary>
        private bool ShowRootDropZone()
            => draggedItem != null && (CanDrop == null || CanDrop(draggedItem, null));

        public TreeView()
        {
            Root.ChildPropertyChanged += OnTreePropertyChanged;
            Root.TreeChanged += OnTreeChanged;
        }

        private void OnTreeChanged(object sender, CollectionChangedEventArgs e)
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

        private bool IsDropAllowed(TreeViewNode<Model> target)
            => draggedItem != null && draggedItem != target && (CanDrop == null || CanDrop(draggedItem, target));

        private void OnDragStart(TreeViewNode<Model> item)
        {
            if (!EnableDragDrop)
                return;
            draggedItem = item;
            StateHasChanged();   // reveal the root drop zone
        }

        private void OnDragEnd()
        {
            // Fires whether or not the drop landed on a target; clear all drag state so the root drop
            // zone hides and no stale highlight lingers.
            if (draggedItem == null && dragOverItem == null && !dragOverRoot)
                return;
            draggedItem = null;
            dragOverItem = null;
            dragOverRoot = false;
            StateHasChanged();
        }

        // Highlight is driven by dragover (fires continuously over whatever row is under the cursor)
        // instead of dragenter/dragleave, which flicker as the pointer crosses child elements and make
        // the drop target feel like a tiny rectangle. We only re-render when the highlighted row changes.
        private void OnDragOver(TreeViewNode<Model> item)
        {
            if (!EnableDragDrop)
                return;
            var target = IsDropAllowed(item) ? item : null;
            if (dragOverItem != target || dragOverRoot)
            {
                dragOverItem = target;
                dragOverRoot = false;
                StateHasChanged();
            }
        }

        private void OnDragOverRoot()
        {
            if (!EnableDragDrop)
                return;
            if (!dragOverRoot || dragOverItem != null)
            {
                dragOverRoot = true;
                dragOverItem = null;
                StateHasChanged();
            }
        }

        private async Task OnDrop(TreeViewNode<Model> item)
        {
            if (!EnableDragDrop)
                return;

            var dragged = draggedItem;
            dragOverItem = null;
            dragOverRoot = false;
            draggedItem = null;

            if (dragged != null && dragged != item && (CanDrop == null || CanDrop(dragged, item)))
                await ItemDropped.InvokeAsync((dragged, item));

            StateHasChanged();
        }

        private async Task OnDropRoot()
        {
            if (!EnableDragDrop)
                return;

            // Item drops stopPropagation, so reaching here means the drop landed on the root drop zone
            // or empty tree space: move to the root (target null).
            var dragged = draggedItem;
            dragOverItem = null;
            dragOverRoot = false;
            draggedItem = null;

            if (dragged != null && (CanDrop == null || CanDrop(dragged, null)))
                await ItemDropped.InvokeAsync((dragged, null));

            StateHasChanged();
        }
    }
}
