using Microsoft.VisualStudio.TestTools.UnitTesting;
using Regard.Backend.Services;
using System;

namespace Regard.Backend.Tests
{
    /// <summary>
    /// Deferred subscription creation inserts the row before anything is known about it, but
    /// Subscription.Name is [Required] — so it needs a stand-in. The name is also the tree's sort key,
    /// so a good placeholder lands near the eventual alphabetical position instead of parking every new
    /// subscription under "h" for "https://…" and then jumping when the real name arrives.
    /// </summary>
    [TestClass]
    public class PlaceholderNameTests
    {
        private static string Name(string url) => SubscriptionManager.PlaceholderName(new Uri(url));

        [TestMethod]
        public void PrefersTheChannelHandle()
        {
            Assert.AreEqual("Computerphile", Name("https://www.youtube.com/@Computerphile"));
            Assert.AreEqual("Computerphile", Name("https://www.youtube.com/@Computerphile/videos"));
            Assert.AreEqual("CGPGrey", Name("https://www.youtube.com/@CGPGrey/videos"));
        }

        [TestMethod]
        public void IgnoresTheVideosSuffixTheNormalizerAppends()
        {
            // FixYouTubeChannelUri rewrites channel URLs to end in /videos, which is never a useful name.
            Assert.AreEqual("UCxxxxxxxxxxxxxxxxxxxxxx", Name("https://www.youtube.com/channel/UCxxxxxxxxxxxxxxxxxxxxxx/videos"));
            Assert.AreEqual("SomeUser", Name("https://www.youtube.com/user/SomeUser/videos"));
        }

        [TestMethod]
        public void FallsBackToTheLastPathSegment()
        {
            Assert.AreEqual("feed.xml", Name("https://example.com/blog/feed.xml"));
            Assert.AreEqual("rss", Name("https://example.com/rss"));
        }

        [TestMethod]
        public void FallsBackToTheHostWhenThereIsNoPath()
        {
            Assert.AreEqual("example.com", Name("https://example.com"));
            Assert.AreEqual("example.com", Name("https://example.com/"));
        }

        [TestMethod]
        public void DecodesEscapedSegments()
        {
            Assert.AreEqual("Some Channel", Name("https://example.com/Some%20Channel"));
        }

        [TestMethod]
        public void NeverReturnsEmpty()
        {
            // Subscription.Name is [Required]; an empty placeholder would fail the insert outright.
            foreach (var url in new[]
                     {
                         "https://www.youtube.com/@x",
                         "https://example.com",
                         "https://example.com/",
                         "https://example.com/a/b/c/",
                         "https://www.youtube.com/videos",
                     })
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(Name(url)), $"empty placeholder for {url}");
            }
        }
    }
}
