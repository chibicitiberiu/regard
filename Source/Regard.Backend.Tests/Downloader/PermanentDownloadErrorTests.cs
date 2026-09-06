using Microsoft.VisualStudio.TestTools.UnitTesting;
using Regard.Backend.Downloader;

namespace Regard.Backend.Tests.Downloader
{
    [TestClass]
    public class PermanentDownloadErrorTests
    {
        [TestMethod]
        public void MembersOnly_IsPermanent()
        {
            Assert.AreEqual("members-only", PermanentDownloadError.Match(
                "ERROR: [youtube] abc123: This video is available to this channel's members on level: Tier 1. Join this channel to get access to members-only content like this video."));
            Assert.AreEqual("members-only", PermanentDownloadError.Match(
                "ERROR: [youtube] abc: Join this channel to get access to members-only content"));
        }

        [TestMethod]
        public void Private_IsPermanent()
        {
            Assert.AreEqual("private video", PermanentDownloadError.Match(
                "ERROR: [youtube] xyz: Private video. Sign in if you've been granted access to this video"));
        }

        [TestMethod]
        public void Removed_IsPermanent()
        {
            Assert.AreEqual("removed", PermanentDownloadError.Match(
                "ERROR: [youtube] xyz: Video unavailable. This video has been removed by the uploader"));
            Assert.AreEqual("removed", PermanentDownloadError.Match(
                "ERROR: [youtube] xyz: This video is no longer available because the YouTube account associated with this video has been terminated."));
        }

        [TestMethod]
        public void TransientErrors_AreNotPermanent()
        {
            Assert.IsNull(PermanentDownloadError.Match("ERROR: unable to download video data: HTTP Error 429: Too Many Requests"));
            Assert.IsNull(PermanentDownloadError.Match("ERROR: [youtube] xyz: Video unavailable"));  // bare -> could be geo/transient
            Assert.IsNull(PermanentDownloadError.Match("ERROR: unable to download webpage: <urlopen error timed out>"));
        }

        [TestMethod]
        public void NonErrorLine_IsNotPermanent()
        {
            // A WARNING carrying the same words does not fail the download, so it must not fast-fail.
            Assert.IsNull(PermanentDownloadError.Match("WARNING: this channel's members-only content is skipped"));
            Assert.IsNull(PermanentDownloadError.Match("[download]  50.0% of 10.00MiB at 1.00MiB/s ETA 00:05"));
        }

        [TestMethod]
        public void NullOrEmpty_IsNull()
        {
            Assert.IsNull(PermanentDownloadError.Match(null));
            Assert.IsNull(PermanentDownloadError.Match(""));
        }
    }
}
