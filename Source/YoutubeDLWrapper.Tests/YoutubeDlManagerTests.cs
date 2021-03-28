using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoreLinq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace YoutubeDLWrapper.Tests
{
    [TestClass]
    public class YoutubeDlManagerTests
    {
        public TestContext TestContext { get; set; }

        private readonly LoggerFactory loggerFactory = new LoggerFactory();

        [TestMethod]
        public async Task DownloadTest()
        {
            var manager = new YoutubeDLManager(loggerFactory)
            {
                StorePath = TestContext.TestDir
            };

            await manager.Initialize();
            Assert.AreEqual(0, manager.Versions.Count);

            // Download latest version, we don't have any version
            Assert.AreEqual(true, await manager.DownloadLatestVersion());
            Assert.AreEqual(1, manager.Versions.Count);

            // We already have the latest version, this should not do anything
            Assert.AreEqual(false, await manager.DownloadLatestVersion());
            Assert.AreEqual(1, manager.Versions.Count);

            await manager.CleanupOldVersions();
            Assert.AreEqual(1, manager.Versions.Count);
        }

        [TestMethod]
        public async Task MultipleVersionsTest()
        {
            TestUtils.DeployEmbeddedResource("youtube-dl-2000", TestContext.TestRunDirectory);
            TestUtils.DeployEmbeddedResource("youtube-dl-2001", TestContext.TestRunDirectory);
            TestUtils.DeployEmbeddedResource("youtube-dl-2002", TestContext.TestRunDirectory);

            var manager = new YoutubeDLManager(loggerFactory)
            {
                StorePath = TestContext.TestRunDirectory
            };

            await manager.Initialize();
            Assert.AreEqual(3, manager.Versions.Count);

            Assert.AreEqual(true, await manager.DownloadLatestVersion());
            Assert.AreEqual(4, manager.Versions.Count);

            await manager.CleanupOldVersions();
            Assert.AreEqual(1, manager.Versions.Count);
            Assert.IsTrue(manager.Versions.First().Key.Major > 2002);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Directory.GetFiles(TestContext.TestRunDirectory, "youtube-dl-*")
                .ForEach(File.Delete);
        }
    }
}
