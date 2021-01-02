using Microsoft.VisualStudio.TestTools.UnitTesting;
using Regard.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Backend.Tests
{
    [TestClass]
    public class SortedObservableCollectionTests
    {
        [TestMethod]
        public void SmokeIntTest()
        {
            SortedObservableCollection<int> mySortedCollection = new SortedObservableCollection<int>();
            mySortedCollection.Add(5);
            mySortedCollection.Add(2);
            mySortedCollection.Add(3);
            mySortedCollection.Add(7);
            mySortedCollection.Add(9);

            Assert.AreEqual(5, mySortedCollection.Count);
            Assert.AreEqual(2, mySortedCollection[0]);
            Assert.AreEqual(3, mySortedCollection[1]);
            Assert.AreEqual(5, mySortedCollection[2]);
            Assert.AreEqual(7, mySortedCollection[3]);
            Assert.AreEqual(9, mySortedCollection[4]);
        }

        [TestMethod]
        public void OrderedIntTest()
        {
            SortedObservableCollection<int> mySortedCollection = new SortedObservableCollection<int>();
            mySortedCollection.Add(2);
            mySortedCollection.Add(3);
            mySortedCollection.Add(5);
            mySortedCollection.Add(7);
            mySortedCollection.Add(9);

            Assert.AreEqual(5, mySortedCollection.Count);
            Assert.AreEqual(2, mySortedCollection[0]);
            Assert.AreEqual(3, mySortedCollection[1]);
            Assert.AreEqual(5, mySortedCollection[2]);
            Assert.AreEqual(7, mySortedCollection[3]);
            Assert.AreEqual(9, mySortedCollection[4]);
        }

        [TestMethod]
        public void ReverseIntTest()
        {
            SortedObservableCollection<int> mySortedCollection = new SortedObservableCollection<int>();
            mySortedCollection.Add(9);
            mySortedCollection.Add(7);
            mySortedCollection.Add(5);
            mySortedCollection.Add(3);
            mySortedCollection.Add(2);

            Assert.AreEqual(5, mySortedCollection.Count);
            Assert.AreEqual(2, mySortedCollection[0]);
            Assert.AreEqual(3, mySortedCollection[1]);
            Assert.AreEqual(5, mySortedCollection[2]);
            Assert.AreEqual(7, mySortedCollection[3]);
            Assert.AreEqual(9, mySortedCollection[4]);
        }


        [TestMethod]
        public void SmokeStringTest()
        {
            SortedObservableCollection<string> mySortedCollection = new SortedObservableCollection<string>
            {
                "ddeddd",
                "bbbb",
                "aaaaa",
                "eeeee",
                "ccacaccc",
                "ffafasfdsd",
            };

            Assert.AreEqual(6, mySortedCollection.Count);
            Assert.AreEqual("aaaaa", mySortedCollection[0]);
            Assert.AreEqual("bbbb", mySortedCollection[1]);
            Assert.AreEqual("ccacaccc", mySortedCollection[2]);
            Assert.AreEqual("ddeddd", mySortedCollection[3]);
            Assert.AreEqual("eeeee", mySortedCollection[4]);
            Assert.AreEqual("ffafasfdsd", mySortedCollection[5]);
        }

        [TestMethod]
        public void OrderedStringTest()
        {
            SortedObservableCollection<string> mySortedCollection = new SortedObservableCollection<string>
            {
                "aaaaa",
                "bbbb",
                "ccacaccc",
                "ddeddd",
                "eeeee",
                "ffafasfdsd",
            };

            Assert.AreEqual(6, mySortedCollection.Count);
            Assert.AreEqual("aaaaa", mySortedCollection[0]);
            Assert.AreEqual("bbbb", mySortedCollection[1]);
            Assert.AreEqual("ccacaccc", mySortedCollection[2]);
            Assert.AreEqual("ddeddd", mySortedCollection[3]);
            Assert.AreEqual("eeeee", mySortedCollection[4]);
            Assert.AreEqual("ffafasfdsd", mySortedCollection[5]);
        }

        [TestMethod]
        public void ReverseStringTest()
        {
            SortedObservableCollection<string> mySortedCollection = new SortedObservableCollection<string>
            {
                "ffafasfdsd",
                "eeeee",
                "ddeddd",
                "ccacaccc",
                "bbbb",
                "aaaaa",
            };

            Assert.AreEqual(6, mySortedCollection.Count);
            Assert.AreEqual("aaaaa", mySortedCollection[0]);
            Assert.AreEqual("bbbb", mySortedCollection[1]);
            Assert.AreEqual("ccacaccc", mySortedCollection[2]);
            Assert.AreEqual("ddeddd", mySortedCollection[3]);
            Assert.AreEqual("eeeee", mySortedCollection[4]);
            Assert.AreEqual("ffafasfdsd", mySortedCollection[5]);
        }

        [TestMethod]
        public void InsertTest()
        {
            SortedObservableCollection<int> mySortedCollection = new SortedObservableCollection<int>();
            mySortedCollection.Insert(0, 5);
            mySortedCollection.Insert(0, 2);
            mySortedCollection.Insert(1, 3);
            mySortedCollection.Insert(0, 7);
            mySortedCollection.Insert(1, 9);

            Assert.AreEqual(5, mySortedCollection.Count);
            Assert.AreEqual(2, mySortedCollection[0]);
            Assert.AreEqual(3, mySortedCollection[1]);
            Assert.AreEqual(5, mySortedCollection[2]);
            Assert.AreEqual(7, mySortedCollection[3]);
            Assert.AreEqual(9, mySortedCollection[4]);
        }

        [TestMethod]
        public void SetterTest()
        {
            SortedObservableCollection<string> mySortedCollection = new SortedObservableCollection<string>()
            {
                "ddeddd",
                "bbbb",
                "aaaaa",
                "eeeee",
                "ccacaccc",
                "ffafasfdsd",
            };

            Assert.AreEqual(6, mySortedCollection.Count);
            Assert.AreEqual("aaaaa", mySortedCollection[0]);
            Assert.AreEqual("bbbb", mySortedCollection[1]);
            Assert.AreEqual("ccacaccc", mySortedCollection[2]);
            Assert.AreEqual("ddeddd", mySortedCollection[3]);
            Assert.AreEqual("eeeee", mySortedCollection[4]);
            Assert.AreEqual("ffafasfdsd", mySortedCollection[5]);

            mySortedCollection[0] = "zzzz";
            mySortedCollection[0] = "babababa";
            mySortedCollection[4] = "caaaa";

            Assert.AreEqual(6, mySortedCollection.Count);
            Assert.AreEqual("babababa", mySortedCollection[0]);
            Assert.AreEqual("caaaa", mySortedCollection[1]);
            Assert.AreEqual("ccacaccc", mySortedCollection[2]);
            Assert.AreEqual("ddeddd", mySortedCollection[3]);
            Assert.AreEqual("eeeee", mySortedCollection[4]);
            Assert.AreEqual("zzzz", mySortedCollection[5]);
        }

        [TestMethod]
        public void StressTest()
        {
            List<KeyValuePair<int, string>> items = new List<KeyValuePair<int, string>>();
            SortedObservableCollection<KeyValuePair<int, string>, int> mySortedCollection = new SortedObservableCollection<KeyValuePair<int, string>, int>(x => x.Key);
            Random random = new Random();

            for (int i = 0; i < 10000; i++)
            {
                var item = new KeyValuePair<int, string>(random.Next(), random.Next().ToString());
                items.Add(item);
                mySortedCollection.Add(item);
            }

            // Sort list, sorted list should be the same
            items.Sort((x, y) => x.Key - y.Key);

            Assert.AreEqual(items.Count, mySortedCollection.Count);

            for (int i = 0; i < items.Count; i++)
                Assert.AreEqual(items[i].Key, mySortedCollection[i].Key);
        }
    }
}
