using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Common.Utils
{
    public class SortedObservableCollection<T, TKey> : BulkObservableCollection<T>
    {
        private readonly Func<T, TKey> keySelector;
        private readonly Comparer<TKey> comparer;

        public SortedObservableCollection(Func<T, TKey> keySelector)
        {
            this.keySelector = keySelector;
            this.comparer = Comparer<TKey>.Default;
        }

        protected override void InsertItem(int index, T item)
        {
            base.InsertItem(FindInsertPosition(item), item);
        }

        protected override void SetItem(int index, T item)
        {
            base.RemoveItem(index);
            base.Add(item);
        }

        private int FindInsertPosition(T item)
        {
            int left = 0;
            int right = Count - 1;

            while (left <= right)
            {
                int mid = (left + right) / 2;
                int c = comparer.Compare(keySelector(item), keySelector(this[mid]));

                if (c == 0) return mid;
                if (c > 0) left = mid + 1;
                else right = mid - 1;
            }

            return left;
        }
    }

    public class SortedObservableCollection<T> : SortedObservableCollection<T, T>
    {
        public SortedObservableCollection() : base(x => x)
        {
        }
    }
}
