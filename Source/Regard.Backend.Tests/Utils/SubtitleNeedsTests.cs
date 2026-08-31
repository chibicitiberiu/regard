using Microsoft.VisualStudio.TestTools.UnitTesting;
using Regard.Backend.Common.Utils;
using System;
using System.Linq;

namespace Regard.Backend.Tests.Utils
{
    /// <summary>
    /// "Does this downloaded video still need subtitles?" — the predicate that decides whether a
    /// reprocess job is worth queueing, and whether one that ran achieved anything.
    ///
    /// Getting it wrong in the lenient direction makes the automatic sweep re-fetch the same video on
    /// every pass, forever, against a budget that only allows a handful of extractions an hour.
    /// </summary>
    [TestClass]
    public class SubtitleNeedsTests
    {
        // --- ParseWanted -----------------------------------------------------------------------------

        [TestMethod]
        public void ParseWanted_splits_and_trims()
        {
            CollectionAssert.AreEqual(new[] { "en", "ro" }, SubtitleNeeds.ParseWanted(" en , ro ").ToArray());
        }

        [TestMethod]
        public void ParseWanted_drops_blanks_and_duplicates()
        {
            CollectionAssert.AreEqual(new[] { "en" }, SubtitleNeeds.ParseWanted("en,,  ,EN,en").ToArray());
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow(",,,")]
        public void ParseWanted_yields_nothing_for_junk(string csv)
        {
            Assert.AreEqual(0, SubtitleNeeds.ParseWanted(csv).Count);
        }

        // --- MissingFrom / Satisfies -----------------------------------------------------------------

        [TestMethod]
        public void Everything_present_satisfies()
        {
            Assert.IsTrue(SubtitleNeeds.Satisfies(new[] { "en", "ro" }, new[] { "en", "ro" }));
            Assert.IsTrue(SubtitleNeeds.Satisfies(new[] { "ro", "en", "de" }, new[] { "en" }));
        }

        [TestMethod]
        public void Missing_languages_are_reported_in_request_order()
        {
            CollectionAssert.AreEqual(
                new[] { "ro", "de" },
                SubtitleNeeds.MissingFrom(new[] { "en" }, new[] { "en", "ro", "de" }).ToArray());
        }

        [TestMethod]
        public void Language_matching_is_case_insensitive()
        {
            Assert.IsTrue(SubtitleNeeds.Satisfies(new[] { "EN" }, new[] { "en" }));
        }

        /// <summary>
        /// yt-dlp writes a channel's own original-language track as "en-orig". Treating that as a
        /// different language from "en" makes the sweep re-fetch the video on every single pass.
        /// </summary>
        [TestMethod]
        public void An_orig_suffixed_track_satisfies_the_plain_language()
        {
            Assert.IsTrue(SubtitleNeeds.Satisfies(new[] { "en-orig" }, new[] { "en" }));
            Assert.IsTrue(SubtitleNeeds.Satisfies(new[] { "en" }, new[] { "en-orig" }));
        }

        /// <summary>
        /// A regional variant is genuinely a different track, though — en-GB must not silently stand in
        /// for a request for en.
        /// </summary>
        [TestMethod]
        public void A_regional_variant_does_not_satisfy_the_base_language()
        {
            Assert.IsFalse(SubtitleNeeds.Satisfies(new[] { "en-GB" }, new[] { "en" }));
        }

        [TestMethod]
        public void Null_and_blank_entries_are_ignored_on_both_sides()
        {
            Assert.IsTrue(SubtitleNeeds.Satisfies(new[] { "en", null, "  " }, new[] { "en", null, "" }));
            Assert.AreEqual(0, SubtitleNeeds.MissingFrom(null, null).Count);
        }

        // --- NeedsSubtitles --------------------------------------------------------------------------

        [TestMethod]
        public void Subtitles_switched_off_means_nothing_to_fetch()
        {
            Assert.IsFalse(SubtitleNeeds.NeedsSubtitles(
                Array.Empty<string>(), "en,ro", writeSubs: false, writeAutoSubs: false, allSubs: false));
        }

        [TestMethod]
        public void A_video_with_no_subtitles_needs_them()
        {
            Assert.IsTrue(SubtitleNeeds.NeedsSubtitles(
                Array.Empty<string>(), "en,ro", writeSubs: true, writeAutoSubs: false, allSubs: false));
        }

        [TestMethod]
        public void A_partially_fetched_video_still_needs_the_rest()
        {
            // The realistic case: YouTube 429s the caption endpoint mid-run, so "en" lands and "ro" fails.
            Assert.IsTrue(SubtitleNeeds.NeedsSubtitles(
                new[] { "en" }, "en,ro", writeSubs: true, writeAutoSubs: true, allSubs: false));
        }

        [TestMethod]
        public void A_complete_video_needs_nothing()
        {
            Assert.IsFalse(SubtitleNeeds.NeedsSubtitles(
                new[] { "en", "ro" }, "en,ro", writeSubs: true, writeAutoSubs: true, allSubs: false));
        }

        /// <summary>
        /// With "all languages" there is no finite target set to compare against without asking yt-dlp
        /// what exists, so "has none at all" is the only answer available locally. Anything stricter would
        /// re-fetch forever, since YouTube offers ~200 auto-translated tracks per video.
        /// </summary>
        [TestMethod]
        public void All_languages_only_asks_whether_there_are_any_at_all()
        {
            Assert.IsTrue(SubtitleNeeds.NeedsSubtitles(
                Array.Empty<string>(), "en", writeSubs: true, writeAutoSubs: false, allSubs: true));
            Assert.IsFalse(SubtitleNeeds.NeedsSubtitles(
                new[] { "en" }, "en", writeSubs: true, writeAutoSubs: false, allSubs: true));
        }

        [TestMethod]
        public void No_configured_languages_falls_back_to_has_any()
        {
            Assert.IsTrue(SubtitleNeeds.NeedsSubtitles(
                Array.Empty<string>(), "", writeSubs: true, writeAutoSubs: false, allSubs: false));
            Assert.IsFalse(SubtitleNeeds.NeedsSubtitles(
                new[] { "de" }, null, writeSubs: true, writeAutoSubs: false, allSubs: false));
        }

        /// <summary>
        /// Auto-generated captions alone are enough to count as "on" — the option pair is independent,
        /// and a video with only machine captions is still a video with captions.
        /// </summary>
        [TestMethod]
        public void Auto_subs_alone_still_counts_as_enabled()
        {
            Assert.IsTrue(SubtitleNeeds.NeedsSubtitles(
                Array.Empty<string>(), "en", writeSubs: false, writeAutoSubs: true, allSubs: false));
        }
    }
}
