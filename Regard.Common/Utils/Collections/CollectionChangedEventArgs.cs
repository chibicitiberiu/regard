using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;

namespace Regard.Common.Utils.Collections
{
    public class CollectionChangedEventArgs : EventArgs
    {
        public NotifyCollectionChangedAction Action { get; set; }

        public IList NewItems { get; set; }

        public IList OldItems { get; set; }

        public int? NewIndex { get; set; }

        public int? OldIndex { get; set; }
    }
}
