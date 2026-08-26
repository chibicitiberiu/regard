using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Regard.Backend.Jellyfin
{
    public class JellyfinUser
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public class JellyfinItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
    }

    public class JellyfinItemsResponse
    {
        public List<JellyfinItem> Items { get; set; }
    }

    public interface IJellyfinClient
    {
        /// <summary>Resolves a Jellyfin user id from its username (case-insensitive), or null if not found.</summary>
        Task<string> ResolveUserIdAsync(string username);

        /// <summary>Returns all items the given user has marked played (with their file paths).</summary>
        Task<IReadOnlyList<JellyfinItem>> GetPlayedItemsAsync(string userId);
    }

    /// <summary>
    /// Minimal typed client for the Jellyfin REST API, used by <see cref="Jobs.JellyfinSyncJob"/>
    /// to read played state. Authenticated with an admin API key via the X-Emby-Token header.
    /// </summary>
    public class JellyfinClient : IJellyfinClient
    {
        private readonly HttpClient http;

        public JellyfinClient(HttpClient http, IConfiguration configuration)
        {
            this.http = http;

            var baseUrl = configuration["Jellyfin:BaseUrl"];
            if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
            {
                // Trailing slash so relative request URIs combine onto the host, not replace it.
                http.BaseAddress = new Uri(baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/");
                http.DefaultRequestHeaders.Add("X-Emby-Token", configuration["Jellyfin:ApiKey"]);
            }
        }

        public async Task<string> ResolveUserIdAsync(string username)
        {
            // GET /Users returns a bare JSON array of all users (admin key).
            var users = await http.GetFromJsonAsync<List<JellyfinUser>>("Users");
            return users?
                .FirstOrDefault(u => string.Equals(u.Name, username, StringComparison.OrdinalIgnoreCase))?
                .Id;
        }

        public async Task<IReadOnlyList<JellyfinItem>> GetPlayedItemsAsync(string userId)
        {
            var response = await http.GetFromJsonAsync<JellyfinItemsResponse>(
                $"Users/{userId}/Items?Filters=IsPlayed&Recursive=true&Fields=Path&IncludeItemTypes=Video,Movie,Episode");
            return response?.Items ?? new List<JellyfinItem>();
        }
    }
}
