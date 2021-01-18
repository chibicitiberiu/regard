using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Regard.Common.Utils.Collections
{
    public class DictionaryChangedEventArgs<TKey, TValue> : EventArgs
    {
        public NotifyCollectionChangedAction Action { get; set; }

        public IList<KeyValuePair<TKey, TValue>> NewItems { get; set; }

        public IList<KeyValuePair<TKey, TValue>> OldItems { get; set; }
    }
}
