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
    /// Guards which files a video "owns". This decides what "Download again" and "Delete downloaded
    /// files" destroy, so a matching bug here costs the user data that isn't recoverable from the app.
    /// </summary>
    [TestClass]
    public class VideoStorageFileMatchingTests
    {
        private string root;
        private VideoStorageService storage;

        [TestInitialize]
        public void Setup()
        {
            root = Path.Combine(Path.GetTempPath(), "regard-storage-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            storage = new VideoStorageService(NullLogger<VideoStorageService>.Instance, StorageManagerFor(root));
        }

        [TestCleanup]
        public void Cleanup()
        {
            try { Directory.Delete(root, recursive: true); } catch { /* best effort */ }
        }

        private void Touch(params string[] relativePaths)
        {
            foreach (var rel in relativePaths)
            {
                var full = Path.Combine(root, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllText(full, "x");
            }
        }

        private async Task<List<string>> FilesAt(string prefix)
        {
            var found = new List<string>();
            await foreach (var f in storage.GetFilesAt(prefix))
                found.Add(Path.GetFileName(f));
            found.Sort(StringComparer.Ordinal);
            return found;
        }

        [TestMethod]
        public async Task CollectsEveryOutputOfOneVideo()
        {
            Touch(
                "CGP Grey/S2025E1 - Foo.mp4",
                "CGP Grey/S2025E1 - Foo.en.vtt",
                "CGP Grey/S2025E1 - Foo.ro.vtt",
                "CGP Grey/S2025E1 - Foo.info.json",
                "CGP Grey/S2025E1 - Foo.f315.webm.part");

            var found = await FilesAt("CGP Grey/S2025E1 - Foo");

            Assert.AreEqual(5, found.Count, string.Join(", ", found));
            CollectionAssert.Contains(found, "S2025E1 - Foo.f315.webm.part", "a stale .part must be swept");
            CollectionAssert.Contains(found, "S2025E1 - Foo.info.json");
        }

        [TestMethod]
        public async Task DoesNotClaimASiblingWithALongerName()
        {
            // The reason a bare StartsWith is unsafe: forcing a re-download of "Foo" would otherwise
            // delete "Foo (Part 2)"'s media and subtitles, with nothing to restore them from.
            Touch(
                "Sub/S1E1 - Foo.mp4",
                "Sub/S1E1 - Foo.en.vtt",
                "Sub/S1E1 - Foo (Part 2).mp4",
                "Sub/S1E1 - Foo (Part 2).en.vtt",
                "Sub/S1E1 - Foobar.mp4");

            var found = await FilesAt("Sub/S1E1 - Foo");

            CollectionAssert.AreEquivalent(
                new[] { "S1E1 - Foo.mp4", "S1E1 - Foo.en.vtt" }, found,
                "only the exact video's own outputs: " + string.Join(", ", found));
        }

        [TestMethod]
        public async Task ClaimsTheDashSuffixedJellyfinThumbnail()
        {
            // WriteEpisodeMetadata renames artwork to "<name>-thumb.jpg" with a DASH, not a dot — the one
            // output that doesn't follow the dot convention. Missing it orphans a thumbnail per
            // re-download.
            Touch(
                "Sub/S1E1 - Foo.mp4",
                "Sub/S1E1 - Foo.nfo",
                "Sub/S1E1 - Foo-thumb.jpg");

            var found = await FilesAt("Sub/S1E1 - Foo");

            Assert.AreEqual(3, found.Count, string.Join(", ", found));
            CollectionAssert.Contains(found, "S1E1 - Foo-thumb.jpg");
        }

        [TestMethod]
        public async Task DoesNotClaimASiblingStartingWithDashThumb()
        {
            // The trailing dot in "-thumb." is what keeps this precise.
            Touch(
                "Sub/S1E1 - Foo.mp4",
                "Sub/S1E1 - Foo-thumbnail deep dive.mp4");

            var found = await FilesAt("Sub/S1E1 - Foo");

            CollectionAssert.AreEquivalent(new[] { "S1E1 - Foo.mp4" }, found, string.Join(", ", found));
        }

        [TestMethod]
        public async Task HandlesATitleThatEndsWithADot()
        {
            // Real case from the dev library: "You Need To Quit Weed." — the prefix carries the dot, so
            // its outputs have two.
            Touch(
                "Sub/S1E1 - Quit Weed..mp4",
                "Sub/S1E1 - Quit Weed..en.vtt");

            var found = await FilesAt("Sub/S1E1 - Quit Weed.");

            Assert.AreEqual(2, found.Count, string.Join(", ", found));
        }

        [TestMethod]
        public async Task MatchesAnExtensionlessFileExactly()
        {
            Touch("Sub/S1E1 - Bare");

            var found = await FilesAt("Sub/S1E1 - Bare");

            CollectionAssert.AreEquivalent(new[] { "S1E1 - Bare" }, found);
        }

        [TestMethod]
        public async Task EmptyOrMissingInputsYieldNothing()
        {
            Assert.AreEqual(0, (await FilesAt(null)).Count);
            Assert.AreEqual(0, (await FilesAt("")).Count);
            Assert.AreEqual(0, (await FilesAt("No Such Dir/Nope")).Count);
        }

        [TestMethod]
        public async Task DeleteAtRemovesOnlyTheVideosOwnFiles()
        {
            Touch(
                "Sub/S1E1 - Foo.mp4",
                "Sub/S1E1 - Foo.en.vtt",
                "Sub/S1E1 - Foo.f315.webm.part",
                "Sub/S1E1 - Foo (Part 2).mp4");

            int deleted = await storage.DeleteAt("Sub/S1E1 - Foo");

            Assert.AreEqual(3, deleted);
            Assert.IsTrue(File.Exists(Path.Combine(root, "Sub/S1E1 - Foo (Part 2).mp4")),
                          "a sibling video must survive");
            Assert.IsFalse(File.Exists(Path.Combine(root, "Sub/S1E1 - Foo.mp4")));
            Assert.IsFalse(File.Exists(Path.Combine(root, "Sub/S1E1 - Foo.f315.webm.part")));
        }

        [TestMethod]
        public async Task GetFilesReturnsNothingForAVideoThatNeverFinished()
        {
            // DownloadedPath is written only on success, so a half-finished download is invisible to
            // GetFiles. This is why the forced re-download also sweeps the resolved output path.
            Touch("Sub/S1E1 - Half.f315.webm.part");

            var viaVideo = new List<string>();
            await foreach (var f in storage.GetFiles(new Video { DownloadedPath = null }))
                viaVideo.Add(f);

            Assert.AreEqual(0, viaVideo.Count, "nothing findable through the Video");
            Assert.AreEqual(1, (await FilesAt("Sub/S1E1 - Half")).Count, "but findable by path");
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
}
