using Microsoft.AspNetCore.Components;
using Regard.Common.Utils;
using Regard.Common.Utils.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Controls
{
    public class TreeViewNode<Model> : NotifyPropertyChangedBase
    {
        /// <summary>
        /// Event called when a property value of either this node or any child changes
        /// </summary>
        public event PropertyChangedEventHandler ChildPropertyChanged;

        /// <summary>
        /// Event called when items are added or removed from the subtree
        /// </summary>
        public event EventHandler<CollectionChangedEventArgs> TreeChanged;

        private TreeViewNode<Model> parent = null;
        private bool isSelected = false;
        private bool isExpanded = true;
        private Model data = default;

        /// <summary>
        /// Gets the collection of children
        /// </summary>
        public virtual BulkObservableCollection<TreeViewNode<Model>> Children { get; } = new BulkObservableCollection<TreeViewNode<Model>>();

        /// <summary>
        /// Gets or sets the parent tree node
        /// </summary>
        public TreeViewNode<Model> Parent
        {
            get => parent;
            set => SetField(ref parent, value);
        }

        /// <summary>
        /// Gets or sets the associated data
        /// </summary>
        public Model Data
        {
            get => data;
            set 
            {
                if (data is INotifyPropertyChanged oldData)
                    oldData.PropertyChanged -= OnDataPropertyChanged;
                SetField(ref data, value);
                if (data is INotifyPropertyChanged newData)
                    newData.PropertyChanged += OnDataPropertyChanged;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating if this tree item is selected
        /// </summary>
        public bool IsSelected
        {
            get => isSelected;
            set => SetField(ref isSelected, value);
        }

        /// <summary>
        /// Gets or sets a value indicating if this tree item is expanded
        /// </summary>
        public bool IsExpanded
        {
            get => isExpanded;
            set => SetField(ref isExpanded, value);
        }

        public string CssClasses
        { 
            get
            {
                var str = new StringBuilder();
                if (IsSelected)
                    str.Append("selected ");
                str.Append(IsExpanded ? "expanded " : "collapsed ");
                return str.ToString();
            }
        }

        public TreeViewNode()
        {
            Children.CollectionChanged += OnChildrenCollectionChanged;
        }
        
        public TreeViewNode(Model data) : this()
        {
            Data = data;
        }

        public TreeViewNode(Model data, TreeViewNode<Model> parent) : this(data)
        {
            parent.Children.Add(this);
        }

        protected override void NotifyPropertyChanged(string propertyName)
        {
            base.NotifyPropertyChanged(propertyName);
            ChildPropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OnChildrenCollectionChanged(object sender, CollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var oldChild in e.OldItems.Cast<TreeViewNode<Model>>())
                {
                    oldChild.ChildPropertyChanged -= ChildPropertyChanged;
                    oldChild.TreeChanged -= TreeChanged;
                    oldChild.Parent = null;
                }
            }
            if (e.NewItems != null)
            {
                foreach (var newChild in e.NewItems.Cast<TreeViewNode<Model>>())
                {
                    newChild.ChildPropertyChanged += ChildPropertyChanged;
                    newChild.TreeChanged += TreeChanged;
                    newChild.Parent = this;
                }
            }

            TreeChanged?.Invoke(sender, e);
        }

        private void OnDataPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            ChildPropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Data." + e.PropertyName));
        }
    }
}
