using Microsoft.VisualStudio.TestTools.UnitTesting;
using Regard.Backend.Services;
using System.IO;

namespace Regard.Backend.Tests.Utils
{
    [TestClass]
    public class SubtitleSentinelTests
    {
        private string media;  // extension-less DownloadedPath

        [TestInitialize]
        public void Setup()
        {
            var dir = Path.Combine(Path.GetTempPath(), "regard-nosubs-" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            media = Path.Combine(dir, "S2005E01 - Some Video");
        }

        [TestCleanup]
        public void Cleanup()
        {
            try { Directory.Delete(Path.GetDirectoryName(media), true); } catch { }
        }

        [TestMethod]
        public void Signature_IsOrderIndependentAndFlagSensitive()
        {
            Assert.AreEqual(SubtitleSentinel.Signature("en,ro", false, true, false),
                            SubtitleSentinel.Signature("RO, EN", false, true, false));
            Assert.AreNotEqual(SubtitleSentinel.Signature("en", false, true, false),
                               SubtitleSentinel.Signature("en", false, true, true));   // auto-subs flag changes it
            Assert.AreNotEqual(SubtitleSentinel.Signature("en", false, true, false),
                               SubtitleSentinel.Signature("en,ro", false, true, false)); // language set changes it
            Assert.AreEqual("s:all", SubtitleSentinel.Signature("en", true, true, false));
        }

        [TestMethod]
        public void Write_IsSatisfied_Clear_RoundTrip()
        {
            string sig = SubtitleSentinel.Signature("en", false, true, false);
            Assert.IsFalse(SubtitleSentinel.IsSatisfied(media, sig));   // none written yet
            SubtitleSentinel.Write(media, sig);
            Assert.IsTrue(File.Exists(media + ".nosubs"));
            Assert.IsTrue(SubtitleSentinel.IsSatisfied(media, sig));
            SubtitleSentinel.Clear(media);
            Assert.IsFalse(SubtitleSentinel.IsSatisfied(media, sig));
        }

        [TestMethod]
        public void IsSatisfied_FalseWhenConfigChanged()
        {
            SubtitleSentinel.Write(media, SubtitleSentinel.Signature("en", false, true, false));
            // The user now also wants Romanian — the stale sentinel must not satisfy the new config.
            Assert.IsFalse(SubtitleSentinel.IsSatisfied(media,
                SubtitleSentinel.Signature("en,ro", false, true, false)));
        }
    }
}
