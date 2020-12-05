using Google.Apis.Util;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Regard.Backend.Providers.YouTube
{
    public class YouTubeAPIProxy
    {
        private readonly YouTubeService service;

        public YouTubeAPIProxy(string apiKey)
        {
            service = new YouTubeService(new Google.Apis.Services.BaseClientService.Initializer()
            {
                ApplicationName = "YouTube Helper",
                ApiKey = apiKey
            });
        }

        public async Task<Channel> FetchChannel(string channelId)
        {
            var request = service.Channels.List("id");
            request.Id = channelId;

            var response = await request.ExecuteAsync();
            return response.Items.FirstOrDefault();
        }

        public async Task<Channel> FetchChannelByUserId(string user)
        {
            var request = service.Channels.List("id");
            request.ForUsername = user;

            var response = await request.ExecuteAsync();
            return response.Items.FirstOrDefault();
        }

        public async Task<SearchResult> FetchChannelByCustomId(string customId)
        {
            // See https://stackoverflow.com/a/37947865
            //
            // Using the YT API, the only way to obtain a channel using a custom URL that we know of is to search for it.
            // Another option (which might be more reliable) could be scraping the page

            var searchRequest = service.Search.List("id");
            searchRequest.Type = "channel";
            searchRequest.Q = customId;

            var searchResponse = await searchRequest.ExecuteAsync();
            var searchResult = searchResponse.Items.FirstOrDefault();
            return searchResult;
        }

        public async Task<Playlist> FetchPlaylist(string listId)
        {
            var request = service.Playlists.List("id");
            request.Id = listId;

            var response = await request.ExecuteAsync();
            return response.Items.FirstOrDefault();
        }

        public async IAsyncEnumerable<PlaylistItem> GetPlaylistVideos(string listId)
        {
            var request = service.PlaylistItems.List("id");
            request.Id = listId;

            do
            {
                var response = await request.ExecuteAsync();
                foreach (var item in response.Items)
                    yield return item;

                request.PageToken = response.NextPageToken;
            } while (request.PageToken != null);
        }

        public IAsyncEnumerable<Video> GetVideos(IEnumerable<string> videoIds, params string[] parts)
        {
            return GetVideos(videoIds, parts);
        }

        public async IAsyncEnumerable<Video> GetVideos(IEnumerable<string> videoIds, IEnumerable<string> parts)
        {
            var request = service.Videos.List(parts.Aggregate((x, y) => x + "," + y));
            request.Id = videoIds.Aggregate((x, y) => x + ',' + y);

            do
            {
                var response = await request.ExecuteAsync();
                foreach (var item in response.Items)
                    yield return item;

                request.PageToken = response.NextPageToken;
            } while (request.PageToken != null);
        }

        public async IAsyncEnumerable<SearchResult> GetSearchResults(string query, string type)
        {
            var request = service.Search.List("id");
            request.Type = type;
            request.Q = query;

            do
            {
                var response = await request.ExecuteAsync();
                foreach (var item in response.Items)
                    yield return item;

                request.PageToken = response.NextPageToken;
            } while (request.PageToken != null);
        }
    }
}
