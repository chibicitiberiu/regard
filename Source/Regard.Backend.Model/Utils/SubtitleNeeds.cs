using System;
using System.Collections.Generic;
using System.Linq;

namespace Regard.Backend.Common.Utils
{
    /// <summary>
    /// Decides whether a downloaded video is still missing subtitle languages the user asked for.
    ///
    /// Takes plain strings rather than the storage layer's SubtitleFile so it stays pure and testable
    /// alongside the other filters; the caller supplies the languages actually found on disk.
    /// </summary>
    public static class SubtitleNeeds
    {
        /// <summary>
        /// The configured languages, from the comma-separated <c>Ytdl_SubLang</c> option. Blank entries
        /// are dropped; order is preserved so the yt-dlp argument reads the way the user wrote it.
        /// </summary>
        public static IReadOnlyList<string> ParseWanted(string subLangCsv)
        {
            if (string.IsNullOrWhiteSpace(subLangCsv))
                return Array.Empty<string>();

            return subLangCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// True when <paramref name="present"/> covers <paramref name="wanted"/>.
        ///
        /// The match tolerates yt-dlp's "-orig" suffix: a channel's own original-language track is written
        /// as <c>en-orig</c>, which would never string-equal a configured <c>en</c>, and treating that as
        /// "missing" makes a sweep re-fetch the same video forever. A plain regional variant (<c>en-GB</c>)
        /// deliberately does NOT satisfy a request for <c>en</c> — those are genuinely different tracks.
        /// </summary>
        public static bool Satisfies(IEnumerable<string> present, IEnumerable<string> wanted)
        {
            return !MissingFrom(present, wanted).Any();
        }

        /// <summary>The wanted languages not present on disk, in the order they were requested.</summary>
        public static IReadOnlyList<string> MissingFrom(IEnumerable<string> present, IEnumerable<string> wanted)
        {
            var have = new HashSet<string>(
                (present ?? Enumerable.Empty<string>())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(StripOrig),
                StringComparer.OrdinalIgnoreCase);

            return (wanted ?? Enumerable.Empty<string>())
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .Where(w => !have.Contains(StripOrig(w)))
                .ToList();
        }

        private static string StripOrig(string lang)
        {
            lang = lang.Trim();
            return lang.EndsWith("-orig", StringComparison.OrdinalIgnoreCase)
                ? lang.Substring(0, lang.Length - "-orig".Length)
                : lang;
        }

        /// <summary>
        /// Whether a reprocess run could plausibly add anything.
        ///
        /// Returns false when subtitles are switched off entirely — there is nothing to fetch, and the
        /// sweep must not queue a job that would immediately no-op. With <c>allSubs</c> on there is no
        /// finite target set to compare against, so "has none at all" is the only answer we can give
        /// locally without asking yt-dlp what exists.
        /// </summary>
        public static bool NeedsSubtitles(
            IEnumerable<string> present, string subLangCsv, bool writeSubs, bool writeAutoSubs, bool allSubs)
        {
            if (!writeSubs && !writeAutoSubs)
                return false;

            var have = (present ?? Enumerable.Empty<string>()).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();

            if (allSubs)
                return have.Count == 0;

            var wanted = ParseWanted(subLangCsv);
            if (wanted.Count == 0)
                return have.Count == 0;

            return MissingFrom(have, wanted).Count > 0;
        }
    }
}
