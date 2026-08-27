using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Regard.Backend.Common.Utils
{
    /// <summary>
    /// A node in a parsed import: a feed leaf (<see cref="Url"/> set) or a folder
    /// (<see cref="Url"/> null, with <see cref="Children"/>). The root is a folder with no title.
    /// </summary>
    public class ImportNode
    {
        public string Title { get; set; }

        /// <summary>Non-null marks this a feed/channel to subscribe to; null marks a folder.</summary>
        public string Url { get; set; }

        public List<ImportNode> Children { get; set; }

        public bool IsFolder => Url == null;
    }

    /// <summary>
    /// Parses import input into an <see cref="ImportNode"/> tree. Accepts an OPML document (folder
    /// groupings preserved) or a newline-separated URL list. YouTube feed URLs
    /// (feeds/videos.xml?channel_id=…) are rewritten to channel URLs so the yt-dlp provider handles
    /// them with full features. Never throws on bad input — it yields an empty/partial tree.
    /// </summary>
    public static class SubscriptionImportParser
    {
        public static ImportNode Parse(string content)
        {
            var root = new ImportNode { Children = new List<ImportNode>() };
            if (string.IsNullOrWhiteSpace(content))
                return root;

            if (content.TrimStart().StartsWith("<"))
                ParseOpml(content, root);
            else
                ParseUrlList(content, root);

            PruneEmptyFolders(root);
            return root;
        }

        /// <summary>Counts feed leaves in the tree (the batch size / progress total).</summary>
        public static int CountFeeds(ImportNode node)
        {
            if (node == null)
                return 0;
            int count = node.Url != null ? 1 : 0;
            if (node.Children != null)
                foreach (var child in node.Children)
                    count += CountFeeds(child);
            return count;
        }

        private static void ParseOpml(string xml, ImportNode root)
        {
            XDocument doc;
            try { doc = XDocument.Parse(xml); }
            catch { return; }

            var body = doc.Root?.Elements()
                .FirstOrDefault(e => e.Name.LocalName.Equals("body", StringComparison.OrdinalIgnoreCase));
            var container = body ?? doc.Root;
            if (container == null)
                return;

            foreach (var outline in Outlines(container))
                root.Children.Add(ParseOutline(outline));
        }

        private static IEnumerable<XElement> Outlines(XElement parent) =>
            parent.Elements().Where(e => e.Name.LocalName.Equals("outline", StringComparison.OrdinalIgnoreCase));

        private static ImportNode ParseOutline(XElement el)
        {
            string title = Attr(el, "title") ?? Attr(el, "text");
            string url = ResolveFeedUrl(Attr(el, "htmlUrl"), Attr(el, "xmlUrl"));

            if (url != null)
                return new ImportNode { Title = title, Url = url };

            var folder = new ImportNode
            {
                Title = string.IsNullOrWhiteSpace(title) ? "Imported" : title,
                Children = new List<ImportNode>(),
            };
            foreach (var child in Outlines(el))
                folder.Children.Add(ParseOutline(child));
            return folder;
        }

        private static void ParseUrlList(string content, ImportNode root)
        {
            foreach (var raw in content.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;
                if (IsHttp(line))
                    root.Children.Add(new ImportNode { Url = line });
            }
        }

        // Prefer the human page (htmlUrl); else the feed (xmlUrl), converting a YouTube feed to a
        // channel URL. Returns null when neither is a usable http(s) link (i.e. this is a folder).
        private static string ResolveFeedUrl(string htmlUrl, string xmlUrl)
        {
            if (IsHttp(htmlUrl))
                return htmlUrl;
            if (IsHttp(xmlUrl))
                return ConvertYouTubeFeed(xmlUrl);
            return null;
        }

        private static string ConvertYouTubeFeed(string url)
        {
            try
            {
                var uri = new Uri(url);
                if (!uri.Host.ToLowerInvariant().Contains("youtube.com"))
                    return url;
                if (!uri.AbsolutePath.TrimEnd('/').EndsWith("feeds/videos.xml", StringComparison.OrdinalIgnoreCase))
                    return url;

                var query = ParseQuery(uri.Query);
                if (query.TryGetValue("channel_id", out var channelId) && !string.IsNullOrEmpty(channelId))
                    return $"https://www.youtube.com/channel/{channelId}";
                if (query.TryGetValue("user", out var user) && !string.IsNullOrEmpty(user))
                    return $"https://www.youtube.com/user/{user}";
                return url;
            }
            catch
            {
                return url;
            }
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = pair.Split('=', 2);
                if (kv.Length == 2)
                    result[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv[1]);
            }
            return result;
        }

        private static string Attr(XElement el, string name) =>
            el.Attributes().FirstOrDefault(a => a.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;

        private static bool IsHttp(string s) =>
            Uri.TryCreate(s, UriKind.Absolute, out var u) &&
            (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);

        // Drop folder nodes that contain no feeds, so empty OPML groups don't create empty folders.
        private static void PruneEmptyFolders(ImportNode node)
        {
            if (node.Children == null)
                return;
            node.Children.RemoveAll(c => c.IsFolder && CountFeeds(c) == 0);
            foreach (var child in node.Children)
                PruneEmptyFolders(child);
        }
    }
}
