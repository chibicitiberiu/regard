using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Regard.Backend.Model;
using Regard.Backend.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Tests
{
    /// <summary>
    /// Which sidecar files become selectable subtitle tracks, what language they claim, and whether they
    /// are machine-generated. Getting the first part wrong exposes a .part file to the player; getting
    /// the last part wrong mislabels every track in the picker.
    /// </summary>
    [TestClass]
    public class SubtitleDiscoveryTests
    {
        // Real headers. The auto sample is YouTube ASR (per-word timing tags); the human one is a
        // hand-written track. Both were taken from files in the dev library.
        private const string AutoVtt =
            "WEBVTT\nKind: captions\nLanguage: en\n\n" +
            "00:00:00.240 --> 00:00:01.990 align:start position:0%\n \n" +
            "While<00:00:00.560><c> waiting</c><00:00:00.800><c> on</c><00:00:01.040><c> a</c>\n\n";

        private const string HumanVtt =
            "WEBVTT\nKind: captions\nLanguage: en\n\n" +
            "00:00:00.400 --> 00:00:03.600\nTaking care of your health can feel overwhelming.\n\n";

        private string root;
        private VideoStorageService storage;

        [TestInitialize]
        public void Setup()
        {
            root = Path.Combine(Path.GetTempPath(), "regard-subtitle-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            storage = new VideoStorageService(NullLogger<VideoStorageService>.Instance, StorageManagerFor(root));
        }

        [TestCleanup]
        public void Cleanup()
        {
            try { Directory.Delete(root, recursive: true); } catch { /* best effort */ }
        }

        private void Write(string relativePath, string content)
        {
            var full = Path.Combine(root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, content);
        }

        [TestMethod]
        public async Task PicksOutSubtitlesAndIgnoresEverythingElse()
        {
            Write("Sub/S1E1 - Foo.mp4", "video");
            Write("Sub/S1E1 - Foo.en.vtt", HumanVtt);
            Write("Sub/S1E1 - Foo.ro.vtt", AutoVtt);
            Write("Sub/S1E1 - Foo.info.json", "{}");
            Write("Sub/S1E1 - Foo.f315.webm.part", "partial");
            Write("Sub/S1E1 - Foo.nfo", "<nfo/>");
            Write("Sub/S1E1 - Foo-thumb.jpg", "jpeg");
            Write("Sub/S1E1 - Foo.en.json3", "{}");     // yt-dlp "best" can emit this; unusable in a track

            var tracks = await storage.GetSubtitleFiles(new Video { DownloadedPath = "Sub/S1E1 - Foo" });

            CollectionAssert.AreEquivalent(
                new[] { "en", "ro" },
                tracks.Select(t => t.Lang).ToArray(),
                "only the vtt/srt sidecars are tracks: " + string.Join(", ", tracks.Select(t => t.Lang)));
        }

        [TestMethod]
        public async Task DoesNotClaimASiblingWithALongerName()
        {
            // Same hazard as the file-ownership tests: "Foo" must not pick up "Foo (Part 2)"'s subtitles.
            Write("Sub/S1E1 - Foo.en.vtt", HumanVtt);
            Write("Sub/S1E1 - Foo (Part 2).de.vtt", HumanVtt);

            var tracks = await storage.GetSubtitleFiles(new Video { DownloadedPath = "Sub/S1E1 - Foo" });

            Assert.AreEqual(1, tracks.Count);
            Assert.AreEqual("en", tracks[0].Lang);
        }

        [TestMethod]
        public async Task HandlesATitleContainingASlash()
        {
            // A video title with a slash makes yt-dlp create a directory, so the output prefix is the
            // segment after it. This exists in the real library ("... part 2/2.en.vtt").
            Write("nanobyte/S2023E0 - Memory management - part 2/2.en.vtt", AutoVtt);
            Write("nanobyte/S2023E0 - Memory management - part 2/2.mp4", "video");

            var tracks = await storage.GetSubtitleFiles(
                new Video { DownloadedPath = "nanobyte/S2023E0 - Memory management - part 2/2" });

            Assert.AreEqual(1, tracks.Count, "the prefix is the part after the slash");
            Assert.AreEqual("en", tracks[0].Lang);
        }

        [TestMethod]
        public async Task WorksWithAnAbsoluteDownloadedPath()
        {
            // DownloadedPath is stored absolute in practice (the path template starts with
            // {DownloadDirectory}), so Path.Combine returns it unchanged. Cover the shipped shape, not
            // just the relative one the other tests use.
            Write("Sub/S1E2 - Abs.en.vtt", HumanVtt);

            var absolute = Path.Combine(root, "Sub/S1E2 - Abs");
            var tracks = await storage.GetSubtitleFiles(new Video { DownloadedPath = absolute });

            Assert.AreEqual(1, tracks.Count);
            Assert.AreEqual("en", tracks[0].Lang);
        }

        [TestMethod]
        public async Task TellsMachineGeneratedCuesFromHumanOnes()
        {
            Write("Sub/S1E1 - Foo.en.vtt", HumanVtt);
            Write("Sub/S1E1 - Foo.ro.vtt", AutoVtt);

            var tracks = await storage.GetSubtitleFiles(new Video { DownloadedPath = "Sub/S1E1 - Foo" });

            Assert.IsFalse(tracks.Single(t => t.Lang == "en").AutoGenerated, "hand-written track");
            Assert.IsTrue(tracks.Single(t => t.Lang == "ro").AutoGenerated, "ASR track");
        }

        [TestMethod]
        public async Task SrtIsNeverReportedAsAutoGenerated()
        {
            // yt-dlp's --convert-subs srt strips the per-word timing tags, so the evidence is gone. Say
            // nothing rather than guess.
            Write("Sub/S1E1 - Foo.en.srt", "1\n00:00:00,400 --> 00:00:03,600\nHello\n\n");

            var tracks = await storage.GetSubtitleFiles(new Video { DownloadedPath = "Sub/S1E1 - Foo" });

            Assert.AreEqual("srt", tracks.Single().Format);
            Assert.IsFalse(tracks.Single().AutoGenerated);
        }

        [TestMethod]
        public async Task AcceptsRegionAndScriptTags()
        {
            Write("Sub/S1E1 - Foo.en-US.vtt", HumanVtt);
            Write("Sub/S1E1 - Foo.zh-Hans.vtt", HumanVtt);
            Write("Sub/S1E1 - Foo.en-orig.vtt", HumanVtt);

            var tracks = await storage.GetSubtitleFiles(new Video { DownloadedPath = "Sub/S1E1 - Foo" });

            CollectionAssert.AreEquivalent(
                new[] { "en-US", "zh-Hans", "en-orig" }, tracks.Select(t => t.Lang).ToArray());
        }

        [TestMethod]
        public async Task NoDownloadedPathMeansNoTracks()
        {
            Write("Sub/S1E1 - Foo.en.vtt", HumanVtt);

            var tracks = await storage.GetSubtitleFiles(new Video { DownloadedPath = null });

            Assert.AreEqual(0, tracks.Count);
        }

        [TestMethod]
        public void LabelsAreHumanReadable()
        {
            Assert.AreEqual("English", SubtitleFile.LabelFor("en", false));
            Assert.AreEqual("Romanian (auto-generated)", SubtitleFile.LabelFor("ro", true));
            Assert.AreEqual("Chinese (Simplified)", SubtitleFile.LabelFor("zh-Hans", false));
        }

        [TestMethod]
        public void OrigSuffixIsPeeledOffRatherThanFedToCultureInfo()
        {
            // CultureInfo does NOT throw on "en-orig" — ICU parses "orig" as a script subtag and returns
            // "English (Orig)". Relying on a catch here would ship that string to the picker.
            Assert.AreEqual("English (original)", SubtitleFile.LabelFor("en-orig", false));
        }

        [TestMethod]
        public void AnUnnameableTagFallsBackToItself()
        {
            // "und" resolves to the invariant culture ("Invariant Language (Invariant Country)"), which is
            // worse than showing the raw tag.
            Assert.AreEqual("und", SubtitleFile.LabelFor("und", false));
        }

        private static StorageManager StorageManagerFor(string downloadRoot)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["DownloadDirectory"] = downloadRoot,
                    ["DataDirectory"] = downloadRoot,
                })
                .Build();
            return new StorageManager(NullLogger<VideoStorageService>.Instance, config);
        }
    }

    /// <summary>SubRip has to become WebVTT or the browser silently shows no cues.</summary>
    [TestClass]
    public class SubtitleConverterTests
    {
        [TestMethod]
        public void AddsTheHeaderAndFixesTheDecimalSeparator()
        {
            string vtt = SubtitleConverter.SrtToVtt(
                "1\n00:00:00,400 --> 00:00:03,600\nHello there\n\n2\n00:00:03,600 --> 00:00:08,320\nAnd again\n");

            StringAssert.StartsWith(vtt, "WEBVTT\n\n");
            StringAssert.Contains(vtt, "00:00:00.400 --> 00:00:03.600");
            StringAssert.Contains(vtt, "00:00:03.600 --> 00:00:08.320");
            Assert.IsFalse(vtt.Contains(","), "no comma separators survive: " + vtt);
            StringAssert.Contains(vtt, "Hello there");
        }

        [TestMethod]
        public void DropsCueCountersButKeepsNumericSubtitleText()
        {
            // "42" as the whole of a cue's text must survive; only a counter directly above a timecode goes.
            string vtt = SubtitleConverter.SrtToVtt("1\n00:00:01,000 --> 00:00:02,000\n42\n\n");

            StringAssert.Contains(vtt, "42");
            Assert.IsFalse(vtt.Contains("\n1\n"), "the cue counter is gone: " + vtt);
        }

        [TestMethod]
        public void StripsAByteOrderMark()
        {
            string vtt = SubtitleConverter.SrtToVtt("﻿1\n00:00:01,000 --> 00:00:02,000\nHi\n");

            StringAssert.StartsWith(vtt, "WEBVTT", "a BOM before WEBVTT breaks parsing in some browsers");
        }

        [TestMethod]
        public void StripsCuePlacementSoAsrTracksAreNotPinnedLeft()
        {
            // YouTube's ASR files put "align:start position:0%" on every cue, which renders them ragged
            // against the left edge while hand-written tracks sit centred. CSS can't fix it — alignment
            // is a cue setting, not a style — so the settings come off here.
            string vtt = SubtitleConverter.ToWebVtt(
                "WEBVTT\n\n00:00:00.240 --> 00:00:01.990 align:start position:0%\nHello\n\n", "vtt");

            StringAssert.Contains(vtt, "00:00:00.240 --> 00:00:01.990");
            Assert.IsFalse(vtt.Contains("align:"), "alignment stripped: " + vtt);
            Assert.IsFalse(vtt.Contains("position:"), "position stripped: " + vtt);
            StringAssert.Contains(vtt, "Hello");
        }

        [TestMethod]
        public void LeavesTimecodesAndCueTextAlone()
        {
            string vtt = SubtitleConverter.ToWebVtt(
                "WEBVTT\n\n00:01:02.500 --> 00:01:05.000\nline one\nline two\n\n", "vtt");

            StringAssert.Contains(vtt, "00:01:02.500 --> 00:01:05.000");
            StringAssert.Contains(vtt, "line one\nline two");
        }

        [TestMethod]
        public void SrtIsConvertedAndNormalisedInOnePass()
        {
            string vtt = SubtitleConverter.ToWebVtt(
                "1\n00:00:01,000 --> 00:00:02,000\nHi\n\n", "srt");

            StringAssert.StartsWith(vtt, "WEBVTT\n\n");
            StringAssert.Contains(vtt, "00:00:01.000 --> 00:00:02.000");
        }

        [TestMethod]
        public void HandlesCrlfAndNonAscii()
        {
            string vtt = SubtitleConverter.SrtToVtt("1\r\n00:00:01,000 --> 00:00:02,000\r\nÎn timp ce așteptați\r\n");

            StringAssert.Contains(vtt, "În timp ce așteptați");
            StringAssert.Contains(vtt, "00:00:01.000 --> 00:00:02.000");
        }
    }
}
