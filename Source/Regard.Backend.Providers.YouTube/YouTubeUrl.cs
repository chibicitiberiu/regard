using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Providers.YouTube
{
    public class YouTubeUrl
    {
        public YouTubeUrlType Type;
        public string VideoId;
        public string ListId;
        public string ChannelId;
        public string ChannelCustomId;
        public string UserId;
        public string Query;

        public static YouTubeUrl Video(string videoId, string listId = null) 
            => new YouTubeUrl() { Type = YouTubeUrlType.Video, VideoId = videoId, ListId = listId };

        public static YouTubeUrl Playlist(string listId)
            => new YouTubeUrl() { Type = YouTubeUrlType.Playlist, ListId = listId };
        
        public static YouTubeUrl Channel(string channelId)
            => new YouTubeUrl() { Type = YouTubeUrlType.Channel, ChannelId = channelId };

        public static YouTubeUrl ChannelCustom(string channelCustomId)
            => new YouTubeUrl() { Type = YouTubeUrlType.ChannelCustom, ChannelCustomId = channelCustomId };

        public static YouTubeUrl User(string userId)
            => new YouTubeUrl() { Type = YouTubeUrlType.User, UserId = userId };

        public static YouTubeUrl Search(string query)
            => new YouTubeUrl() { Type = YouTubeUrlType.Search, Query = query };
    }
}
