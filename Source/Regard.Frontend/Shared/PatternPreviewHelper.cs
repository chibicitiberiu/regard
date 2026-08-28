namespace Regard.Frontend.Shared
{
    /// <summary>
    /// Renders an illustrative example of a download path/filename template by substituting sample
    /// values for the common tokens. This is a client-side guide only — it does not run the server's
    /// real SmartFormat expansion or path normalization, so unknown/exotic tokens stay literal.
    /// </summary>
    public static class PatternPreviewHelper
    {
        /// <summary>The option's built-in default (kept in sync with Options.Subscriptions_DownloadPath).</summary>
        public const string DefaultPattern =
            "{DownloadDirectory}/{FolderPath}/{Subscription.Name}/{EpisodeCode} - {Video.Name}";

        public static string Render(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                pattern = DefaultPattern;

            return pattern
                .Replace("{DownloadDirectory}", "/downloads")
                .Replace("{DataDirectory}", "/data")
                .Replace("{FolderPath}", "Tech")
                .Replace("{Subscription.Name}", "CGP Grey")
                .Replace("{EpisodeCode}", "S2025E148")
                .Replace("{Video.Name}", "The Longest-Reigning Monarch");
        }
    }
}
