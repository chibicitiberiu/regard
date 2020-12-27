using Microsoft.VisualStudio.TestTools.UnitTesting;
using Regard.Backend.Providers.Rss;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Regard.Backend.Tests.Providers.Rss
{
    [TestClass]
    public class RedditLinkProcessorTest
    {
        [TestMethod]
        public async Task ValidUrlTest()
        {
            var processor = new RedditLinkProcessor();
            Uri original, actual, expected;

            // Valid URL - old.reddit.com
            original = new Uri("https://old.reddit.com/r/videos/comments/88ll08/this_is_what_happens_when_one_company_owns_dozens/");
            expected = new Uri("https://youtu.be/hWLjYJ4BzvI");
            actual = await processor.ProcessLink(original);
            Assert.AreEqual(expected, actual);

            // Valid URL - www.reddit.com
            original = new Uri("https://www.reddit.com/r/videos/comments/88ll08/this_is_what_happens_when_one_company_owns_dozens/");
            expected = new Uri("https://youtu.be/hWLjYJ4BzvI");
            actual = await processor.ProcessLink(original);
            Assert.AreEqual(expected, actual);

            // Valid URL
            original = new Uri("https://www.reddit.com/r/videos/comments/88ll08/_/");
            expected = new Uri("https://youtu.be/hWLjYJ4BzvI");
            actual = await processor.ProcessLink(original);
            Assert.AreEqual(expected, actual);

            // Valid URL - reddit.com
            original = new Uri("https://reddit.com/r/videos/comments/88ll08/this_is_what_happens_when_one_company_owns_dozens/");
            expected = new Uri("https://youtu.be/hWLjYJ4BzvI");
            actual = await processor.ProcessLink(original);
            Assert.AreEqual(expected, actual);

            // Valid URL - shortened redd.it
            original = new Uri("https://redd.it/88ll08/");
            expected = new Uri("https://youtu.be/hWLjYJ4BzvI");
            actual = await processor.ProcessLink(original);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public async Task InvalidUrlTest()
        {
            var processor = new RedditLinkProcessor();
            Uri original, actual, expected;

            // Valid URL, but a text post, shouldn't have URL
            original = new Uri("https://www.reddit.com/r/AskReddit/comments/99eh6b/without_saying_what_the_category_is_what_are_your/");
            expected = original;
            actual = await processor.ProcessLink(original);
            Assert.AreEqual(expected, actual);

            // Invalid URL - old.reddit.com
            original = new Uri("https://old.reddit.com/r/videos/comments/88ll08aaaaa/this_is_what_happens_when_one_company_owns_dozens/");
            expected = original;
            actual = await processor.ProcessLink(original);
            Assert.AreEqual(expected, actual);

            // Invalid URL - www.reddit.com
            original = new Uri("https://www.reddit.com/r/videos/comments/88ll08aaaaaaaaa/this_is_what_happens_when_one_company_owns_dozens/");
            expected = original;
            actual = await processor.ProcessLink(original);
            Assert.AreEqual(expected, actual);

            // Invalid URL
            original = new Uri("https://www.reddit.com/r/videos/comments/88ll08aaaaaaaaaa/_/");
            expected = original;
            actual = await processor.ProcessLink(original);
            Assert.AreEqual(expected, actual);

            // Invalid URL - reddit.com
            original = new Uri("https://reddit.com/r/videos/comments/88ll08aaaaaaaa/this_is_what_happens_when_one_company_owns_dozens/");
            expected = original;
            actual = await processor.ProcessLink(original);
            Assert.AreEqual(expected, actual);

            // Invalid URL - shortened redd.it
            original = new Uri("https://redd.it/88ll08aaaaaaaa/");
            expected = original;
            actual = await processor.ProcessLink(original);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public async Task NotRedditTest()
        {
            var processor = new RedditLinkProcessor();
            Uri original, actual, expected;

            original = new Uri("https://youtu.be/hWLjYJ4BzvI");
            expected = original;
            actual = await processor.ProcessLink(original);
            Assert.AreEqual(expected, actual);

            original = new Uri("https://google.com/r/aaaa/comments/asdasdasd");
            expected = original;
            actual = await processor.ProcessLink(original);
            Assert.AreEqual(expected, actual);

            original = new Uri("https://reddit.example/r/aaaa/comments/asdasdasd");
            expected = original;
            actual = await processor.ProcessLink(original);
            Assert.AreEqual(expected, actual);
        }
    }
}
