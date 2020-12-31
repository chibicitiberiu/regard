using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Regard.Backend.Tests
{
    interface ITest
    {
    }

    class TestA : ITest
    {
        public string A => "A";
    }

    class TestB : TestA, ITest
    {
        public string B => "B";
    }

    [TestClass]
    public class MyTest
    {
        [TestMethod]
        public void MyActualTest()
        {
            TestA a = new TestA();
            TestB b = new TestB();
            TestA c = new TestB();
            ITest d = new TestB();
            ITest e = new TestA();

            Console.WriteLine(JsonSerializer.Serialize(a, a.GetType()));
            Console.WriteLine(JsonSerializer.Serialize(b, b.GetType()));
            Console.WriteLine(JsonSerializer.Serialize(c, c.GetType()));
            Console.WriteLine(JsonSerializer.Serialize(d, d.GetType()));
            Console.WriteLine(JsonSerializer.Serialize(e, e.GetType()));
        }
    }
}
