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
    public class TreeViewNode<Model> : INotifyPropertyChanged
    {
        /// <summary>
        /// Event called when a property value of this node changes
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Event called when a property value of either this node or any child changes
        /// </summary>
        public event PropertyChangedEventHandler ChildPropertyChanged;

        /// <summary>
        /// Event called when items are added or removed from the subtree
        /// </summary>
        public event NotifyCollectionChangedEventHandler TreeChanged;

        private bool isEnabled = true;
        private bool isSelected = false;
        private bool isExpanded = true;
        private Model data = default;
        private TreeViewNode<Model> parent = null;

        /// <summary>
        /// Gets or sets a value indicating if this tree item is enabled
        /// </summary>
        public bool IsEnabled 
        {
            get => isEnabled;
            set => SetField(ref isEnabled, value);
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
                if (!IsEnabled)
                    str.Append("disabled ");
                str.Append(IsExpanded ? "expanded " : "collapsed ");
                return str.ToString();
            }
        }

        /// <summary>
        /// Gets or sets the associated data
        /// </summary>
        public Model Data 
        { 
            get => data;
            set => SetField(ref data, value);
        }

        /// <summary>
        /// Gets or sets the parent tree node
        /// </summary>
        public TreeViewNode<Model> Parent
        {
            get => parent;
            set => SetField(ref parent, value);
        }

        /// <summary>
        /// Gets the current depth in the tree
        /// </summary>
        public int Level { get => (parent != null) ? (parent.Level + 1) : 0; }

        /// <summary>
        /// Gets the collection of children
        /// </summary>
        public ObservableCollection<TreeViewNode<Model>> Children { get; } = new ObservableCollection<TreeViewNode<Model>>();

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

        /// <summary>
        /// Sets backing field of property and calls NotifyPropertyChanged
        /// </summary>
        /// <typeparam name="T">Field datatype</typeparam>
        /// <param name="field">Backing field</param>
        /// <param name="value">New value</param>
        /// <param name="propertyName">Name of property, filled automatically when called from setter</param>
        protected void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                NotifyPropertyChanged(propertyName);
            }
        }

        /// <summary>
        /// Notify property changed
        /// </summary>
        /// <param name="propertyName">Property name</param>
        protected void NotifyPropertyChanged(string propertyName)
        {
            var e = new PropertyChangedEventArgs(propertyName);
            PropertyChanged?.Invoke(this, e);
            ChildPropertyChanged?.Invoke(this, e);
        }

        private void OnChildrenCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
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
    }
}
