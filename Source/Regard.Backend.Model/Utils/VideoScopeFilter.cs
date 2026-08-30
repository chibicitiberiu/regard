using System;

namespace Regard.Backend.Common.Utils
{
    /// <summary>
    /// Decides whether a video is in scope for a subscription, from signals that are available during a
    /// FLAT sync — before any per-video extraction. Both predicates are pure, so the sync job can call
    /// them per entry without touching the network or the database.
    /// </summary>
    public static class VideoScopeFilter
    {
        private const string SubscriberOnly = "subscriber_only";

        /// <summary>
        /// True when the URL is a YouTube Short.
        ///
        /// The signal is the URL shape: entries listed from a channel's Shorts tab come back as
        /// youtube.com/shorts/&lt;id&gt;, while the Videos tab yields watch?v=&lt;id&gt;. This parses the
        /// path rather than doing a substring search, so a watch URL whose query or fragment happens to
        /// contain "/shorts/" is not mistaken for one.
        ///
        /// Duration is deliberately NOT used as a fallback. Plenty of ordinary uploads are under a
        /// minute (CGP Grey's footnote videos, for two that sit in the dev library at 32 s and 9 s), and
        /// a length threshold would silently discard them.
        ///
        /// Known limit: a Short reached through a channel's "uploads" (UU…) playlist is returned as a
        /// plain watch URL, and nothing in a flat listing distinguishes it. Those are not detected.
        /// </summary>
        public static bool IsShort(string originalUrl)
        {
            if (string.IsNullOrWhiteSpace(originalUrl))
                return false;

            if (!Uri.TryCreate(originalUrl, UriKind.Absolute, out var uri))
                return false;

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return false;

            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length >= 1
                && string.Equals(segments[0], "shorts", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// True when yt-dlp reports the video as members-only.
        ///
        /// Matches "subscriber_only" and nothing else. The other restricted values yt-dlp can report —
        /// premium_only, needs_auth, private, unlisted — are different situations with different fixes,
        /// and lumping them in here would quietly drop videos the user can actually watch.
        /// </summary>
        public static bool IsMembersOnly(string availability)
        {
            return string.Equals(availability, SubscriberOnly, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Given a channel's "Videos" tab URL, produces the sibling "Shorts" tab URL; returns false for
        /// anything else.
        ///
        /// This exists because a channel subscription never sees a Short otherwise.
        /// YouTubeUrlHelper.FixYouTubeChannelUri normalises youtube.com/@Handle (and /channel/ID, /c/,
        /// /user/) to the channel's /videos tab at creation time, and YouTube's Videos tab excludes
        /// Shorts by construction — they live in their own tab. So with only the stored URL to go on,
        /// "Include Shorts" would be a setting that could never change anything.
        ///
        /// The sync job therefore lists this second URL as well, but only when the option is on. With it
        /// off (the default) nothing extra is fetched and behaviour is exactly as before.
        /// </summary>
        public static bool TryGetShortsTabUrl(string channelUrl, out string shortsUrl)
        {
            shortsUrl = null;
            if (string.IsNullOrWhiteSpace(channelUrl))
                return false;

            if (!Uri.TryCreate(channelUrl, UriKind.Absolute, out var uri))
                return false;

            if (!uri.Host.EndsWith("youtube.com", StringComparison.OrdinalIgnoreCase))
                return false;

            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2 || !string.Equals(segments[^1], "videos", StringComparison.OrdinalIgnoreCase))
                return false;

            var builder = new UriBuilder(uri)
            {
                Path = string.Join('/', segments[..^1]) + "/shorts",
                Query = string.Empty,
                Fragment = string.Empty,
            };
            shortsUrl = builder.Uri.ToString();
            return true;
        }
    }
}
