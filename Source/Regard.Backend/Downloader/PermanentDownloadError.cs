using System;

namespace Regard.Backend.Downloader
{
    /// <summary>
    /// Recognises yt-dlp stderr lines that describe a permanent, non-transient failure — a video that
    /// will never download no matter how often we retry (members-only, private, removed/terminated).
    /// Retrying these only wastes requests against YouTube's bot gate and, for a
    /// <c>[ResumeAfterRestart]</c> download, re-queues on every boot and starves background maintenance.
    /// Returns a short human-readable reason, or <c>null</c> when the line is not a known-permanent error.
    /// </summary>
    public static class PermanentDownloadError
    {
        public static string Match(string stderrLine)
        {
            if (string.IsNullOrEmpty(stderrLine))
                return null;

            // Only real yt-dlp ERROR lines are permanent; a WARNING with the same words does not fail
            // the download. This also keeps a transient "ERROR: HTTP Error 429" from matching — it
            // carries none of the phrases below.
            if (stderrLine.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase) < 0)
                return null;

            bool Has(string sub) => stderrLine.IndexOf(sub, StringComparison.OrdinalIgnoreCase) >= 0;

            if (Has("members-only") || Has("available to this channel's members") || Has("Join this channel"))
                return "members-only";
            if (Has("Private video") || Has("This video is private"))
                return "private video";
            if (Has("removed by the uploader") || Has("account associated with this video has been terminated"))
                return "removed";

            return null;
        }
    }
}
