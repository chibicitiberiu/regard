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
    public class ObservableDictionaryTests
    {
        [TestMethod]
        public void AddTest()
        {
            var dictionary = new ObservableDictionary<int, string>();

            DictionaryChangedEventArgs<int, string> args = null;
            dictionary.DictionaryChanged += (sender, e) => args = e;

            for (int i = 0; i < 50; i++)
            {
                args = null;
                dictionary.Add(i, i.ToString());
                Assert.IsNotNull(args);
                Assert.AreEqual(NotifyCollectionChangedAction.Add, args.Action);
                Assert.AreEqual(1, args.NewItems.Count);
                Assert.AreEqual(i, args.NewItems[0].Key);
                Assert.AreEqual(i.ToString(), args.NewItems[0].Value);
                Assert.AreEqual(0, args.OldItems.Count);
                Assert.AreEqual(i + 1, dictionary.Count);
            }

            for (int i = 50; i < 100; i++)
            {
                args = null;
                dictionary[i] = i.ToString();
                Assert.IsNotNull(args);
                Assert.AreEqual(NotifyCollectionChangedAction.Add, args.Action);
                Assert.AreEqual(1, args.NewItems.Count);
                Assert.AreEqual(i, args.NewItems[0].Key);
                Assert.AreEqual(i.ToString(), args.NewItems[0].Value);
                Assert.AreEqual(0, args.OldItems.Count);
                Assert.AreEqual(i + 1, dictionary.Count);
            }
        }

        [TestMethod]
        public void RemoveTest()
        {
            var initialItems = MakeTestPairs(Enumerable.Range(0, 50));
            var dictionary = new ObservableDictionary<int, string>(initialItems);

            DictionaryChangedEventArgs<int, string> args = null;
            dictionary.DictionaryChanged += (sender, e) => args = e;

            for (int i = 0; i < 50; i++)
            {
                args = null;
                dictionary.Remove(i);
                Assert.IsNotNull(args);
                Assert.AreEqual(NotifyCollectionChangedAction.Remove, args.Action);
                Assert.AreEqual(0, args.NewItems.Count);
                Assert.AreEqual(1, args.OldItems.Count);
                Assert.AreEqual(i, args.OldItems[0].Key);
                Assert.AreEqual(i.ToString(), args.OldItems[0].Value);
                Assert.AreEqual(50 - i - 1, dictionary.Count);
            }
        }

        [TestMethod]
        public void ResetTest()
        {
            var initialItems = MakeTestPairs(Enumerable.Range(0, 50));
            var dictionary = new ObservableDictionary<int, string>(initialItems);

            DictionaryChangedEventArgs<int, string> args = null;
            dictionary.DictionaryChanged += (sender, e) => args = e;

            dictionary.Clear();
            Assert.IsNotNull(args);
            Assert.AreEqual(NotifyCollectionChangedAction.Reset, args.Action);
            Assert.AreEqual(0, args.NewItems.Count);
            Assert.AreEqual(0, args.OldItems.Count);
            Assert.AreEqual(0, dictionary.Count);
        }

        [TestMethod]
        public void ReplaceTest()
        {
            var initialItems = MakeTestPairs(Enumerable.Range(0, 50));
            var dictionary = new ObservableDictionary<int, string>(initialItems);

            DictionaryChangedEventArgs<int, string> args = null;
            dictionary.DictionaryChanged += (sender, e) => args = e;
            
            Random random = new Random();

            for (int i = 0; i < 20; i++)
            {
                int index = random.Next(dictionary.Count);
                if (dictionary[index] == "aaaa")
                    continue;

                args = null;
                dictionary[index] = "aaaa";

                Assert.IsNotNull(args);
                Assert.AreEqual(NotifyCollectionChangedAction.Replace, args.Action);
                Assert.AreEqual(1, args.NewItems.Count);
                Assert.AreEqual(index, args.NewItems[0].Key);
                Assert.AreEqual("aaaa", args.NewItems[0].Value);
                Assert.AreEqual(1, args.OldItems.Count);
                Assert.AreEqual(index, args.OldItems[0].Key);
                Assert.AreEqual(index.ToString(), args.OldItems[0].Value);
                Assert.AreEqual(50, dictionary.Count);
            }
        }

        [TestMethod]
        public void AddRangeTest()
        {
            DictionaryChangedEventArgs<int, string> args = null;

            var dictionary = new ObservableDictionary<int, string>();
            dictionary.DictionaryChanged += (sender, e) => args = e;

            dictionary.Add(1, "1");
            Assert.IsNotNull(args);
            Assert.AreEqual(NotifyCollectionChangedAction.Add, args.Action);
            Assert.AreEqual(1, args.NewItems.Count);
            Assert.AreEqual(1, args.NewItems[0].Key);
            Assert.AreEqual("1", args.NewItems[0].Value);

            args = null;
            dictionary.AddRange(MakeTestPairs(2, 3, 4, 5));
            Assert.IsNotNull(args);
            Assert.AreEqual(NotifyCollectionChangedAction.Add, args.Action);
            Assert.AreEqual(4, args.NewItems.Count);
            Assert.AreEqual(2, args.NewItems[0].Key);
            Assert.AreEqual("2", args.NewItems[0].Value);
            Assert.AreEqual(5, args.NewItems[3].Key);
            Assert.AreEqual("5", args.NewItems[3].Value);
            Assert.AreEqual(5, dictionary.Count);

            args = null;
            dictionary.AddRange(MakeTestPairs(6, 7, 8, 9, 10, 11, 12, 13, 14, 15));
            Assert.IsNotNull(args);
            Assert.AreEqual(NotifyCollectionChangedAction.Add, args.Action);
            Assert.AreEqual(10, args.NewItems.Count);
            Assert.AreEqual(6, args.NewItems[0].Key);
            Assert.AreEqual("6", args.NewItems[0].Value);
            Assert.AreEqual(15, args.NewItems[9].Key);
            Assert.AreEqual("15", args.NewItems[9].Value);
            Assert.AreEqual(15, dictionary.Count);
        }

        [TestMethod]
        public void BeginEndBulkOperationTest()
        {
            DictionaryChangedEventArgs<int, string> args = null;

            var dictionary = new ObservableDictionary<int, string>();
            dictionary.DictionaryChanged += (sender, e) => args = e;

            dictionary.BeginBatch();
            dictionary.Add(1, "1");
            Assert.IsNull(args);
            dictionary.Add(2, "2");
            Assert.IsNull(args);
            dictionary.EndBatch();
            Assert.IsNotNull(args);

            args = null;
            dictionary.BeginBatch();
            dictionary.AddRange(MakeTestPairs(4, 5, 6));
            Assert.IsNull(args);
            dictionary.Add(10, "10");
            Assert.IsNull(args);
            dictionary.Remove(4);
            Assert.IsNull(args);
            dictionary[4] = "44";
            Assert.IsNull(args);
            dictionary[44] = "44";
            Assert.IsNull(args);
            dictionary.Clear();
            Assert.IsNull(args);
            dictionary.EndBatch();
            Assert.IsNotNull(args);
        }

        [TestMethod]
        public void OperationCollapseTest()
        {
            var argsList = new List<DictionaryChangedEventArgs<int, string>>();
            var dictionary = new ObservableDictionary<int, string>();
            dictionary.DictionaryChanged += (sender, e) => argsList.Add(e);

            dictionary.BeginBatch();
            dictionary.Add(1, "1");         //  \
            dictionary.Add(2, "2");         //   > collapse into [0] add
            dictionary.Add(3, "3");         //  /
            dictionary.Remove(1);           //  \
            dictionary.Remove(2);           //   > collapse into [1] remove
            dictionary.Remove(3);           //  /
            dictionary.AddRange(MakeTestPairs(1, 2, 3));     //  \
            dictionary.Add(4, "4");                          //   > collapse into [2] add
            dictionary.AddRange(MakeTestPairs(5, 6, 7));     //  /
            dictionary.Remove(7);           //  \
            dictionary.Remove(6);           //   > collapse into [3] reset
            dictionary.Clear();             //  /
            dictionary[1] = "1";            //  \
            dictionary[2] = "2";            //   > collapse into [4] add
            dictionary[3] = "3";            //  /
            dictionary[1] = "1234";         // no collapse - [5] replace
            dictionary.Remove(1);           //  \ 
            dictionary.Clear();             //  / collapse into [6] reset
            Assert.AreEqual(0, argsList.Count);
            dictionary.EndBatch();

            Assert.AreEqual(7, argsList.Count);
            
            Assert.AreEqual(NotifyCollectionChangedAction.Add, argsList[0].Action);
            Assert.IsTrue(Enumerable.SequenceEqual(MakeTestPairs(1, 2, 3), argsList[0].NewItems));
            
            Assert.AreEqual(NotifyCollectionChangedAction.Remove, argsList[1].Action);
            Assert.IsTrue(Enumerable.SequenceEqual(MakeTestPairs(1, 2, 3), argsList[1].OldItems));

            Assert.AreEqual(NotifyCollectionChangedAction.Add, argsList[2].Action);
            Assert.IsTrue(Enumerable.SequenceEqual(MakeTestPairs(1, 2, 3, 4, 5, 6, 7), argsList[2].NewItems));

            Assert.AreEqual(NotifyCollectionChangedAction.Reset, argsList[3].Action);

            Assert.AreEqual(NotifyCollectionChangedAction.Add, argsList[4].Action);
            Assert.IsTrue(Enumerable.SequenceEqual(MakeTestPairs(1, 2, 3), argsList[4].NewItems));

            Assert.AreEqual(NotifyCollectionChangedAction.Replace, argsList[5].Action);

            Assert.AreEqual(NotifyCollectionChangedAction.Reset, argsList[6].Action);
        }

        private IEnumerable<KeyValuePair<int, string>> MakeTestPairs(params int[] values)
        {
            return values.Select(x => KeyValuePair.Create(x, x.ToString()));
        }

        private IEnumerable<KeyValuePair<int, string>> MakeTestPairs(IEnumerable<int> values)
        {
            return values.Select(x => KeyValuePair.Create(x, x.ToString()));
        }
    }
}
