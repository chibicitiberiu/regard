using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.Serialization;
using System.Text;

namespace Regard.Common.Utils.Collections
{
    public class ObservableDictionary<TKey, TValue> : ICollection<KeyValuePair<TKey, TValue>>,
                                                      IBulkCollection<KeyValuePair<TKey, TValue>>,
                                                      IEnumerable<KeyValuePair<TKey, TValue>>,
                                                      IEnumerable, IDictionary<TKey, TValue>,
                                                      IReadOnlyCollection<KeyValuePair<TKey, TValue>>,
                                                      IReadOnlyDictionary<TKey, TValue>,
                                                      ICollection, 
                                                      IDictionary,
                                                      IDeserializationCallback, 
                                                      ISerializable,
                                                      IObservableDictionary<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> dict;
        private int batchOperation = 0;
        private readonly IList<DictionaryChangedEventArgs<TKey, TValue>> suppressedOperations = new List<DictionaryChangedEventArgs<TKey, TValue>>();

        /// <summary>
        /// Event called when the elements in the dictionary change
        /// </summary>
        public event EventHandler<DictionaryChangedEventArgs<TKey, TValue>> DictionaryChanged;

        public TValue this[TKey key] 
        { 
            get => dict[key];
            set => Set(key, value);
        }

        public ICollection<TKey> Keys => dict.Keys;

        public ICollection<TValue> Values => dict.Values;

        public int Count => dict.Count;

        public bool IsReadOnly => ((IDictionary<TKey, TValue>)dict).IsReadOnly;

        #region IReadOnlyDictionary

        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => ((IReadOnlyDictionary<TKey, TValue>)dict).Keys;

        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => ((IReadOnlyDictionary<TKey, TValue>)dict).Values;

        #endregion

        #region ICollection

        public bool IsSynchronized => ((ICollection)dict).IsSynchronized;

        public object SyncRoot => ((ICollection)dict).SyncRoot;

        public bool IsFixedSize => ((IDictionary)dict).IsFixedSize;
        
        #endregion

        #region IDictionary
        
        ICollection IDictionary.Keys => ((IDictionary)dict).Keys;

        ICollection IDictionary.Values => ((IDictionary)dict).Values;

        public object this[object key] 
        { 
            get => ((IDictionary)dict)[key];
            set => Set(key, value);
        }

        #endregion

        #region Constructors

        public ObservableDictionary() 
        {
            dict = new Dictionary<TKey, TValue>(); 
        }

        public ObservableDictionary(IDictionary<TKey, TValue> dictionary) 
        { 
            dict = new Dictionary<TKey, TValue>(dictionary);
        }

        public ObservableDictionary(IEnumerable<KeyValuePair<TKey, TValue>> collection)
        { 
            dict = new Dictionary<TKey, TValue>(collection);
        }

        public ObservableDictionary(IEqualityComparer<TKey> comparer)
        {
            dict = new Dictionary<TKey, TValue>(comparer);
        }

        public ObservableDictionary(int capacity)
        {
            dict = new Dictionary<TKey, TValue>(capacity);
        }

        public ObservableDictionary(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer) 
        {
            dict = new Dictionary<TKey, TValue>(dictionary, comparer);
        }

        public ObservableDictionary(IEnumerable<KeyValuePair<TKey, TValue>> collection, IEqualityComparer<TKey> comparer)
        {
            dict = new Dictionary<TKey, TValue>(collection, comparer);
        }

        public ObservableDictionary(int capacity, IEqualityComparer<TKey> comparer)
        {
            dict = new Dictionary<TKey, TValue>(capacity, comparer);
        }

        #endregion

        public void Add(TKey key, TValue value)
        {
            dict.Add(key, value);
            OnCollectionChanged(NotifyCollectionChangedAction.Add, newItem: KeyValuePair.Create(key, value));
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            ((ICollection<KeyValuePair<TKey, TValue>>)dict).Add(item);
            OnCollectionChanged(NotifyCollectionChangedAction.Add, newItem: item);
        }

        public void Clear()
        {
            dict.Clear();
            OnCollectionChanged(NotifyCollectionChangedAction.Reset);
        }

        public bool Contains(KeyValuePair<TKey, TValue> item) => ((ICollection<KeyValuePair<TKey, TValue>>)dict).Contains(item);


        public bool ContainsKey(TKey key) => dict.ContainsKey(key);

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => ((ICollection<KeyValuePair<TKey, TValue>>)dict).CopyTo(array, arrayIndex);

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => dict.GetEnumerator();

        public bool Remove(TKey key)
        {
            bool result = false;
            if (dict.TryGetValue(key, out TValue value))
            {
                result = dict.Remove(key);
                OnCollectionChanged(NotifyCollectionChangedAction.Remove, oldItem: KeyValuePair.Create(key, value));
            }
            return result;
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            if (((ICollection<KeyValuePair<TKey, TValue>>)dict).Remove(item))
            {
                OnCollectionChanged(NotifyCollectionChangedAction.Remove, oldItem: item);
                return true;
            }

            return false;
        }

        public bool TryGetValue(TKey key, out TValue value) => dict.TryGetValue(key, out value);

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)dict).GetEnumerator();

        public void CopyTo(Array array, int index) => ((ICollection)dict).CopyTo(array, index);

        public void Add(object key, object value)
        {
            if (key is TKey tKey)
            {
                if (value is TValue tValue)
                    Add(tKey, tValue);
                else throw new ArgumentException("Value should have data type " + typeof(TValue).Name, "value");
            }
            else throw new ArgumentException("Key should have data type " + typeof(TKey).Name, "key");
        }

        public bool Contains(object key) => ((IDictionary)dict).Contains(key);

        IDictionaryEnumerator IDictionary.GetEnumerator() => ((IDictionary)dict).GetEnumerator();

        public void Remove(object key)
        {
            if (key is TKey tKey)
                Remove(tKey);
            else throw new ArgumentException("Key should have data type " + typeof(TKey).Name, "key");
        }

        public void OnDeserialization(object sender) => dict.OnDeserialization(sender);

        public void GetObjectData(SerializationInfo info, StreamingContext context) => dict.GetObjectData(info, context);

        public void AddRange(IEnumerable<KeyValuePair<TKey, TValue>> range)
        {
            BeginBatch();

            foreach (var item in range)
                Add(item);

            EndBatch();
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

        protected void Set(TKey key, TValue value)
        {
            KeyValuePair<TKey, TValue>? oldItem = null;
            NotifyCollectionChangedAction action = NotifyCollectionChangedAction.Add;

            if (dict.TryGetValue(key, out TValue oldValue))
            {
                oldItem = KeyValuePair.Create(key, oldValue);
                action = NotifyCollectionChangedAction.Replace;
            }

            dict[key] = value;
            OnCollectionChanged(action, newItem: KeyValuePair.Create(key, value), oldItem: oldItem);
        }

        protected void Set(object key, object value)
        {
            if (key is TKey tKey)
            {
                if (value is TValue tValue)
                    Set(tKey, tValue);
                else throw new ArgumentException("Value should have data type " + typeof(TValue).Name, "value");
            }
            else throw new ArgumentException("Key should have data type " + typeof(TKey).Name, "key");
        }

        protected void OnCollectionChanged(NotifyCollectionChangedAction action, KeyValuePair<TKey, TValue>? newItem = null, KeyValuePair<TKey, TValue>? oldItem = null)
        {
            var e = new DictionaryChangedEventArgs<TKey, TValue>()
            {
                Action = action,
                NewItems = new List<KeyValuePair<TKey, TValue>>(),
                OldItems = new List<KeyValuePair<TKey, TValue>>(),
            };

            if (newItem.HasValue)
                e.NewItems.Add(newItem.Value);
            if (oldItem.HasValue)
                e.OldItems.Add(oldItem.Value);

            OnCollectionChanged(e);
        }

        protected virtual void OnCollectionChanged(DictionaryChangedEventArgs<TKey, TValue> e)
        {
            if (batchOperation > 0)
            {
                // Try to merge with last operation
                if (!TryMergeOperation(e))
                    suppressedOperations.Add(e);
            }
            else
            {
                DictionaryChanged?.Invoke(this, e);
            }
        }

        private bool TryMergeOperation(DictionaryChangedEventArgs<TKey, TValue> op)
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
                        prevOp.OldItems.AddRange(op.OldItems);
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
    }
}
