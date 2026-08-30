using Microsoft.VisualStudio.TestTools.UnitTesting;
using Regard.Common.Utils;
using System.Collections.Generic;
using System.Linq;

namespace Regard.Backend.Tests
{
    /// <summary>
    /// The description renderer's tokenizer. Two classes of bug matter here: turning something that is
    /// not a link into one (an e-mail address, a URL fragment) and mangling something that is (a
    /// wikipedia URL ending in a paren). Several cases below are taken verbatim from descriptions in the
    /// dev library rather than invented.
    /// </summary>
    [TestClass]
    public class DescriptionParserTests
    {
        private static List<DescBlock> Parse(string text) => DescriptionParser.Parse(text, linkifyYouTube: true);

        private static IEnumerable<DescInline> AllInlines(List<DescBlock> blocks) =>
            blocks.SelectMany(b => b.Lines).SelectMany(l => l.Inlines);

        private static DescInline SingleLink(string text)
        {
            var links = AllInlines(Parse(text)).Where(i => i.Kind == DescInlineKind.Link).ToList();
            Assert.AreEqual(1, links.Count, "expected exactly one link in: " + text);
            return links[0];
        }

        private static List<DescInline> Links(string text) =>
            AllInlines(Parse(text)).Where(i => i.Kind == DescInlineKind.Link).ToList();

        private static List<DescInline> Timestamps(string text) =>
            AllInlines(Parse(text)).Where(i => i.Kind == DescInlineKind.Timestamp).ToList();

        // ---- links -------------------------------------------------------------------------------

        [TestMethod]
        public void LinkifiesABareUrl()
        {
            var link = SingleLink("Source code: https://github.com/nanobyte-dev/nanobyte_os");
            Assert.AreEqual("https://github.com/nanobyte-dev/nanobyte_os", link.Url);
        }

        [TestMethod]
        public void KeepsAClosingParenThatBelongsToTheUrl()
        {
            // Straight out of video 447. A naive trailing-punctuation trim truncates this and the link
            // 404s, which is the kind of thing nobody notices until they click it.
            var link = SingleLink("- Memory management (wikipedia): https://en.wikipedia.org/wiki/Memory_management_(operating_systems)");
            Assert.AreEqual("https://en.wikipedia.org/wiki/Memory_management_(operating_systems)", link.Url);
        }

        [TestMethod]
        public void DropsSentencePunctuationAfterAUrl()
        {
            var link = SingleLink("See https://example.com/page.");
            Assert.AreEqual("https://example.com/page", link.Url);
        }

        [TestMethod]
        public void DropsAnUnbalancedClosingParen()
        {
            var link = SingleLink("(see https://example.com/page)");
            Assert.AreEqual("https://example.com/page", link.Url);
        }

        [TestMethod]
        public void KeepsAFragmentInsideTheUrlInsteadOfMakingItAHashtag()
        {
            // Video 454 has this exact shape. If the URL stops at the '#', the tail becomes a bogus
            // YouTube hashtag link.
            string text = "https://wiki.osdev.org/Why_do_I_need_a_Cross_Compiler%3F#Options_that_you_should_pass";
            var links = Links(text);
            Assert.AreEqual(1, links.Count, "one link, not a link plus a hashtag");
            Assert.AreEqual(text, links[0].Url);
        }

        [TestMethod]
        public void PrefixesABareWwwLink()
        {
            var link = SingleLink("go to www.example.com now");
            Assert.AreEqual("https://www.example.com", link.Url);
            Assert.AreEqual("www.example.com", link.Text, "the visible text stays as written");
        }

        // ---- handles and hashtags ----------------------------------------------------------------

        [TestMethod]
        public void DoesNotTurnAnEmailAddressIntoAChannelLink()
        {
            // Verbatim from videos 196/205/209/211. This is the single most likely false positive in the
            // whole library, and it sits in the primary test fixture.
            var links = Links("please send subtitles to subtitle@kurzgesagt.org");
            Assert.AreEqual(0, links.Count,
                "an e-mail is not a handle, got: " + string.Join(", ", links.Select(l => l.Url)));
        }

        [TestMethod]
        public void LinkifiesARealHandle()
        {
            var link = SingleLink("subscribe to @kurzgesagt for more");
            Assert.AreEqual("https://www.youtube.com/@kurzgesagt", link.Url);
        }

        [TestMethod]
        public void LinkifiesAHashtag()
        {
            var link = SingleLink("filed under #science today");
            Assert.AreEqual("https://www.youtube.com/hashtag/science", link.Url);
        }

        [TestMethod]
        public void DoesNotTreatAHeadingMarkerAsAHashtag()
        {
            var blocks = Parse("## Related Videos:");
            Assert.AreEqual(DescBlockKind.Heading, blocks.Single().Kind);
            Assert.AreEqual(0, Links("## Related Videos:").Count);
        }

        [TestMethod]
        public void DoesNotLinkifyTagsForNonYouTubeVideos()
        {
            var blocks = DescriptionParser.Parse("see @someone and #thing", linkifyYouTube: false);
            Assert.AreEqual(0, AllInlines(blocks).Count(i => i.Kind == DescInlineKind.Link));
        }

        // ---- timestamps --------------------------------------------------------------------------

        [TestMethod]
        public void ParsesMinuteAndHourTimestamps()
        {
            var stamps = Timestamps("0:00 Intro\n18:10 Buddy allocator\n1:04:38 Testing methodology");

            CollectionAssert.AreEqual(
                new[] { 0d, 18 * 60 + 10d, 3600 + 4 * 60 + 38d },
                stamps.Select(s => s.Seconds).ToArray());
        }

        [TestMethod]
        public void DoesNotFindATimestampInsideAUrl()
        {
            // The whole reason the scan is one pass instead of chained replaces.
            Assert.AreEqual(0, Timestamps("https://example.com/watch?t=12:34&x=1").Count);
        }

        [TestMethod]
        public void RejectsImpossibleTimes()
        {
            Assert.AreEqual(0, Timestamps("scores were 12:99 and 5:75").Count);
        }

        // ---- blocks ------------------------------------------------------------------------------

        [TestMethod]
        public void GroupsConsecutiveBulletsIntoOneList()
        {
            var blocks = Parse("Links:\n- Patreon: https://www.patreon.com/nanobyte\n- Discord: https://discord.gg/x\n\nafter");

            var list = blocks.Single(b => b.Kind == DescBlockKind.UnorderedList);
            Assert.AreEqual(2, list.Lines.Count);
            Assert.AreEqual(2, blocks.Count(b => b.Kind == DescBlockKind.Paragraph), "'Links:' and 'after'");
        }

        [TestMethod]
        public void ParsesOrderedLists()
        {
            var blocks = Parse("1. first\n2. second");
            Assert.AreEqual(2, blocks.Single(b => b.Kind == DescBlockKind.OrderedList).Lines.Count);
        }

        [TestMethod]
        public void ConsecutiveTextLinesShareOneParagraph()
        {
            // The old renderer wrapped every line in its own <p>, so a chapter list was double-spaced and
            // blank lines rendered as empty paragraphs.
            var blocks = Parse("0:00 Intro\n0:35 Why switch\n\nnext paragraph");

            Assert.AreEqual(2, blocks.Count);
            Assert.AreEqual(1, blocks[0].Lines[0].Inlines.Count(i => i.Kind == DescInlineKind.LineBreak));
        }

        [TestMethod]
        public void BlankLinesDoNotProduceEmptyBlocks()
        {
            Assert.AreEqual(1, Parse("\n\n\nonly line\n\n\n").Count);
        }

        [TestMethod]
        public void HeadingLevelIsCaptured()
        {
            Assert.AreEqual(2, Parse("## Chapters").Single().Level);
            Assert.AreEqual(1, Parse("# Big").Single().Level);
        }

        [TestMethod]
        public void HeadingNeedsASpaceAfterTheHashes()
        {
            Assert.AreEqual(DescBlockKind.Paragraph, Parse("#nospace").Single().Kind);
        }

        // ---- emphasis ----------------------------------------------------------------------------

        [TestMethod]
        public void ParsesBoldAndItalic()
        {
            var bold = AllInlines(Parse("this is **important** stuff")).Single(i => i.Bold);
            Assert.AreEqual("important", bold.Text);

            var italic = AllInlines(Parse("this is *aside* stuff")).Single(i => i.Italic);
            Assert.AreEqual("aside", italic.Text);
        }

        [TestMethod]
        public void SpacedAsterisksStayLiteral()
        {
            // Nothing in the library uses bold markers, so the realistic risk is false positives.
            Assert.IsFalse(AllInlines(Parse("2 * 3 * 4 = 24")).Any(i => i.Italic || i.Bold));
        }

        [TestMethod]
        public void UnderscoresInsideWordsAreNotItalics()
        {
            Assert.IsFalse(AllInlines(Parse("the file is nanobyte_os_kernel here")).Any(i => i.Italic));
        }

        // ---- safety ------------------------------------------------------------------------------

        [TestMethod]
        public void HtmlInADescriptionStaysText()
        {
            // The renderer emits elements from these nodes and never a MarkupString, so anything that
            // arrives as Text is escaped by Blazor. Assert it is classified as text and not as markup or
            // a link.
            string hostile = "<img src=x onerror=alert(1)> and <script>alert(2)</script>";
            var blocks = Parse(hostile);

            Assert.IsTrue(AllInlines(blocks).All(i => i.Kind == DescInlineKind.Text),
                "no node type other than plain text");
            StringAssert.Contains(string.Concat(AllInlines(blocks).Select(i => i.Text)), "<script>");
        }

        [TestMethod]
        public void JavascriptUrlsAreNeverLinkified()
        {
            // The invariant that keeps <a href="@url"> safe: only http/https/www ever become links.
            Assert.AreEqual(0, Links("javascript:alert(1)").Count);
            Assert.AreEqual(0, Links("data:text/html;base64,PHNjcmlwdD4=").Count);
        }

        [TestMethod]
        public void HandlesNullAndEmpty()
        {
            Assert.AreEqual(0, DescriptionParser.Parse(null, true).Count);
            Assert.AreEqual(0, DescriptionParser.Parse("   ", true).Count);
        }

        [TestMethod]
        public void RealDescriptionParsesEndToEnd()
        {
            // Video 447's description, trimmed. Exercises list + links + paren URL + both timestamp forms
            // in one go.
            string description = string.Join("\n", new[]
            {
                "How to manage your memory.",
                "",
                "Links:",
                "- Patreon: https://www.patreon.com/nanobyte",
                "- Source code: https://github.com/nanobyte-dev/physical-allocators",
                "",
                "Documentation:",
                "- Memory management (wikipedia): https://en.wikipedia.org/wiki/Memory_management_(operating_systems)",
                "",
                "Chapters:",
                "0:00 Linked list allocator",
                "18:10 Buddy allocator",
                "1:04:38 Testing methodology",
            });

            var blocks = Parse(description);

            Assert.AreEqual(2, blocks.Count(b => b.Kind == DescBlockKind.UnorderedList), "two separate lists");
            Assert.AreEqual(3, Timestamps(description).Count);
            Assert.IsTrue(Links(description).Any(l => l.Url.EndsWith("(operating_systems)")),
                "the wikipedia link keeps its paren");
            Assert.AreEqual(0, Links(description).Count(l => l.Url.Contains("youtube.com/@")),
                "nothing here is a handle");
        }
    }
}
