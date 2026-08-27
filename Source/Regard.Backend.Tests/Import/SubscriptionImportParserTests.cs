using Microsoft.VisualStudio.TestTools.UnitTesting;
using Regard.Backend.Common.Utils;
using System.Linq;

namespace Regard.Backend.Tests.Import
{
    [TestClass]
    public class SubscriptionImportParserTests
    {
        [TestMethod]
        public void Opml_NestedFolder_MirrorsStructure_AndConvertsYouTubeFeeds()
        {
            const string opml = @"<?xml version=""1.0""?>
<opml version=""1.1"">
  <head><title>subscriptions</title></head>
  <body>
    <outline text=""Tech"" title=""Tech"">
      <outline text=""CGP Grey"" title=""CGP Grey"" type=""rss""
               xmlUrl=""https://www.youtube.com/feeds/videos.xml?channel_id=UC2C_jShtL725hvbm1arSV9w"" />
      <outline text=""Kurzgesagt"" title=""Kurzgesagt"" type=""rss""
               xmlUrl=""https://www.youtube.com/feeds/videos.xml?channel_id=UCsXVk37bltHxD1rDPwtNM8Q"" />
    </outline>
    <outline text=""Vsauce"" title=""Vsauce"" type=""rss""
             xmlUrl=""https://www.youtube.com/feeds/videos.xml?channel_id=UC6nSFpj9HTCZ5t-N3Rm3-HA"" />
  </body>
</opml>";

            var root = SubscriptionImportParser.Parse(opml);

            Assert.AreEqual(3, SubscriptionImportParser.CountFeeds(root));

            // Root has one folder ("Tech") plus one loose feed ("Vsauce").
            var folder = root.Children.Single(c => c.IsFolder);
            Assert.AreEqual("Tech", folder.Title);
            Assert.AreEqual(2, folder.Children.Count);

            // YouTube feed URLs are rewritten to channel URLs.
            Assert.AreEqual("https://www.youtube.com/channel/UC2C_jShtL725hvbm1arSV9w",
                folder.Children[0].Url);
            Assert.AreEqual("https://www.youtube.com/channel/UCsXVk37bltHxD1rDPwtNM8Q",
                folder.Children[1].Url);

            var loose = root.Children.Single(c => !c.IsFolder);
            Assert.AreEqual("https://www.youtube.com/channel/UC6nSFpj9HTCZ5t-N3Rm3-HA", loose.Url);
        }

        [TestMethod]
        public void Opml_PrefersHtmlUrl_OverXmlUrl()
        {
            const string opml = @"<opml><body>
    <outline text=""Chan"" htmlUrl=""https://www.youtube.com/@somehandle""
             xmlUrl=""https://www.youtube.com/feeds/videos.xml?channel_id=UCxxxx"" />
</body></opml>";

            var root = SubscriptionImportParser.Parse(opml);
            Assert.AreEqual(1, SubscriptionImportParser.CountFeeds(root));
            Assert.AreEqual("https://www.youtube.com/@somehandle", root.Children.Single().Url);
        }

        [TestMethod]
        public void UrlList_ParsesToFlatLeaves_IgnoringBlanksAndComments()
        {
            const string list = "https://www.youtube.com/@a\n\n  # a comment\nhttps://vimeo.com/user123\nnot a url\n";

            var root = SubscriptionImportParser.Parse(list);

            Assert.AreEqual(2, SubscriptionImportParser.CountFeeds(root));
            Assert.IsTrue(root.Children.All(c => !c.IsFolder));
            Assert.AreEqual("https://www.youtube.com/@a", root.Children[0].Url);
            Assert.AreEqual("https://vimeo.com/user123", root.Children[1].Url);
        }

        [TestMethod]
        public void EmptyOrJunk_YieldsNoFeeds()
        {
            Assert.AreEqual(0, SubscriptionImportParser.CountFeeds(SubscriptionImportParser.Parse("")));
            Assert.AreEqual(0, SubscriptionImportParser.CountFeeds(SubscriptionImportParser.Parse("   ")));
            Assert.AreEqual(0, SubscriptionImportParser.CountFeeds(SubscriptionImportParser.Parse("<opml><body></body></opml>")));
            // A folder with no feeds is pruned away.
            Assert.AreEqual(0, SubscriptionImportParser.CountFeeds(
                SubscriptionImportParser.Parse("<opml><body><outline text='Empty'></outline></body></opml>")));
        }
    }
}
