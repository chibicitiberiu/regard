using Microsoft.Extensions.Logging;
using Regard.Backend.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Regard.Backend.Services
{
    /// <summary>
    /// Server-wide yt-dlp anti-bot arguments applied to EVERY yt-dlp invocation (download + extraction):
    /// cookies (to clear YouTube's "confirm you're not a bot" gate), browser TLS-fingerprint
    /// impersonation, and an inter-request sleep. Built as a fresh per-call list on demand so it is never
    /// shared mutable state on the singleton YoutubeDL instance (which is used concurrently once the job
    /// pool is > 1).
    /// </summary>
    public static class YtdlAntibotArgs
    {
        /// <summary>Configured value meaning "let yt-dlp pick any available target" (--impersonate=).</summary>
        public const string ImpersonateAuto = "auto";

        // Warning state, so a misconfigured target is reported once instead of on every single yt-dlp
        // call. Keyed on the (target, availability) pair so a config or environment change re-reports.
        private static string lastImpersonateWarning;

        /// <summary>
        /// Builds the per-call anti-bot args. <paramref name="subscriptionId"/> or
        /// <paramref name="userId"/> select whose cookie jar to use; give whichever is available and the
        /// resolution falls back to the server-wide jar when the user has none.
        /// </summary>
        public static List<string> Build(IOptionManager optionManager,
                                         IReadOnlyList<string> availableImpersonateTargets = null,
                                         ILogger log = null,
                                         int? subscriptionId = null,
                                         string userId = null)
        {
            var args = new List<string>();

            // Cookies apply regardless of the throttle toggle — they're what clears the bot gate.
            var cookiesFile = ResolveCookiesFile(optionManager, subscriptionId, userId);
            if (!string.IsNullOrWhiteSpace(cookiesFile) && File.Exists(cookiesFile))
            {
                args.Add("--cookies");
                args.Add(cookiesFile);
            }

            // Impersonation is also independent of the throttle: it changes how requests look, not how
            // often they're made.
            var impersonate = ResolveImpersonate(
                optionManager.GetGlobal(Options.Server_Ytdl_Impersonate),
                availableImpersonateTargets,
                log);
            if (impersonate != null)
                args.Add(impersonate);

            // Sleeps are throttle behavior.
            if (optionManager.GetGlobal(Options.Server_Throttle_Enabled))
            {
                int sleepRequests = optionManager.GetGlobal(Options.Server_Ytdl_SleepRequests);
                if (sleepRequests > 0)
                {
                    args.Add("--sleep-requests");
                    args.Add(sleepRequests.ToString(CultureInfo.InvariantCulture));
                }
            }

            return args;
        }

        /// <summary>
        /// Picks the cookie jar for whoever this call is on behalf of: their own if they've uploaded one,
        /// otherwise the server-wide jar.
        ///
        /// Worth knowing, because it reads wrong at a glance: the option carries only OptionFlags.User,
        /// yet GetForSubscription still works. Its per-subscription lookup is what that flag gates; the
        /// else branch walks folder -> user -> global regardless (OptionManager.cs:198-205). So a
        /// subscription id resolves through its owner without granting per-subscription cookie overrides
        /// that nothing should be setting.
        /// </summary>
        private static string ResolveCookiesFile(IOptionManager optionManager, int? subscriptionId, string userId)
        {
            if (subscriptionId.HasValue)
                return optionManager.GetForSubscription(Options.Server_Ytdl_CookiesFile, subscriptionId.Value);

            if (!string.IsNullOrEmpty(userId))
                return optionManager.GetForUser(Options.Server_Ytdl_CookiesFile, userId);

            // No owner in scope (e.g. probing a URL before any subscription exists): the shared jar.
            return optionManager.GetGlobal(Options.Server_Ytdl_CookiesFile);
        }

        /// <summary>
        /// Turns the configured impersonate target into a single "--impersonate=VALUE" argument, or null
        /// when it should be left off.
        ///
        /// The availability check is not defensive politeness: yt-dlp raises in YoutubeDL.__init__ when
        /// the target can't be resolved, so passing an unavailable one breaks *every* extraction and
        /// download before any network call. That includes --impersonate= ("any"), which fails just the
        /// same when curl_cffi isn't installed. So an unusable configuration degrades to "no
        /// impersonation" with a warning rather than taking the server down.
        /// </summary>
        public static string ResolveImpersonate(string configured,
                                                  IReadOnlyList<string> available,
                                                  ILogger log)
        {
            if (string.IsNullOrWhiteSpace(configured))
                return null;

            configured = configured.Trim();
            available ??= Array.Empty<string>();

            bool auto = string.Equals(configured, ImpersonateAuto, StringComparison.OrdinalIgnoreCase);

            // A configured target may carry a version and/or OS ("chrome-110:windows-10"); the probe
            // only knows client names, which is the part yt-dlp matches loosely anyway.
            var client = configured.Split(':')[0].Split('-')[0].ToLowerInvariant();

            bool usable = available.Count > 0 && (auto || available.Contains(client));
            if (!usable)
            {
                Warn(log, configured, available);
                return null;
            }

            // "--impersonate=" (empty value) is yt-dlp's "any available target"; it must be passed as a
            // single argument, since a bare "--impersonate" followed by nothing would swallow the next arg.
            return auto ? "--impersonate=" : "--impersonate=" + configured;
        }

        private static void Warn(ILogger log, string configured, IReadOnlyList<string> available)
        {
            var key = configured + "|" + string.Join(",", available);
            if (key == lastImpersonateWarning)
                return;
            lastImpersonateWarning = key;

            if (available.Count == 0)
            {
                log?.LogWarning(
                    "yt-dlp impersonation is set to '{0}' but no targets are available — install curl_cffi " +
                    "for the Python that runs yt-dlp. Continuing without --impersonate.", configured);
            }
            else
            {
                log?.LogWarning(
                    "yt-dlp impersonate target '{0}' is not available (available: {1}). Continuing without " +
                    "--impersonate.", configured, string.Join(", ", available));
            }
        }
    }
}
