using System;

namespace Regard.Backend.Common.Utils
{
    /// <summary>
    /// Normalizes a URL to a stable "hosting domain" key for per-domain throttling (youtube.com,
    /// vimeo.com, ...). Mirrors the host normalization in VideoEmbedHelper (lowercase, strip www./m.).
    /// Never throws — a malformed/empty URL yields "unknown".
    /// </summary>
    public static class UrlHostKey
    {
        public static string Of(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return "unknown";

            try
            {
                var host = new Uri(url).Host.ToLowerInvariant();
                if (host.StartsWith("www."))
                    host = host.Substring(4);
                if (host.StartsWith("m."))
                    host = host.Substring(2);
                return string.IsNullOrEmpty(host) ? "unknown" : host;
            }
            catch
            {
                return "unknown";
            }
        }
    }
}
