using Microsoft.VisualStudio.TestTools.UnitTesting;
using Regard.Backend.Services;
using System.Linq;

namespace Regard.Backend.Tests
{
    /// <summary>
    /// Guards the impersonation wiring. The stakes are higher than they look: yt-dlp throws in
    /// YoutubeDL.__init__ when --impersonate names a target it can't resolve, so getting this wrong
    /// doesn't degrade impersonation — it breaks every extraction and download on the server.
    /// </summary>
    [TestClass]
    public class YtdlImpersonateTests
    {
        // Verbatim output of `yt-dlp --list-impersonate-targets` (2026.8.19) on a host without curl_cffi.
        private const string ListingNothingAvailable = @"[info] Available impersonate targets
Client    OS   Source
--------------------------------------------
Tor       -    curl_cffi>=0.11 (unavailable)
Edge      -    curl_cffi (unavailable)
Firefox   -    curl_cffi (unavailable)
Safari    -    curl_cffi (unavailable)
Chrome    -    curl_cffi (unavailable)
";

        // Same command with curl_cffi installed: available targets are appended without the
        // "(unavailable)" tag, carry versions, and may name an OS.
        private const string ListingSomeAvailable = @"[info] Available impersonate targets
Client       OS            Source
-----------------------------------------------
Tor          -             curl_cffi>=0.11 (unavailable)
Safari       -             curl_cffi (unavailable)
Chrome-110   windows-10    curl_cffi
Chrome-124   macos-14      curl_cffi
Edge-99      windows-10    curl_cffi
Firefox-133  -             curl_cffi>=0.10
";

        [TestMethod]
        public void Parse_NoCurlCffi_YieldsNoTargets()
        {
            var targets = YoutubeDLService.ParseImpersonateTargets(ListingNothingAvailable);
            Assert.AreEqual(0, targets.Count, "every row is tagged (unavailable)");
        }

        [TestMethod]
        public void Parse_KeepsOnlyAvailableClients_Deduplicated()
        {
            var targets = YoutubeDLService.ParseImpersonateTargets(ListingSomeAvailable);

            CollectionAssert.AreEquivalent(new[] { "chrome", "edge", "firefox" }, targets.ToArray());
            Assert.IsFalse(targets.Contains("tor"), "unavailable rows must not leak in");
            Assert.IsFalse(targets.Contains("safari"), "unavailable rows must not leak in");
            Assert.AreEqual(1, targets.Count(t => t == "chrome"), "Chrome-110 and Chrome-124 collapse to one client");
        }

        [TestMethod]
        public void Parse_HandlesEmptyAndGarbage()
        {
            Assert.AreEqual(0, YoutubeDLService.ParseImpersonateTargets(null).Count);
            Assert.AreEqual(0, YoutubeDLService.ParseImpersonateTargets("").Count);
            Assert.AreEqual(0, YoutubeDLService.ParseImpersonateTargets("ERROR: something went wrong\n").Count);
        }

        [TestMethod]
        public void Parse_IgnoresNoiseAroundTheTable()
        {
            // A phantom target would be worse than no target: a non-empty list is what allows "auto",
            // and yt-dlp aborts on every call if that target can't actually be resolved.
            const string noisy = @"WARNING: something unrelated
[info] Available impersonate targets
Client       OS            Source
-----------------------------------------------
Chrome-110   windows-10    curl_cffi
Safari       -             curl_cffi (unavailable)
ERROR: late failure
[debug] trailing chatter
";
            var targets = YoutubeDLService.ParseImpersonateTargets(noisy);
            CollectionAssert.AreEquivalent(new[] { "chrome" }, targets.ToArray());
        }

        [TestMethod]
        public void Resolve_OffWhenNotConfigured()
        {
            var available = new[] { "chrome" };
            Assert.IsNull(YtdlAntibotArgs.ResolveImpersonate(null, available, null));
            Assert.IsNull(YtdlAntibotArgs.ResolveImpersonate("", available, null));
            Assert.IsNull(YtdlAntibotArgs.ResolveImpersonate("   ", available, null));
        }

        [TestMethod]
        public void Resolve_SkipsWhenNothingIsAvailable()
        {
            var none = new string[0];

            // The important case: curl_cffi missing. Even "auto" must be dropped — yt-dlp fails on
            // --impersonate= just as hard as on a named target.
            Assert.IsNull(YtdlAntibotArgs.ResolveImpersonate("chrome", none, null));
            Assert.IsNull(YtdlAntibotArgs.ResolveImpersonate("auto", none, null));
            Assert.IsNull(YtdlAntibotArgs.ResolveImpersonate("chrome", null, null));
        }

        [TestMethod]
        public void Resolve_SkipsUnavailableOrMisspelledTarget()
        {
            var available = new[] { "chrome", "edge" };
            Assert.IsNull(YtdlAntibotArgs.ResolveImpersonate("safari", available, null));
            Assert.IsNull(YtdlAntibotArgs.ResolveImpersonate("chrom", available, null), "a typo must not break yt-dlp");
        }

        [TestMethod]
        public void Resolve_PassesAvailableTarget()
        {
            var available = new[] { "chrome", "edge" };

            Assert.AreEqual("--impersonate=chrome", YtdlAntibotArgs.ResolveImpersonate("chrome", available, null));
            Assert.AreEqual("--impersonate=", YtdlAntibotArgs.ResolveImpersonate("auto", available, null));
            Assert.AreEqual("--impersonate=", YtdlAntibotArgs.ResolveImpersonate("AUTO", available, null));
        }

        [TestMethod]
        public void Resolve_MatchesOnClientButKeepsTheFullTarget()
        {
            var available = new[] { "chrome" };

            // The probe only knows client names; a version/OS qualifier still has to reach yt-dlp intact.
            Assert.AreEqual("--impersonate=chrome-110",
                YtdlAntibotArgs.ResolveImpersonate("chrome-110", available, null));
            Assert.AreEqual("--impersonate=chrome:windows-10",
                YtdlAntibotArgs.ResolveImpersonate("chrome:windows-10", available, null));
            Assert.AreEqual("--impersonate=Chrome",
                YtdlAntibotArgs.ResolveImpersonate("Chrome", available, null));
        }

        [TestMethod]
        public void Resolve_EmitsExactlyOneArgument()
        {
            // A bare "--impersonate" with a separate value would swallow the following argument when the
            // value is empty, so the flag and its value must stay glued together.
            var arg = YtdlAntibotArgs.ResolveImpersonate("auto", new[] { "chrome" }, null);
            Assert.IsFalse(arg.Contains(' '), "must be a single argv entry");
            Assert.IsTrue(arg.StartsWith("--impersonate="));
        }
    }
}
