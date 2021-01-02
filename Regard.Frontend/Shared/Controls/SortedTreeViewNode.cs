using Regard.Common.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Controls
{
    public interface ISortedTreeViewNodeModel<TSortKey>
    {
        TSortKey SortKey { get; }
    }

    public class SortedTreeViewNode<Model, TSortKey> : TreeViewNode<Model> where Model : ISortedTreeViewNodeModel<TSortKey>
    {
        public override ObservableCollection<TreeViewNode<Model>> Children { get; } 
            = new SortedObservableCollection<TreeViewNode<Model>, TSortKey>(item => item.Data.SortKey);

        public SortedTreeViewNode() : base()
        {
        }

        public SortedTreeViewNode(Model data) : base(data)
        {
        }

        public SortedTreeViewNode(Model data, TreeViewNode<Model> parent) : base(data, parent)
        {
        }
    }
}
