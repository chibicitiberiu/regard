using Microsoft.VisualStudio.TestTools.UnitTesting;
using Regard.Backend.Jobs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Regard.Backend.Tests.Jobs
{
    /// <summary>
    /// The sidecar-only yt-dlp argument list.
    ///
    /// What must NOT be here matters more than what is. --skip-download does not stop yt-dlp resolving
    /// formats, so a restrictive -f selector still fails the whole run even though no media is wanted;
    /// remux/recode/merge are post-processors for a file that will never exist; and SponsorBlock's Remove
    /// re-times cues against a cut it cannot perform here. The obvious "simplification" someone will
    /// reach for later is to route this through DownloadVideoJob.ResolveDownloadOptions — these
    /// assertions are what stops that going unnoticed.
    /// </summary>
    [TestClass]
    public class ReprocessArgsTests
    {
        private const string Path = "/videos/CGP Grey/S2023E135 - Why Most People Can No Longer Comment";
        private const string Url = "https://www.youtube.com/watch?v=BHMF-FlkqPw";

        private static List<string> Build(
            IEnumerable<string> antibot = null,
            IEnumerable<string> sleep = null,
            IEnumerable<string> subtitles = null,
            string retries = null,
            string outputPath = Path,
            string url = Url)
            => ReprocessVideoJob.ComposeArgs(antibot, sleep, subtitles, retries, outputPath, url).ToList();

        [TestMethod]
        public void The_media_download_is_skipped()
        {
            CollectionAssert.Contains(Build(), "--skip-download");
        }

        /// <summary>
        /// Load-bearing. Without it, one language failing (YouTube 429s the caption endpoint readily)
        /// raises inside yt-dlp's _write_subtitles, and process_info returns before writing the
        /// info-json — losing both the metadata and the languages that had already succeeded.
        /// </summary>
        [TestMethod]
        public void Errors_are_ignored_so_one_bad_language_does_not_lose_the_rest()
        {
            CollectionAssert.Contains(Build(), "--ignore-errors");
        }

        [TestMethod]
        [DataRow("-f")]
        [DataRow("--format")]
        [DataRow("--prefer-free-formats")]
        [DataRow("--merge-output-format")]
        [DataRow("--remux-video")]
        [DataRow("--recode-video")]
        [DataRow("-r")]
        [DataRow("--limit-rate")]
        public void No_media_arguments_are_emitted(string forbidden)
        {
            CollectionAssert.DoesNotContain(Build(), forbidden);
        }

        [TestMethod]
        public void No_sponsorblock_arguments_are_emitted()
        {
            Assert.IsFalse(Build().Any(a => a.StartsWith("--sponsorblock", StringComparison.OrdinalIgnoreCase)),
                "SponsorBlock Remove re-times cues to a cut that cannot happen with --skip-download");
            CollectionAssert.DoesNotContain(Build(), "--convert-subs");
        }

        /// <summary>
        /// The output path must be the recorded DownloadedPath verbatim. Re-resolving it from the
        /// download-path template would drift if the subscription was renamed, and the sidecars would
        /// land next to a file that isn't there — invisible to subtitle discovery, which keys off
        /// DownloadedPath.
        /// </summary>
        [TestMethod]
        public void The_output_path_is_passed_through_verbatim()
        {
            var args = Build();
            int i = args.IndexOf("-o");
            Assert.IsTrue(i >= 0, "-o missing");
            Assert.AreEqual(Path, args[i + 1]);
        }

        [TestMethod]
        public void The_info_json_is_always_requested()
        {
            // It's how one extraction yields both subtitles and fresh metadata; the job deletes it
            // afterwards unless the subscription wants it kept.
            CollectionAssert.Contains(Build(), "--write-info-json");
        }

        [TestMethod]
        public void The_url_comes_last()
        {
            Assert.AreEqual(Url, Build().Last());
        }

        [TestMethod]
        public void Supplied_fragments_are_passed_through_in_order()
        {
            var args = Build(
                antibot: new[] { "--cookies", "/data/cookies.txt" },
                sleep: new[] { "--sleep-interval", "5" },
                subtitles: new[] { "--write-subs", "--sub-langs", "en,ro", "--sub-format", "vtt/srt/best" },
                retries: "3");

            CollectionAssert.Contains(args, "--cookies");
            CollectionAssert.Contains(args, "--sleep-interval");
            CollectionAssert.Contains(args, "--write-subs");
            CollectionAssert.Contains(args, "vtt/srt/best");

            int r = args.IndexOf("-R");
            Assert.IsTrue(r >= 0);
            Assert.AreEqual("3", args[r + 1]);
        }

        [TestMethod]
        public void Null_fragments_are_tolerated()
        {
            var args = Build(antibot: null, sleep: null, subtitles: null, retries: null);
            CollectionAssert.DoesNotContain(args, "-R");
            CollectionAssert.Contains(args, "--skip-download");
            Assert.AreEqual(Url, args.Last());
        }

        [TestMethod]
        public void Progress_is_line_buffered()
        {
            // The job's stdout handler reads one line at a time.
            CollectionAssert.Contains(Build(), "--newline");
        }
    }
}
