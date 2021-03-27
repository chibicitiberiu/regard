using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Regard.Backend.Providers.YouTube
{
    public static class YouTubeUrlHelper
    {
        public static YouTubeUrl ParseUrl(Uri uri)
        {
            if (uri.Segments.Length < 2)
                throw new ArgumentException("Invalid URL.");
            string s1 = uri.Segments[1].Trim('/');

            if (uri.Host.EndsWith("youtube.com", StringComparison.InvariantCultureIgnoreCase))
            {
                // http://www.youtube.com/watch?v=-wtIMTCHWuI
                if (s1.Equals("watch", StringComparison.InvariantCultureIgnoreCase))
                {
                    var query = HttpUtility.ParseQueryString(uri.Query);
                    string video = query.Get("v");
                    string list = query.Get("list");

                    if (video == null)
                        throw new ArgumentException("Invalid URL.");

                    return YouTubeUrl.Video(video, list);
                }

                // http://www.youtube.com/v/-wtIMTCHWuI?version=3&autohide=1
                // https://www.youtube.com/embed/M7lc1UVf-VE
                else if (s1.Equals("v", StringComparison.InvariantCultureIgnoreCase)
                    || s1.Equals("embed", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (uri.Segments.Length < 3)
                        throw new ArgumentException("Invalid URL.");
                    string video = uri.Segments[2];

                    return YouTubeUrl.Video(video);
                }

                // https://www.youtube.com/playlist?list=PLJRbJuI_csVDXhgRJ1xv6z-Igeb7CKroe
                else if (s1.Equals("playlist", StringComparison.InvariantCultureIgnoreCase))
                {
                    var query = HttpUtility.ParseQueryString(uri.Query);
                    string list = query.Get("list");

                    if (list == null)
                        throw new ArgumentException("Invalid URL.");

                    return YouTubeUrl.Playlist(list);
                }

                // https://www.youtube.com/channel/UC0QHWhjbe5fGJEPz3sVb6nw
                else if (s1.Equals("channel", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (uri.Segments.Length < 3)
                        throw new ArgumentException("Invalid URL.");
                    string channel = uri.Segments[2];

                    return YouTubeUrl.Channel(channel);
                }

                // https://www.youtube.com/c/LinusTechTips
                else if (s1.Equals("c", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (uri.Segments.Length < 3)
                        throw new ArgumentException("Invalid URL.");
                    string channel = uri.Segments[2];

                    return YouTubeUrl.ChannelCustom(channel);
                }

                // https://www.youtube.com/user/LinusTechTips
                else if (s1.Equals("user", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (uri.Segments.Length < 3)
                        throw new ArgumentException("Invalid URL.");
                    string user = uri.Segments[2];

                    return YouTubeUrl.User(user);
                }

                // http://www.youtube.com/oembed?url=http%3A//www.youtube.com/watch?v%3D-wtIMTCHWuI&format=json
                else if (s1.Equals("oembed", StringComparison.InvariantCultureIgnoreCase))
                {
                    var query = HttpUtility.ParseQueryString(uri.Query);
                    string url = query.Get("url");

                    if (url == null)
                        throw new ArgumentException("Invalid URL.");

                    return ParseUrl(new Uri(url));
                }

                // http://www.youtube.com/attribution_link?a=JdfC0C9V6ZI&u=%2Fwatch%3Fv%3DEhxJLojIE_o%26feature%3Dshare
                else if (s1.Equals("attribution_link", StringComparison.InvariantCultureIgnoreCase))
                {
                    var query = HttpUtility.ParseQueryString(uri.Query);
                    string u = query.Get("u");

                    if (u == null)
                        throw new ArgumentException("Invalid URL.");

                    return ParseUrl(new Uri("http://youtube.com" + u));
                }

                // https://www.youtube.com/results?search_query=test
                else if (s1.Equals("results", StringComparison.InvariantCultureIgnoreCase))
                {
                    var query = HttpUtility.ParseQueryString(uri.Query);
                    string q = query.Get("search_query");

                    if (q == null)
                        throw new ArgumentException("Invalid URL.");

                    return YouTubeUrl.Search(q);
                }

                // https://www.youtube.com/feeds/videos.xml?channel_id=UC0QHWhjbe5fGJEPz3sVb6nw
                // https://www.youtube.com/feeds/videos.xml?playlist_id=PLQMVnqe4XbictUtFZK1-gBYvyUzTWJnOk
                else if (s1.Equals("feeds", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (uri.Segments.Length < 3)
                        throw new ArgumentException("Invalid URL.");

                    if (uri.Segments[2].Equals("videos.xml", StringComparison.InvariantCultureIgnoreCase))
                    {
                        var query = HttpUtility.ParseQueryString(uri.Query);
                        string channel_id = query.Get("channel_id");
                        string playlist_id = query.Get("playlist_id");

                        if (channel_id != null)
                            return YouTubeUrl.Channel(channel_id);

                        else if (playlist_id != null)
                            return YouTubeUrl.Playlist(playlist_id);
                    }
                }
                // Custom channel URLs might have the format https://www.youtube.com/LinusTechTips, which are pretty much
                // impossible to handle properly
            }

            // http://youtu.be/-wtIMTCHWuI
            else if (uri.Host.EndsWith("youtu.be", StringComparison.InvariantCultureIgnoreCase))
                return YouTubeUrl.Video(s1);

            // https://youtube.googleapis.com/v/My2FRPA3Gf8
            else if (uri.Host.EndsWith("youtube.googleapis.com", StringComparison.InvariantCultureIgnoreCase)
                && s1.Equals("v", StringComparison.InvariantCultureIgnoreCase))
            {
                if (uri.Segments.Length < 3)
                    throw new ArgumentException("Invalid URL.");

                return YouTubeUrl.Video(uri.Segments[2]);
            }

            throw new ArgumentException("Unrecognized URL format");
        }
    }
}
