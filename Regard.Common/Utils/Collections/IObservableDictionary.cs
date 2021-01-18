using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.Utils.Collections
{
    public interface IObservableDictionary<TKey, TValue>
    {
        event EventHandler<DictionaryChangedEventArgs<TKey, TValue>> DictionaryChanged;
    }
}
