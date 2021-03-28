using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace Regard.Backend.Providers.YouTubeDL
{
    public static class YouTubeUrlHelper
    {
        public static Uri FixYouTubeChannelUri(Uri uri)
        {
            if (uri.Segments.Length < 2)
                return uri;

            if (!uri.Host.Equals("youtube.com", StringComparison.InvariantCultureIgnoreCase)
                && !uri.Host.EndsWith(".youtube.com", StringComparison.InvariantCultureIgnoreCase))
                return uri;

            string s1 = uri.Segments[1].Trim('/');
            bool isChannel = s1.Equals("channel", StringComparison.InvariantCultureIgnoreCase)
                || s1.Equals("c", StringComparison.InvariantCultureIgnoreCase)
                || s1.Equals("user", StringComparison.InvariantCultureIgnoreCase);

            if (isChannel)
            {
                string channelId = uri.Segments[2].Trim('/');
                var builder = new UriBuilder(uri);
                builder.Path = $"{s1}/{channelId}/videos";
                return builder.Uri;
            }

            // http://www.youtube.com/oembed?url=http%3A//www.youtube.com/watch?v%3D-wtIMTCHWuI&format=json
            else if (s1.Equals("oembed", StringComparison.InvariantCultureIgnoreCase))
            {
                var query = HttpUtility.ParseQueryString(uri.Query);
                string url = query.Get("url");
                if (url != null)
                    return FixYouTubeChannelUri(new Uri(url));
            }

            // http://www.youtube.com/attribution_link?a=JdfC0C9V6ZI&u=%2Fwatch%3Fv%3DEhxJLojIE_o%26feature%3Dshare
            else if (s1.Equals("attribution_link", StringComparison.InvariantCultureIgnoreCase))
            {
                var query = HttpUtility.ParseQueryString(uri.Query);
                string u = query.Get("u");
                if (u != null)
                    return FixYouTubeChannelUri(new Uri("https://youtube.com" + u));
            }           

            return uri;
        }
    }
}
