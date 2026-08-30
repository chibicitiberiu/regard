using Microsoft.VisualStudio.TestTools.UnitTesting;
using Regard.Backend.Common.Utils;

namespace Regard.Backend.Tests.Utils
{
    /// <summary>
    /// The sync-time content-scope predicates. Both run against flat-listing data, so the shapes below
    /// are the ones yt-dlp actually emits — verified against live listings of a channel's Shorts tab and
    /// of a members-only (UUMO…) playlist.
    /// </summary>
    [TestClass]
    public class VideoScopeFilterTests
    {
        // --- IsShort -------------------------------------------------------------------------------

        [TestMethod]
        [DataRow("https://www.youtube.com/shorts/5mU6SRS2Bxo")]
        [DataRow("https://youtube.com/shorts/abc123")]
        [DataRow("http://www.youtube.com/shorts/abc123")]
        [DataRow("https://www.youtube.com/SHORTS/abc123")]     // path case shouldn't matter
        [DataRow("https://www.youtube.com/shorts/abc123?feature=share")]
        [DataRow("https://www.youtube.com/shorts/abc123/")]    // trailing slash
        public void IsShort_recognizes_a_shorts_url(string url)
        {
            Assert.IsTrue(VideoScopeFilter.IsShort(url), url);
        }

        [TestMethod]
        [DataRow("https://www.youtube.com/watch?v=SuhGwaZiNIk")]
        [DataRow("https://youtu.be/SuhGwaZiNIk")]
        [DataRow("https://www.youtube.com/embed/SuhGwaZiNIk")]
        [DataRow("https://www.youtube.com/playlist?list=UUMOXuqSBlHAE6Xw-yeJA0Tunw")]
        public void IsShort_leaves_ordinary_videos_alone(string url)
        {
            Assert.IsFalse(VideoScopeFilter.IsShort(url), url);
        }

        [TestMethod]
        // The reason this parses the path instead of doing a substring search: "/shorts/" can appear in
        // the query string or fragment of a perfectly ordinary watch URL.
        [DataRow("https://www.youtube.com/watch?v=abc&next=/shorts/xyz")]
        [DataRow("https://www.youtube.com/watch?v=abc#/shorts/xyz")]
        [DataRow("https://example.com/redirect?to=https://www.youtube.com/shorts/abc")]
        public void IsShort_does_not_match_shorts_outside_the_path(string url)
        {
            Assert.IsFalse(VideoScopeFilter.IsShort(url), url);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("not a url at all")]
        [DataRow("/shorts/abc")]              // relative, so not absolute-parseable
        [DataRow("javascript:alert(1)")]      // absolute but not http(s)
        public void IsShort_is_false_and_does_not_throw_for_junk(string url)
        {
            Assert.IsFalse(VideoScopeFilter.IsShort(url));
        }

        /// <summary>
        /// The documented blind spot, pinned so a future move to duration-based detection has to
        /// confront it: a Short listed through a channel's uploads (UU…) playlist arrives as a plain
        /// watch URL and is indistinguishable from any other video.
        /// </summary>
        [TestMethod]
        public void IsShort_cannot_see_a_short_listed_as_a_watch_url()
        {
            Assert.IsFalse(VideoScopeFilter.IsShort("https://www.youtube.com/watch?v=5mU6SRS2Bxo"));
        }

        // --- IsMembersOnly -------------------------------------------------------------------------

        [TestMethod]
        [DataRow("subscriber_only")]
        [DataRow("SUBSCRIBER_ONLY")]
        public void IsMembersOnly_recognizes_subscriber_only(string availability)
        {
            Assert.IsTrue(VideoScopeFilter.IsMembersOnly(availability), availability);
        }

        [TestMethod]
        [DataRow(null)]                 // the normal case: flat entries of public videos carry no value
        [DataRow("")]
        [DataRow("public")]
        [DataRow("unlisted")]
        [DataRow("premium_only")]       // a different restriction, deliberately not lumped in
        [DataRow("needs_auth")]
        [DataRow("private")]
        public void IsMembersOnly_matches_nothing_else(string availability)
        {
            Assert.IsFalse(VideoScopeFilter.IsMembersOnly(availability));
        }

        // --- TryGetShortsTabUrl --------------------------------------------------------------------

        [TestMethod]
        [DataRow("https://www.youtube.com/@ChrisTitusTech/videos", "https://www.youtube.com/@ChrisTitusTech/shorts")]
        [DataRow("https://www.youtube.com/channel/UCabc123/videos", "https://www.youtube.com/channel/UCabc123/shorts")]
        [DataRow("https://www.youtube.com/c/SomeName/videos", "https://www.youtube.com/c/SomeName/shorts")]
        [DataRow("https://www.youtube.com/user/SomeName/videos", "https://www.youtube.com/user/SomeName/shorts")]
        [DataRow("https://youtube.com/@Handle/VIDEOS", "https://youtube.com/@Handle/shorts")]
        public void TryGetShortsTabUrl_swaps_the_videos_tab_for_the_shorts_tab(string input, string expected)
        {
            Assert.IsTrue(VideoScopeFilter.TryGetShortsTabUrl(input, out var actual), input);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("https://www.youtube.com/@Handle")]                       // not normalised to a tab
        [DataRow("https://www.youtube.com/@Handle/shorts")]                // already the shorts tab
        [DataRow("https://www.youtube.com/@Handle/streams")]
        [DataRow("https://www.youtube.com/playlist?list=UUabc")]           // a playlist, not a channel tab
        [DataRow("https://www.youtube.com/watch?v=abc")]
        [DataRow("https://example.com/@Handle/videos")]                    // not YouTube
        [DataRow("not a url")]
        public void TryGetShortsTabUrl_declines_anything_but_a_channel_videos_tab(string input)
        {
            Assert.IsFalse(VideoScopeFilter.TryGetShortsTabUrl(input, out var actual), $"{input} -> {actual}");
            Assert.IsNull(actual);
        }

        /// <summary>
        /// The channel's Shorts TAB is a listing, not a video, and IsShort must not claim otherwise —
        /// "shorts" sits at segment 1 there (@Handle/shorts) but at segment 0 in a Short's own URL
        /// (/shorts/&lt;id&gt;). The entries that tab yields are the ones IsShort is meant to match.
        /// </summary>
        [TestMethod]
        public void The_shorts_tab_url_is_a_listing_not_a_video()
        {
            Assert.IsTrue(VideoScopeFilter.TryGetShortsTabUrl("https://www.youtube.com/@Handle/videos", out var tab));
            Assert.AreEqual("https://www.youtube.com/@Handle/shorts", tab);
            Assert.IsFalse(VideoScopeFilter.IsShort(tab), "the tab itself is not a video");
            // What that tab actually returns:
            Assert.IsTrue(VideoScopeFilter.IsShort("https://www.youtube.com/shorts/5mU6SRS2Bxo"));
        }

        [TestMethod]
        public void TryGetShortsTabUrl_drops_a_query_string()
        {
            Assert.IsTrue(VideoScopeFilter.TryGetShortsTabUrl(
                "https://www.youtube.com/@Handle/videos?view=0&sort=dd", out var url));
            Assert.AreEqual("https://www.youtube.com/@Handle/shorts", url);
        }
    }
}
