using System;
using System.Collections.Generic;
using System.Linq;

namespace Regard.Common.SponsorBlock
{
    /// <summary>What to do with a given SponsorBlock category.</summary>
    public enum SbAction
    {
        Keep,       // ignore the category
        Chapter,    // mark as a chapter (yt-dlp --sponsorblock-mark) - non-destructive
        Remove,     // cut from the file (yt-dlp --sponsorblock-remove) - destructive
        Skip,       // skip in the player (non-destructive, applied at playback)
    }

    /// <summary>
    /// Parses/serializes the per-category SponsorBlock action map stored in the Sponsorblock_Actions
    /// option (a CSV of "category:action" pairs). Shared by the backend (download flags + segment fetch)
    /// and the frontend (the per-category edit table). Never throws on malformed input.
    /// </summary>
    public static class SponsorBlockActions
    {
        /// <summary>The categories Regard exposes (yt-dlp names). Order is the UI display order.</summary>
        public static readonly string[] Categories =
        {
            "sponsor", "selfpromo", "interaction", "intro", "outro", "preview", "filler", "music_offtopic",
        };

        /// <summary>Human labels for the UI, keyed by category.</summary>
        public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
        {
            ["sponsor"] = "Sponsor",
            ["selfpromo"] = "Self-promo / merch",
            ["interaction"] = "Interaction reminder",
            ["intro"] = "Intro / intermission",
            ["outro"] = "Outro / endcards",
            ["preview"] = "Preview / recap",
            ["filler"] = "Filler tangent",
            ["music_offtopic"] = "Non-music section",
        };

        public static Dictionary<string, SbAction> Parse(string csv)
        {
            var map = new Dictionary<string, SbAction>();
            if (string.IsNullOrWhiteSpace(csv))
                return map;

            foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var kv = part.Split(':', 2);
                if (kv.Length != 2)
                    continue;
                var cat = kv[0].Trim().ToLowerInvariant();
                if (!Categories.Contains(cat))
                    continue;
                if (Enum.TryParse<SbAction>(kv[1].Trim(), ignoreCase: true, out var action) && action != SbAction.Keep)
                    map[cat] = action;
            }
            return map;
        }

        public static string Serialize(IReadOnlyDictionary<string, SbAction> map)
        {
            if (map == null)
                return "";
            // Emit in canonical category order, skipping Keep, so the stored string is stable.
            return string.Join(",", Categories
                .Where(c => map.TryGetValue(c, out var a) && a != SbAction.Keep)
                .Select(c => $"{c}:{map[c].ToString().ToLowerInvariant()}"));
        }

        public static List<string> CategoriesWith(string csv, SbAction action)
        {
            var map = Parse(csv);
            return Categories.Where(c => map.TryGetValue(c, out var a) && a == action).ToList();
        }

        /// <summary>Any Chapter/Remove/Skip action set at all.</summary>
        public static bool Any(string csv) => Parse(csv).Count > 0;

        /// <summary>
        /// Remove and Skip cannot both be present: Remove cuts the file (shifting timestamps) while Skip
        /// runs in the player against the original timeline, so on a cut file the Skips would misalign.
        /// Chapter (mark only) is safe to combine with either.
        /// </summary>
        public static bool HasRemoveSkipConflict(string csv)
        {
            var map = Parse(csv);
            return map.Values.Contains(SbAction.Remove) && map.Values.Contains(SbAction.Skip);
        }
    }
}
