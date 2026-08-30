using Microsoft.VisualStudio.TestTools.UnitTesting;
using Regard.Backend.Downloader;
using System.Text.RegularExpressions;

namespace Regard.Backend.Tests
{
    /// <summary>
    /// The yt-dlp progress line feeds three things: the video-card pie (group 1), the size-quota guard
    /// (groups 2-3), and now the speed/ETA on the notification card (groups 4-5). Widening the pattern
    /// must not disturb the first two — if the line stops matching, the pie silently freezes and the
    /// quota guard stops firing, with nothing in the logs to say so.
    ///
    /// The pattern is duplicated here rather than exposed from DownloadVideoJob, which would mean making
    /// a private field public purely for tests. If it changes there, change it here — that is the point
    /// of the test.
    /// </summary>
    [TestClass]
    public class ProgressParsingTests
    {
        private static readonly Regex ProgressRegex = new Regex(
            @"([\d\.]+)% of\s+~?\s*([\d\.]+)([KMG]i?B)" +
            @"(?:\s+at\s+(?:([\d\.]+\s*[KMG]?i?B/s)|Unknown\s*B/s))?" +
            @"(?:\s+ETA\s+(?:([\d:]+)|Unknown))?");

        private static Match M(string line) => ProgressRegex.Match(line);

        [TestMethod]
        public void ParsesAFullProgressLine()
        {
            var m = M("[download]  45.2% of ~  12.34MiB at    1.23MiB/s ETA 00:12");

            Assert.IsTrue(m.Success);
            Assert.AreEqual("45.2", m.Groups[1].Value, "percent drives the pie");
            Assert.AreEqual("12.34", m.Groups[2].Value, "size drives the quota guard");
            Assert.AreEqual("MiB", m.Groups[3].Value);
            Assert.AreEqual("1.23MiB/s", m.Groups[4].Value.Trim());
            Assert.AreEqual("00:12", m.Groups[5].Value);
        }

        [TestMethod]
        public void ParsesWithoutTheTildeEstimate()
        {
            var m = M("[download] 100.0% of 512.00MiB at 3.00MiB/s ETA 00:00");

            Assert.IsTrue(m.Success);
            Assert.AreEqual("100.0", m.Groups[1].Value);
            Assert.AreEqual("512.00", m.Groups[2].Value);
            Assert.AreEqual("MiB", m.Groups[3].Value);
        }

        [TestMethod]
        public void StillMatchesWhenSpeedAndEtaAreAbsent()
        {
            // The original three-group form. This is what the old pattern matched, and it must keep
            // working — otherwise the pie breaks on every line that lacks a speed.
            var m = M("[download]  12.5% of 100.00MiB");

            Assert.IsTrue(m.Success);
            Assert.AreEqual("12.5", m.Groups[1].Value);
            Assert.AreEqual("100.00", m.Groups[2].Value);
            Assert.IsFalse(m.Groups[4].Success, "no speed offered");
            Assert.IsFalse(m.Groups[5].Success, "no ETA offered");
        }

        [TestMethod]
        public void TreatsUnknownSpeedAndEtaAsAbsentRatherThanCapturingTheWordUnknown()
        {
            // yt-dlp prints this at the very start of a download. Capturing it naively would put
            // "at Unknown" on the user's notification card.
            var m = M("[download]   0.0% of ~ 1.00GiB at  Unknown B/s ETA Unknown");

            Assert.IsTrue(m.Success, "the line must still match so the pie starts at 0%");
            Assert.AreEqual("0.0", m.Groups[1].Value);
            Assert.AreEqual("1.00", m.Groups[2].Value);
            Assert.AreEqual("GiB", m.Groups[3].Value);
            Assert.IsFalse(m.Groups[4].Success, "'Unknown B/s' must not be captured as a speed");
            Assert.IsFalse(m.Groups[5].Success, "'Unknown' must not be captured as an ETA");
        }

        [TestMethod]
        public void ToleratesAFragmentSuffix()
        {
            var m = M("[download]  63.1% of ~ 800.00MiB at 2.00MiB/s ETA 02:31 (frag 12/38)");

            Assert.IsTrue(m.Success);
            Assert.AreEqual("63.1", m.Groups[1].Value);
            Assert.AreEqual("2.00MiB/s", m.Groups[4].Value.Trim());
            Assert.AreEqual("02:31", m.Groups[5].Value);
        }

        [TestMethod]
        public void HandlesLongEtasAndPlainByteSpeeds()
        {
            var m = M("[download]   1.0% of ~ 4.00GiB at 512.00KiB/s ETA 02:14:07");
            Assert.IsTrue(m.Success);
            Assert.AreEqual("512.00KiB/s", m.Groups[4].Value.Trim());
            Assert.AreEqual("02:14:07", m.Groups[5].Value, "hours must survive");

            var m2 = M("[download]  50.0% of 2.00MiB at 900.00B/s ETA 00:30");
            Assert.IsTrue(m2.Success);
            Assert.AreEqual("900.00B/s", m2.Groups[4].Value.Trim(), "a unit-less byte speed still parses");
        }


        // ---- the strings the user actually reads -------------------------------------------------

        [TestMethod]
        public void FormatsAllThreeFigures()
        {
            Assert.AreEqual("1.23MiB/s \u00b7 ETA 00:12 \u00b7 12.34MiB",
                DownloadVideoJob.FormatProgress("1.23MiB/s", "00:12", "12.34MiB"));
        }

        [TestMethod]
        public void OmitsWhateverYtDlpDidNotGive()
        {
            Assert.AreEqual("512.00MiB", DownloadVideoJob.FormatProgress(null, null, "512.00MiB"));
            Assert.AreEqual("2.00MiB/s \u00b7 100.00MiB", DownloadVideoJob.FormatProgress("2.00MiB/s", null, "100.00MiB"));
            Assert.AreEqual("ETA 01:30 \u00b7 1.00GiB", DownloadVideoJob.FormatProgress("", "01:30", "1.00GiB"));
        }

        [TestMethod]
        public void FallsBackToPlainDownloadingWhenNothingIsKnownYet()
        {
            // The first progress tick arrives before yt-dlp knows a speed; the card must not be blank.
            Assert.AreEqual("Downloading", DownloadVideoJob.FormatProgress(null, null, null));
            Assert.AreEqual("Downloading", DownloadVideoJob.FormatProgress("  ", "", null));
        }

        [TestMethod]
        public void CardCombinesTheVideoNameWithTheFigures()
        {
            Assert.AreEqual("Trees Are So Weird \u2014 2.00MiB/s \u00b7 ETA 00:30 \u00b7 90.00MiB",
                DownloadVideoJob.FormatCard("Trees Are So Weird",
                    DownloadVideoJob.FormatProgress("2.00MiB/s", "00:30", "90.00MiB")));
        }

        [TestMethod]
        public void CardIsJustTheNameBeforeAnyFiguresArrive()
        {
            Assert.AreEqual("Trees Are So Weird",
                DownloadVideoJob.FormatCard("Trees Are So Weird", DownloadVideoJob.FormatProgress(null, null, null)));
        }

        [TestMethod]
        public void CardSurvivesAnUnloadedVideo()
        {
            // GetOngoingNotification fires once before ExecuteJob has loaded the video.
            Assert.IsNull(DownloadVideoJob.FormatCard(null, DownloadVideoJob.FormatProgress(null, null, null)));
            Assert.AreEqual("1.00MiB/s", DownloadVideoJob.FormatCard(null, DownloadVideoJob.FormatProgress("1.00MiB/s", null, null)));
        }

        [TestMethod]
        public void DoesNotMatchNonPercentageProgressLines()
        {
            // Subtitle downloads report without a percentage; these deliberately fall through to the job
            // log instead of driving the pie.
            Assert.IsFalse(M("[download]    1.00KiB at  Unknown B/s (00:00:00)").Success);
            Assert.IsFalse(M("[download] Destination: /videos/Foo.mp4").Success);
        }
    }
}
