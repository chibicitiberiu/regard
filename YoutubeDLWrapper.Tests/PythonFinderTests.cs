using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace YoutubeDLWrapper.Tests
{
    [TestClass]
    public class PythonFinderTests
    {
        /// <summary>
        /// Just makes sure stuff is working
        /// </summary>
        [TestMethod]
        public void SmokeTest()
        {
            Console.WriteLine(PythonFinder.FindPython3());
            Console.WriteLine(PythonFinder.FindPython3(true));
        }
    }
}
