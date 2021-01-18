using Microsoft.VisualStudio.TestTools.UnitTesting;
using Regard.Common.Utils;
using Regard.Common.Utils.Collections;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace Regard.Backend.Tests
{
    [TestClass]
    public class BulkObservableCollectionTests
    {
        [TestMethod]
        public void AddTest()
        {
            CollectionChangedEventArgs args = null;

            var collection = new BulkObservableCollection<int>();
            collection.CollectionChanged += (sender, e) => args = e;

            for (int i = 0; i < 50; i++)
            {
                args = null;
                collection.Add(i);
                Assert.IsNotNull(args);
                Assert.AreEqual(NotifyCollectionChangedAction.Add, args.Action);
                Assert.AreEqual(1, args.NewItems.Count);
                Assert.AreEqual(i, args.NewItems[0]);
                Assert.AreEqual(i, args.NewIndex);
                Assert.AreEqual(0, args.OldItems.Count);
                Assert.IsNull(args.OldIndex);
                Assert.AreEqual(i + 1, collection.Count);
            }
        }

        [TestMethod]
        public void RemoveTest()
        {
            CollectionChangedEventArgs args = null;
            var collection = new BulkObservableCollection<int>(Enumerable.Range(0, 50));
            collection.CollectionChanged += (sender, e) => args = e;

            for (int i = 0; i < 50; i++)
            {
                args = null;
                collection.Remove(i);
                Assert.IsNotNull(args);
                Assert.AreEqual(NotifyCollectionChangedAction.Remove, args.Action);
                Assert.AreEqual(0, args.NewItems.Count);
                Assert.IsNull(args.NewIndex);
                Assert.AreEqual(1, args.OldItems.Count);
                Assert.AreEqual(i, args.OldItems[0]);
                Assert.AreEqual(0, args.OldIndex);
                Assert.AreEqual(50 - i - 1, collection.Count);
            }
        }

        [TestMethod]
        public void MoveTest()
        {
            CollectionChangedEventArgs args = null;
            Random random = new Random();
            var collection = new BulkObservableCollection<int>(Enumerable.Range(0, 50));
            collection.CollectionChanged += (sender, e) => args = e;

            int oldIndex = random.Next(collection.Count);
            int newIndex = random.Next(collection.Count);

            collection.Move(oldIndex, newIndex);
            Assert.IsNotNull(args);
            Assert.AreEqual(NotifyCollectionChangedAction.Move, args.Action);
            Assert.AreEqual(1, args.NewItems.Count);
            Assert.AreEqual(oldIndex, args.NewItems[0]);
            Assert.AreEqual(newIndex, args.NewIndex);
            Assert.AreEqual(1, args.OldItems.Count);
            Assert.AreEqual(oldIndex, args.OldItems[0]);
            Assert.AreEqual(oldIndex, args.OldIndex);
            Assert.AreEqual(50, collection.Count);
        }

        [TestMethod]
        public void ResetTest()
        {
            CollectionChangedEventArgs args = null;
            var collection = new BulkObservableCollection<int>(Enumerable.Range(0, 50));
            collection.CollectionChanged += (sender, e) => args = e;

            collection.Clear();
            Assert.IsNotNull(args);
            Assert.AreEqual(NotifyCollectionChangedAction.Reset, args.Action);
            Assert.AreEqual(0, args.NewItems.Count);
            Assert.IsNull(args.NewIndex);
            Assert.AreEqual(0, args.OldItems.Count);
            Assert.IsNull(args.OldIndex);
            Assert.AreEqual(0, collection.Count);
        }

        [TestMethod]
        public void ReplaceTest()
        {
            CollectionChangedEventArgs args = null;
            Random random = new Random();
            var collection = new BulkObservableCollection<int>(Enumerable.Range(0, 50));
            collection.CollectionChanged += (sender, e) => args = e;

            for (int i = 0; i < 20; i++)
            {
                int index = random.Next(collection.Count);
                if (collection[index] == 1337)
                    continue;

                args = null;
                collection[index] = 1337;

                Assert.IsNotNull(args);
                Assert.AreEqual(NotifyCollectionChangedAction.Replace, args.Action);
                Assert.AreEqual(1, args.NewItems.Count);
                Assert.AreEqual(1337, args.NewItems[0]);
                Assert.AreEqual(index, args.NewIndex);
                Assert.AreEqual(1, args.OldItems.Count);
                Assert.AreEqual(index, args.OldItems[0]);
                Assert.AreEqual(index, args.OldIndex);
                Assert.AreEqual(50, collection.Count);
            }
        }

        [TestMethod]
        public void AddRangeTest()
        {
            CollectionChangedEventArgs args = null;

            var collection = new BulkObservableCollection<int>();
            collection.CollectionChanged += (sender, e) => args = e;

            collection.Add(1);
            Assert.IsNotNull(args);
            Assert.AreEqual(NotifyCollectionChangedAction.Add, args.Action);
            Assert.AreEqual(1, args.NewItems.Count);
            Assert.AreEqual(1, args.NewItems[0]);

            args = null;
            collection.AddRange(new[] { 2, 3, 4, 5 });
            Assert.IsNotNull(args);
            Assert.AreEqual(NotifyCollectionChangedAction.Add, args.Action);
            Assert.AreEqual(4, args.NewItems.Count);
            Assert.AreEqual(2, args.NewItems[0]);
            Assert.AreEqual(5, args.NewItems[3]);
            Assert.AreEqual(5, collection.Count);

            args = null;
            collection.AddRange(new[] { 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 });
            Assert.IsNotNull(args);
            Assert.AreEqual(NotifyCollectionChangedAction.Add, args.Action);
            Assert.AreEqual(10, args.NewItems.Count);
            Assert.AreEqual(6, args.NewItems[0]);
            Assert.AreEqual(15, args.NewItems[9]);
            Assert.AreEqual(15, collection.Count);
        }

        [TestMethod]
        public void BeginEndBulkOperationTest()
        {
            CollectionChangedEventArgs args = null;

            var collection = new BulkObservableCollection<int>();
            collection.CollectionChanged += (sender, e) => args = e;

            collection.BeginBatch();
            collection.Add(1);
            Assert.IsNull(args);
            collection.Add(2);
            Assert.IsNull(args);
            collection.EndBatch();
            Assert.IsNotNull(args);

            args = null;
            collection.BeginBatch();
            collection.AddRange(new[] { 1, 2, 3 });
            Assert.IsNull(args);
            collection.Add(4);
            Assert.IsNull(args);
            collection.Remove(4);
            Assert.IsNull(args);
            collection.Insert(0, 5);
            Assert.IsNull(args);
            collection.Move(0, 1);
            Assert.IsNull(args);
            collection.RemoveAt(0);
            Assert.IsNull(args);
            collection.Clear();
            Assert.IsNull(args);
            collection.EndBatch();
            Assert.IsNotNull(args);
        }

        [TestMethod]
        public void OperationCollapseTest()
        {
            var argsList = new List<CollectionChangedEventArgs>();
            var collection = new BulkObservableCollection<int>();
            collection.CollectionChanged += (sender, e) => argsList.Add(e);

            collection.BeginBatch();
            collection.Add(1);              //  \
            collection.Add(2);              //   > collapse into 1 add
            collection.Add(3);              //  /
            collection.Remove(1);           //  \
            collection.Remove(2);           //   > collapse into 1 remove
            collection.Remove(3);           //  /
            collection.AddRange(new[] { 1, 2, 3 });     //  \
            collection.Add(4);                          //   > collapse into 1 add
            collection.AddRange(new[] { 5, 6, 7 });     //  /
            collection.Remove(7);           //  \
            collection.Remove(6);           //   > collapse into 1 clear
            collection.Clear();             //  /
            collection.Add(1);              //  \
            collection.Add(2);              //   > collapse into 1 add
            collection.Add(3);              //  /
            collection[0] = 1337;           // no collapse
            collection.Remove(1337);        // no collapse
            collection.Move(0, 1);          // no collapse
            collection.Clear();             // no collapse
            Assert.AreEqual(0, argsList.Count);
            collection.EndBatch();

            Assert.AreEqual(9, argsList.Count);
            
            Assert.AreEqual(NotifyCollectionChangedAction.Add, argsList[0].Action);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, argsList[0].NewItems);

            Assert.AreEqual(NotifyCollectionChangedAction.Remove, argsList[1].Action);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, argsList[1].OldItems);

            Assert.AreEqual(NotifyCollectionChangedAction.Add, argsList[2].Action);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6, 7 }, argsList[2].NewItems);

            Assert.AreEqual(NotifyCollectionChangedAction.Reset, argsList[3].Action);

            Assert.AreEqual(NotifyCollectionChangedAction.Add, argsList[4].Action);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, argsList[4].NewItems);

            Assert.AreEqual(NotifyCollectionChangedAction.Replace, argsList[5].Action);

            Assert.AreEqual(NotifyCollectionChangedAction.Remove, argsList[6].Action);

            Assert.AreEqual(NotifyCollectionChangedAction.Move, argsList[7].Action);

            Assert.AreEqual(NotifyCollectionChangedAction.Reset, argsList[8].Action);
        }
    }
}
