using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;

namespace Regard.Common.Utils.Collections
{
    public class BulkObservableCollection<T> : Collection<T>, IBulkCollection<T>, IObservableCollection
    {
        private int batchOperation = 0;
        private readonly IList<CollectionChangedEventArgs> suppressedOperations = new List<CollectionChangedEventArgs>();

        /// <summary>
        /// Collection changed
        /// </summary>
        public event EventHandler<CollectionChangedEventArgs> CollectionChanged;

        /// <summary>
        /// Constructor
        /// </summary>
        public BulkObservableCollection() : base() { }

        public BulkObservableCollection(IEnumerable<T> items) : base()
        {
            AddRange(items);
        }

        public void AddRange(IEnumerable<T> items)
        {
            if (items == null)
                throw new ArgumentNullException("Items is null!");

            BeginBatch();

            foreach (var item in items)
                Add(item);

            EndBatch();
        }

        /// <summary>
        /// Move item at oldIndex to newIndex.
        /// </summary>
        public void Move(int oldIndex, int newIndex)
        {
            MoveItem(oldIndex, newIndex);
        }

        public void BeginBatch()
        {
            batchOperation++;
        }

        public void EndBatch()
        {
            batchOperation = Math.Max(batchOperation - 1, 0);

            if (batchOperation == 0)
            {
                foreach (var eventArgs in suppressedOperations)
                    OnCollectionChanged(eventArgs);

                suppressedOperations.Clear();
            }
        }

        protected override void ClearItems()
        {
            base.ClearItems();
            OnCollectionChanged(NotifyCollectionChangedAction.Reset, null, null);
        }

        protected override void RemoveItem(int index)
        {
            object item = base[index];
            base.RemoveItem(index);
            OnCollectionChanged(NotifyCollectionChangedAction.Remove, null, item, null, index);
        }

        protected override void InsertItem(int index, T item)
        {
            base.InsertItem(index, item);
            OnCollectionChanged(NotifyCollectionChangedAction.Add, item, null, index);
        }

        protected override void SetItem(int index, T item)
        {
            object oldItem = base[index];
            base.SetItem(index, item);
            OnCollectionChanged(NotifyCollectionChangedAction.Replace, item, oldItem, index, index);
        }

        protected virtual void MoveItem(int oldIndex, int newIndex)
        {
            T item = base[oldIndex];
            base.RemoveItem(oldIndex);
            base.InsertItem(newIndex, item);
            OnCollectionChanged(NotifyCollectionChangedAction.Move, item, item, newIndex, oldIndex);
        }

        protected virtual void OnCollectionChanged(CollectionChangedEventArgs e)
        {
            if (batchOperation > 0)
            {
                // Try to merge with last operation
                if (!TryMergeOperation(e))
                    suppressedOperations.Add(e);
            }
            else
            {
                CollectionChanged?.Invoke(this, e);
            }
        }

        private bool TryMergeOperation(CollectionChangedEventArgs op)
        {
            if (suppressedOperations.Count == 0)
                return false;

            var prevOp = suppressedOperations[suppressedOperations.Count - 1];

            switch (op.Action)
            {
                case NotifyCollectionChangedAction.Add:
                case NotifyCollectionChangedAction.Remove:
                    if (prevOp.Action == op.Action)
                    {
                        prevOp.NewItems.AddRange(op.NewItems);
                        prevOp.NewIndex = null;
                        prevOp.OldItems.AddRange(op.OldItems);
                        prevOp.OldIndex = null;
                        return true;
                    }
                    break;

                case NotifyCollectionChangedAction.Reset:
                    if (prevOp.Action == NotifyCollectionChangedAction.Remove)
                    {
                        // Remove followed by delete, we can safely delete the "remove" operation
                        suppressedOperations.RemoveAt(suppressedOperations.Count - 1);
                        return false;
                    }
                    else if (prevOp.Action == NotifyCollectionChangedAction.Reset)
                    {
                        // Reset followed by another reset, no need to do anything
                        return true;
                    }
                    break;
            }
            return false;
        }

        private void OnCollectionChanged(NotifyCollectionChangedAction action, object newItem, object oldItem, int? newIndex = null, int? oldIndex = null)
        {
            var e = new CollectionChangedEventArgs()
            {
                Action = action,
                NewItems = new ArrayList(),
                NewIndex = newIndex,
                OldItems = new ArrayList(),
                OldIndex = oldIndex
            };

            // null is also a valid item, so add what makes sense depending on the operation
            if (action == NotifyCollectionChangedAction.Add
                || action == NotifyCollectionChangedAction.Replace
                || action == NotifyCollectionChangedAction.Move)
                e.NewItems.Add(newItem);

            if (action == NotifyCollectionChangedAction.Remove
                || action == NotifyCollectionChangedAction.Replace
                || action == NotifyCollectionChangedAction.Move)
                e.OldItems.Add(oldItem);

            OnCollectionChanged(e);
        }
    }
}
