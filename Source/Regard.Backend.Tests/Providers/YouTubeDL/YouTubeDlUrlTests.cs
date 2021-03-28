using Microsoft.VisualStudio.TestTools.UnitTesting;
using Regard.Backend.Providers.YouTubeDL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Regard.Backend.Tests.Providers.YouTubeDL
{
    [TestClass]
    public class YouTubeDlUrlTests
    {
        [TestMethod]
        public void FixYouTubeChannelUriTest()
        {
            Uri uri;

            // youtube stuff that should be untouched
            uri = new("https://youtube.com");
            Assert.AreEqual(uri, YouTubeUrlHelper.FixYouTubeChannelUri(uri));
            
            uri = new("https://www.youtube.com/watch?v=V3zPwkrNK-Q");
            Assert.AreEqual(uri, YouTubeUrlHelper.FixYouTubeChannelUri(uri));
            
            uri = new("https://www.youtube.com/watch?v=YCUrphpzEqY&list=PL8mG-RkN2uTxLsQhOyM5TBgMHF9V4Gfqa");
            Assert.AreEqual(uri, YouTubeUrlHelper.FixYouTubeChannelUri(uri));

            uri = new("https://www.youtube.com/playlist?list=PL8mG-RkN2uTxLsQhOyM5TBgMHF9V4Gfqa");
            Assert.AreEqual(uri, YouTubeUrlHelper.FixYouTubeChannelUri(uri));

            uri = new("http://www.youtube.com/v/-wtIMTCHWuI?version=3&autohide=1");
            Assert.AreEqual(uri, YouTubeUrlHelper.FixYouTubeChannelUri(uri));

            uri = new("https://www.youtube.com/embed/M7lc1UVf-VE");
            Assert.AreEqual(uri, YouTubeUrlHelper.FixYouTubeChannelUri(uri));

            uri = new("https://youtube.com/embed/M7lc1UVf-VE");
            Assert.AreEqual(uri, YouTubeUrlHelper.FixYouTubeChannelUri(uri));

            uri = new("https://www.youtube.com/results?search_query=test");
            Assert.AreEqual(uri, YouTubeUrlHelper.FixYouTubeChannelUri(uri));

            uri = new("https://www.youtube.com/feeds/videos.xml?channel_id=UC0QHWhjbe5fGJEPz3sVb6nw");
            Assert.AreEqual(uri, YouTubeUrlHelper.FixYouTubeChannelUri(uri));

            uri = new("https://www.youtube.com/feeds/videos.xml?playlist_id=PLQMVnqe4XbictUtFZK1-gBYvyUzTWJnOk");
            Assert.AreEqual(uri, YouTubeUrlHelper.FixYouTubeChannelUri(uri));

            uri = new("http://youtu.be/-wtIMTCHWuI");
            Assert.AreEqual(uri, YouTubeUrlHelper.FixYouTubeChannelUri(uri));

            uri = new("https://youtube.googleapis.com/v/My2FRPA3Gf8");
            Assert.AreEqual(uri, YouTubeUrlHelper.FixYouTubeChannelUri(uri));

            // same but http, or without www.
            uri = new("http://youtube.com");
            Assert.AreEqual(uri, YouTubeUrlHelper.FixYouTubeChannelUri(uri));

            uri = new("http://www.youtube.com/watch?v=V3zPwkrNK-Q");
            Assert.AreEqual(uri, YouTubeUrlHelper.FixYouTubeChannelUri(uri));

            uri = new("http://youtube.com/watch?v=YCUrphpzEqY&list=PL8mG-RkN2uTxLsQhOyM5TBgMHF9V4Gfqa");
            Assert.AreEqual(uri, YouTubeUrlHelper.FixYouTubeChannelUri(uri));

            uri = new("http://www.youtube.com/playlist?list=PL8mG-RkN2uTxLsQhOyM5TBgMHF9V4Gfqa");
            Assert.AreEqual(uri, YouTubeUrlHelper.FixYouTubeChannelUri(uri));

            // url will be extracted from these
            Assert.AreEqual(new("http://www.youtube.com/watch?v=-wtIMTCHWuI"),
                YouTubeUrlHelper.FixYouTubeChannelUri(
                    new("http://www.youtube.com/oembed?url=http%3A//www.youtube.com/watch?v%3D-wtIMTCHWuI&format=json")));

            Assert.AreEqual(new("https://youtube.com/watch?v=EhxJLojIE_o&feature=share"), 
                YouTubeUrlHelper.FixYouTubeChannelUri(
                    new("http://www.youtube.com/attribution_link?a=JdfC0C9V6ZI&u=%2Fwatch%3Fv%3DEhxJLojIE_o%26feature%3Dshare")));

            // fixed url
            Assert.AreEqual(new("https://www.youtube.com/channel/UC0QHWhjbe5fGJEPz3sVb6nw/videos"),
                YouTubeUrlHelper.FixYouTubeChannelUri(
                    new("https://www.youtube.com/channel/UC0QHWhjbe5fGJEPz3sVb6nw")));

            Assert.AreEqual(new("https://www.youtube.com/channel/UC0QHWhjbe5fGJEPz3sVb6nw/videos"),
                YouTubeUrlHelper.FixYouTubeChannelUri(
                    new("https://www.youtube.com/channel/UC0QHWhjbe5fGJEPz3sVb6nw/featured")));

            Assert.AreEqual(new("https://www.youtube.com/channel/UC0QHWhjbe5fGJEPz3sVb6nw/videos"),
                YouTubeUrlHelper.FixYouTubeChannelUri(
                    new("https://www.youtube.com/channel/UC0QHWhjbe5fGJEPz3sVb6nw/some/other/bs")));

            Assert.AreEqual(new("https://youtube.com/c/LinusTechTips/videos"),
                YouTubeUrlHelper.FixYouTubeChannelUri(
                    new("https://youtube.com/c/LinusTechTips")));

            Assert.AreEqual(new("https://YOUTUBE.com/c/LinusTechTips/videos"),
                YouTubeUrlHelper.FixYouTubeChannelUri(
                    new("https://YOUTUBE.com/c/LinusTechTips/featured")));

            Assert.AreEqual(new("https://www.youtube.com/user/LinusTechTips/videos"),
                YouTubeUrlHelper.FixYouTubeChannelUri(
                    new("https://www.youtube.com/user/LinusTechTips")));

            Assert.AreEqual(new("https://www.youtube.com/user/LinusTechTips/videos"),
                YouTubeUrlHelper.FixYouTubeChannelUri(
                    new("http://www.youtube.com/oembed?url=https%3A//www.youtube.com/user/LinusTechTips&format=json")));

            // sites that are not youtube are unchanged
            uri = new("https://www.yt.com/channel/UC0QHWhjbe5fGJEPz3sVb6nw");
            Assert.AreEqual(uri, YouTubeUrlHelper.FixYouTubeChannelUri(uri));

            uri = new("http://example.com/channel/UC0QHWhjbe5fGJEPz3sVb6nw/featured");
            Assert.AreEqual(uri, YouTubeUrlHelper.FixYouTubeChannelUri(uri));

            uri = new("http://mytube.com/channel/UC0QHWhjbe5fGJEPz3sVb6nw/xoxoxoxoxo");
            Assert.AreEqual(uri, YouTubeUrlHelper.FixYouTubeChannelUri(uri));

            uri = new("https://www.youuutube.com/channel/UC0QHWhjbe5fGJEPz3sVb6nw/featured");
            Assert.AreEqual(uri, YouTubeUrlHelper.FixYouTubeChannelUri(uri));

            uri = new("https://yyoutube.com/channel/UC0QHWhjbe5fGJEPz3sVb6nw/featured");
            Assert.AreEqual(uri, YouTubeUrlHelper.FixYouTubeChannelUri(uri));

            uri = new("https://www.yyyoutube.com/channel/UC0QHWhjbe5fGJEPz3sVb6nw/some/other/bs");
            Assert.AreEqual(uri, YouTubeUrlHelper.FixYouTubeChannelUri(uri));

            uri = new("https://youtubeyyy.com/c/LinusTechTips");
            Assert.AreEqual(uri, YouTubeUrlHelper.FixYouTubeChannelUri(uri));

            uri = new("https://youtuber.com/c/LinusTechTips/featured");
            Assert.AreEqual(uri, YouTubeUrlHelper.FixYouTubeChannelUri(uri));

            uri = new("https://www.ryoutube.com/user/LinusTechTips");
            Assert.AreEqual(uri, YouTubeUrlHelper.FixYouTubeChannelUri(uri));

            uri = new("http://ayoutube.com/oembed?url=https%3A//www.youtube.com/user/LinusTechTips&format=json");
            Assert.AreEqual(uri, YouTubeUrlHelper.FixYouTubeChannelUri(uri));

            uri = new("http://youtuber.com/oembed?url=http%3A//www.youtube.com/watch?v%3D-wtIMTCHWuI&format=json");
            Assert.AreEqual(uri, YouTubeUrlHelper.FixYouTubeChannelUri(uri));

            uri = new("https://www.youtube.fr/attribution_link?a=JdfC0C9V6ZI&u=%2Fwatch%3Fv%3DEhxJLojIE_o%26feature%3Dshare");
            Assert.AreEqual(uri, YouTubeUrlHelper.FixYouTubeChannelUri(uri));
        }
    }
}
