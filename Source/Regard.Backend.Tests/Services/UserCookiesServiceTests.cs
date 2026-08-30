using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Regard.Backend.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Regard.Backend.Tests
{
    /// <summary>
    /// The per-user cookie jar is security-sensitive: the path this service produces is handed to yt-dlp
    /// as --cookies, and yt-dlp both READS that file and WRITES the jar back to it at the end of a run.
    /// A path a user could influence would therefore be an arbitrary file read and an arbitrary
    /// overwrite. These tests pin the two properties that prevent that — the id is validated, and the
    /// result always lands inside the cookies directory.
    /// </summary>
    [TestClass]
    public class UserCookiesServiceTests
    {
        private string dataDir;
        private UserCookiesService svc;
        private StorageManager storage;

        [TestInitialize]
        public void Setup()
        {
            dataDir = Path.Combine(Path.GetTempPath(), "regard-cookies-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dataDir);

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["DataDirectory"] = dataDir,
                    ["DownloadDirectory"] = Path.Combine(dataDir, "videos"),
                })
                .Build();

            storage = new StorageManager(NullLogger<VideoStorageService>.Instance, config);
            Directory.CreateDirectory(storage.CookiesDirectory);
            svc = new UserCookiesService(storage, NullLogger<UserCookiesService>.Instance);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try { Directory.Delete(dataDir, recursive: true); } catch { }
        }


        /// <summary>HasCookies as the app asks it: the option points at this user's own jar.</summary>
        private bool Configured(string userId) => svc.HasCookies(userId, svc.PathFor(userId));

        [TestMethod]
        public async Task AnOrphanFileDoesNotCountAsConfigured()
        {
            // yt-dlp rewrites the jar at the end of every run it is given, so a run still in flight when
            // the user hits Remove can recreate the file. The cleared option is the source of truth —
            // otherwise the UI claims "your own cookies are in use" for a file nothing will ever read.
            await svc.ApplyAsync(RealUserId, "cookies");
            await svc.ApplyAsync(RealUserId, "");                       // removed; option becomes ""
            File.WriteAllText(svc.PathFor(RealUserId), "# recreated by yt-dlp\n");

            Assert.IsTrue(File.Exists(svc.PathFor(RealUserId)), "the orphan is on disk");
            Assert.IsFalse(svc.HasCookies(RealUserId, ""), "but it is not configured");
            Assert.IsFalse(svc.HasCookies(RealUserId, null));
        }

        [TestMethod]
        public async Task AConfiguredPathThatIsNotOursDoesNotCount()
        {
            // Defence in depth: if a stored value ever pointed somewhere else, don't report it as this
            // user's jar.
            await svc.ApplyAsync(RealUserId, "cookies");
            Assert.IsFalse(svc.HasCookies(RealUserId, "/etc/passwd"));
            Assert.IsFalse(svc.HasCookies(RealUserId, Path.Combine(dataDir, "Regard.db")));
        }

        private const string RealUserId = "a3f1c2d4-5e6f-4a7b-8c9d-0e1f2a3b4c5d";

        [TestMethod]
        public void BuildsAPathInsideTheCookiesDirectory()
        {
            var path = svc.PathFor(RealUserId);

            Assert.IsNotNull(path);
            Assert.IsTrue(Path.GetFullPath(path).StartsWith(Path.GetFullPath(storage.CookiesDirectory), StringComparison.Ordinal),
                          $"escaped the cookies directory: {path}");
            Assert.IsTrue(path.EndsWith(".txt", StringComparison.Ordinal));
        }

        [TestMethod]
        public void RefusesIdsThatCouldEscapeTheDirectory()
        {
            // Path.Combine happily accepts these; the allowlist is what stops them.
            Assert.IsNull(svc.PathFor("../../etc/passwd"));
            Assert.IsNull(svc.PathFor("..\\..\\windows\\system32\\config"));
            Assert.IsNull(svc.PathFor("/etc/passwd"));
            Assert.IsNull(svc.PathFor("a/b"));
            Assert.IsNull(svc.PathFor("id with spaces"));
            Assert.IsNull(svc.PathFor("id.with.dots"));
            Assert.IsNull(svc.PathFor(""));
            Assert.IsNull(svc.PathFor(null));
            Assert.IsNull(svc.PathFor(new string('a', 65)), "absurdly long ids are refused too");
        }

        [TestMethod]
        public void AbsolutePathInjectionCannotRedirectTheJar()
        {
            // The scenario that matters: if an id like this were accepted, Path.Combine would discard the
            // cookies directory entirely and yt-dlp would overwrite the database.
            var evil = Path.Combine(dataDir, "Regard.db");
            Assert.IsNull(svc.PathFor(evil));
        }

        [TestMethod]
        public async Task WritesReadsAndRemovesAJar()
        {
            Assert.IsFalse(Configured(RealUserId));

            var stored = await svc.ApplyAsync(RealUserId, "# Netscape HTTP Cookie File\n.youtube.com\tTRUE\t/\tTRUE\t0\tX\tY\n");
            Assert.AreEqual(svc.PathFor(RealUserId), stored);
            Assert.IsTrue(Configured(RealUserId));
            Assert.IsTrue(File.ReadAllText(stored).Contains("youtube.com"));

            // Empty string means "remove", and the option value becomes empty rather than null.
            var cleared = await svc.ApplyAsync(RealUserId, "");
            Assert.AreEqual("", cleared);
            Assert.IsFalse(Configured(RealUserId));
        }

        [TestMethod]
        public async Task NullContentMeansLeaveItAlone()
        {
            await svc.ApplyAsync(RealUserId, "keep me");
            Assert.IsNull(await svc.ApplyAsync(RealUserId, null), "null must not be treated as a change");
            Assert.IsTrue(Configured(RealUserId), "the existing jar survives");
        }

        [TestMethod]
        public async Task RejectsAnOversizedUpload()
        {
            var huge = new string('x', UserCookiesService.MaxBytes + 1);
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => svc.ApplyAsync(RealUserId, huge));
            Assert.IsFalse(Configured(RealUserId), "nothing is written when it's refused");
        }

        [TestMethod]
        public async Task LeavesNoTempFileBehind()
        {
            await svc.ApplyAsync(RealUserId, "some cookies");
            var leftovers = Directory.GetFiles(storage.CookiesDirectory, "*.tmp");
            Assert.AreEqual(0, leftovers.Length, "temp-then-move must not leave a .tmp");
        }

        [TestMethod]
        public void TwoUsersGetSeparateJars()
        {
            var a = svc.PathFor(RealUserId);
            var b = svc.PathFor("b7e2d1c0-1111-2222-3333-444455556666");
            Assert.AreNotEqual(a, b);
        }

        [TestMethod]
        public async Task DeleteRemovesTheJar()
        {
            await svc.ApplyAsync(RealUserId, "cookies");
            Assert.IsTrue(Configured(RealUserId));

            svc.Delete(RealUserId);
            Assert.IsFalse(Configured(RealUserId));

            svc.Delete(RealUserId);            // idempotent
            svc.Delete("../../etc/passwd");    // and safe on a bad id
        }
    }
}
