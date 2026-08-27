using Regard.Backend.Model;
using System;

namespace Regard.Backend.Common.Utils
{
    /// <summary>
    /// Builds an embeddable player URL for a video when its source site supports iframe embedding.
    /// Only known-embeddable hosts are handled (YouTube today, via the privacy-enhanced no-cookie
    /// domain); everything else returns null so the watch page falls back to a "watch on the
    /// original site" link. Never throws — a malformed OriginalUrl yields null.
    /// </summary>
    public static class VideoEmbedHelper
    {
        public static string GetEmbedUrl(Video video)
        {
            try
            {
                if (string.IsNullOrEmpty(video?.OriginalUrl))
                    return null;

                var uri = new Uri(video.OriginalUrl);
                var host = uri.Host.ToLowerInvariant();
                if (host.StartsWith("www.")) host = host.Substring(4);
                if (host.StartsWith("m.")) host = host.Substring(2);

                switch (host)
                {
                    case "youtube.com":
                    case "youtu.be":
                    case "youtube-nocookie.com":
                        var id = !string.IsNullOrEmpty(video.VideoId)
                            ? video.VideoId
                            : ExtractYouTubeId(uri);
                        return string.IsNullOrEmpty(id)
                            ? null
                            : $"https://www.youtube-nocookie.com/embed/{id}";

                    default:
                        return null;
                }
            }
            catch
            {
                return null;
            }
        }

        // Fallback id extraction from the URL itself (used only if Video.VideoId is empty).
        private static string ExtractYouTubeId(Uri uri)
        {
            // youtu.be/<id>
            if (uri.Host.ToLowerInvariant().Contains("youtu.be"))
            {
                var path = uri.AbsolutePath.Trim('/');
                return string.IsNullOrEmpty(path) ? null : path;
            }

            // youtube.com/watch?v=<id>
            var query = uri.Query.TrimStart('?');
            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = pair.Split('=', 2);
                if (kv.Length == 2 && kv[0] == "v")
                    return Uri.UnescapeDataString(kv[1]);
            }

            // youtube.com/embed/<id> or /shorts/<id>
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            if (segments.Length == 2 && (segments[0] == "embed" || segments[0] == "shorts"))
                return segments[1];

            return null;
        }
    }
}
