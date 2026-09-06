using Microsoft.VisualStudio.TestTools.UnitTesting;
using Regard.Backend.Downloader;
using System.IO;

namespace Regard.Backend.Tests.Downloader
{
    [TestClass]
    public class OutputSizeProbeTests
    {
        private string dir;

        [TestInitialize]
        public void Setup()
        {
            dir = Path.Combine(Path.GetTempPath(), "regard-probe-" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try { Directory.Delete(dir, true); } catch { }
        }

        private void Write(string name, int bytes) =>
            File.WriteAllBytes(Path.Combine(dir, name), new byte[bytes]);

        [TestMethod]
        public void Belongs_ExactAndDotBoundary()
        {
            Assert.IsTrue(OutputSizeProbe.Belongs("Ep 1", "Ep 1"));
            Assert.IsTrue(OutputSizeProbe.Belongs("Ep 1.f395.mp4.part", "Ep 1"));
            Assert.IsTrue(OutputSizeProbe.Belongs("Ep 1.info.json", "Ep 1"));
        }

        [TestMethod]
        public void Belongs_RejectsSpaceOrDigitSibling()
        {
            Assert.IsFalse(OutputSizeProbe.Belongs("Ep 1 2.mp4", "Ep 1"));   // space-separated sibling
            Assert.IsFalse(OutputSizeProbe.Belongs("Ep 10.mp4", "Ep 1"));    // longer number, no boundary
        }

        [TestMethod]
        public void Belongs_DottedSibling_IsAKnownOverMatch()
        {
            // Documented limitation: "Ep 1.5…" starts with "Ep 1." so it over-matches. Harmless under the
            // shipped default template (a unique SxxExx EpisodeCode prefix makes bases non-colliding);
            // only a custom {Video.Name}-only template with "1.5"/"2.0"-style titles is exposed.
            Assert.IsTrue(OutputSizeProbe.Belongs("Ep 1.5 Special.f395.mp4", "Ep 1"));
        }

        [TestMethod]
        public void Sum_CountsOnlyThisBase()
        {
            Write("Ep 1.f395.mp4.part", 1000);
            Write("Ep 1.f251.webm.part", 2000);
            Write("Ep 1.info.json", 50);
            Write("Ep 2.f395.mp4.part", 9999);   // sibling — must not be counted
            Assert.AreEqual(3050, OutputSizeProbe.Sum(dir, "Ep 1"));
        }

        [TestMethod]
        public void Sum_MissingDir_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, OutputSizeProbe.Sum(Path.Combine(dir, "does-not-exist"), "Ep 1"));
        }

        [TestMethod]
        public void Sum_NoMatch_ReturnsZero()
        {
            Write("Unrelated.mp4", 500);
            Assert.AreEqual(0, OutputSizeProbe.Sum(dir, "Ep 1"));
        }

        [TestMethod]
        public void Sum_NullOrEmptyArgs_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, OutputSizeProbe.Sum(null, "Ep 1"));
            Assert.AreEqual(-1, OutputSizeProbe.Sum(dir, ""));
        }
    }
}
