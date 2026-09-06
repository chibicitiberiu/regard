using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Regard.Backend.Common.Utils;

namespace Regard.Backend.Services
{
    /// <summary>
    /// A <c>.nosubs</c> sidecar recording that a subtitle fetch confirmed there is nothing more to get
    /// for a given wanted-language configuration. Without it the hourly sweep re-queues a video YouTube
    /// simply has no captions for, spending one throttled extraction on it every hour, forever.
    ///
    /// The file's contents are the <see cref="Signature"/> of the configuration that was satisfied, so a
    /// later config change (a different signature) re-checks; a manual fetch ignores the sentinel entirely.
    /// It sits next to the media on the same extension-less prefix, so the normal sidecar cleanup removes
    /// it when the video is deleted.
    /// </summary>
    public static class SubtitleSentinel
    {
        public static string PathFor(string downloadedPath) => downloadedPath + ".nosubs";

        /// <summary>A compact, stable key for "which subtitles were asked for".</summary>
        public static string Signature(string subLangCsv, bool allSubs, bool writeSubs, bool writeAutoSubs)
        {
            string src = $"{(writeSubs ? "s" : "")}{(writeAutoSubs ? "a" : "")}";
            string langs = allSubs
                ? "all"
                : string.Join(",", SubtitleNeeds.ParseWanted(subLangCsv)
                    .Select(w => w.ToLowerInvariant())
                    .OrderBy(w => w, StringComparer.Ordinal));
            return $"{src}:{langs}";
        }

        /// <summary>True when a sentinel exists and matches the current configuration signature.</summary>
        public static bool IsSatisfied(string downloadedPath, string currentSignature)
        {
            try
            {
                var p = PathFor(downloadedPath);
                if (!File.Exists(p))
                    return false;
                return string.Equals(File.ReadAllText(p).Trim(), currentSignature, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        public static void Write(string downloadedPath, string signature)
        {
            try { File.WriteAllText(PathFor(downloadedPath), signature); }
            catch { /* best-effort: a missing sentinel only means one more (harmless) sweep attempt */ }
        }

        public static void Clear(string downloadedPath)
        {
            try
            {
                var p = PathFor(downloadedPath);
                if (File.Exists(p))
                    File.Delete(p);
            }
            catch { }
        }
    }
}
