using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.Utils.Collections
{
    public interface IObservableCollection
    {
        public event EventHandler<CollectionChangedEventArgs> CollectionChanged;
    }
}
