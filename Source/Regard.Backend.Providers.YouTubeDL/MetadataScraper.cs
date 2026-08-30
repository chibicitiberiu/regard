using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Regard.Backend.Providers.YouTubeDL
{
    public static class MetadataScraper
    {
        // One shared client with a real timeout. HtmlWeb.Load, which this replaces, is synchronous
        // blocking I/O with no timeout of its own (100 s HttpClient default), so a slow or hanging
        // response parked a thread-pool thread for that long — and wrapping it in Task.Run + WhenAny
        // would NOT have cancelled it, only stopped waiting for it.
        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15),
        };

        private const string BrowserUserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:87.0) Gecko/20100101 Firefox/87.0";

        /// <summary>
        /// Fetches a page and yields its meta/link tags. Returns empty on any failure — this only
        /// supplies nicer channel titles and artwork than yt-dlp gives, so callers fall back rather than
        /// fail. Never throws.
        /// </summary>
        public static async Task<IReadOnlyList<KeyValuePair<string, string>>> ScrapeMetadataAsync(
            Uri uri, CancellationToken cancellationToken = default, Action<string> diagnostic = null)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.TryAddWithoutValidation("User-Agent", BrowserUserAgent);
                // YouTube answers a bare channel request with a redirect to a consent interstitial;
                // these are what the redirect chain sets, and asking for them up front skips it.
                request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
                request.Headers.TryAddWithoutValidation("Cookie", "CONSENT=YES+cb; SOCS=CAI");

                using var response = await Http.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    diagnostic?.Invoke($"metadata scrape got HTTP {(int)response.StatusCode} for {uri}");
                    return Array.Empty<KeyValuePair<string, string>>();
                }

                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                var parsed = ParseMetadata(html);
                diagnostic?.Invoke($"metadata scrape of {uri}: {parsed.Count} tag(s) from {html.Length} bytes");
                return parsed;
            }
            catch (Exception ex)
            {
                // Timeout, DNS failure, a bot interstitial, malformed HTML — all mean "no extra metadata".
                diagnostic?.Invoke($"metadata scrape of {uri} failed: {ex.Message}");
                return Array.Empty<KeyValuePair<string, string>>();
            }
        }

        internal static IReadOnlyList<KeyValuePair<string, string>> ParseMetadata(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            return Extract(doc).ToList();
        }

        private static IEnumerable<KeyValuePair<string, string>> Extract(HtmlDocument doc)
        {
            var metaNodes = doc.DocumentNode.SelectNodes("//meta");
            if (metaNodes != null)
            {
                foreach (var metaNode in metaNodes)
                {
                    string name = metaNode.Attributes["name"]?.Value
                            ?? metaNode.Attributes["property"]?.Value
                            ?? metaNode.Attributes["itemprop"]?.Value;

                    if (name != null)
                    {
                        string value = metaNode.Attributes["content"]?.Value;
                        yield return new KeyValuePair<string, string>(name, value);
                    }
                }
            }

            var linkNodes = doc.DocumentNode.SelectNodes("//link[@itemprop]");
            if (linkNodes != null)
            {
                foreach (var linkNode in linkNodes)
                {
                    string itemprop = linkNode.Attributes["itemprop"]?.Value;
                    string href = linkNode.Attributes["href"]?.Value;

                    if (itemprop != null)
                        yield return new KeyValuePair<string, string>("link:" + itemprop, href);
                }
            }
        }
    }
}
