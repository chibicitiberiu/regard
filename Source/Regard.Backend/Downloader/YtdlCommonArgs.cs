using Regard.Backend.Common.Utils;
using Regard.Backend.Configuration;
using Regard.Backend.Model;
using System.Collections.Generic;

namespace Regard.Backend.Downloader
{
    /// <summary>
    /// yt-dlp argument fragments shared by the download job and the sidecar-only reprocess job.
    ///
    /// Only the blocks that genuinely mean the same thing in both live here. Everything about *media* —
    /// the format selector, remux/recode, the rate limit, SponsorBlock — deliberately stays in
    /// <see cref="DownloadVideoJob.ResolveDownloadOptions"/>: with --skip-download those are at best
    /// inert and at worst fatal (a format selector that matches nothing fails the whole run even when no
    /// media is wanted).
    /// </summary>
    public static class YtdlCommonArgs
    {
        /// <summary>
        /// Server-wide inter-request pacing. Separate from <see cref="YtdlAntibotArgs"/>, which covers
        /// cookies and impersonation; this is the "don't hammer the host" half.
        /// </summary>
        public static IEnumerable<string> ServerSleep(IOptionManager optionManager)
        {
            if (!optionManager.GetGlobal(Options.Server_Throttle_Enabled))
                yield break;

            int sleepMin = optionManager.GetGlobal(Options.Server_Ytdl_SleepInterval);
            int sleepMax = optionManager.GetGlobal(Options.Server_Ytdl_MaxSleepInterval);
            if (sleepMin <= 0)
                yield break;

            yield return "--sleep-interval";
            yield return sleepMin.ToString();
            if (sleepMax > sleepMin)
            {
                yield return "--max-sleep-interval";
                yield return sleepMax.ToString();
            }
        }

        /// <summary>
        /// The subtitle block: which tracks to write, in which languages and format.
        ///
        /// Language and format are emitted only when something is actually being written — the options
        /// have non-null defaults, so emitting them unconditionally sent yt-dlp noise on every download,
        /// and a trailing default "--sub-langs en" silently overrode "all".
        ///
        /// <paramref name="subFormatOverride"/> exists for the reprocess job. The stored default is
        /// "best", which can resolve to json3; SubtitleFile only recognises vtt and srt, so such a file
        /// lands on disk invisible to both the player and the "does this video have subtitles?" check —
        /// and a sweep would re-fetch it forever.
        /// </summary>
        public static IEnumerable<string> Subtitles(
            IOptionManager optionManager, int subscriptionId, string subFormatOverride = null)
        {
            bool writeSubs = optionManager.GetForSubscription(Options.Ytdl_WriteSubtitles, subscriptionId);
            bool writeAutoSubs = optionManager.GetForSubscription(Options.Ytdl_WriteAutoSub, subscriptionId);

            if (writeSubs)
                yield return "--write-subs";

            if (writeAutoSubs)
                yield return "--write-auto-subs";

            if (!writeSubs && !writeAutoSubs)
                yield break;

            if (optionManager.GetForSubscription(Options.Ytdl_AllSubs, subscriptionId))
            {
                yield return "--sub-langs";
                yield return "all";
            }
            else
            {
                string subLang = optionManager.GetForSubscription(Options.Ytdl_SubLang, subscriptionId);
                if (!string.IsNullOrWhiteSpace(subLang))
                {
                    yield return "--sub-langs";
                    yield return subLang;
                }
            }

            string subFormat = subFormatOverride
                ?? optionManager.GetForSubscription(Options.Ytdl_SubFormat, subscriptionId);
            if (!string.IsNullOrWhiteSpace(subFormat))
            {
                yield return "--sub-format";
                yield return subFormat;
            }
        }
    }
}
