using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Controls
{
    public class SortedTreeView<Model, TSortKey> : TreeView<Model> where Model : ISortedTreeViewNodeModel<TSortKey>
    {
        public override TreeViewNode<Model> Root { get; } = new SortedTreeViewNode<Model, TSortKey>();
    }
}
