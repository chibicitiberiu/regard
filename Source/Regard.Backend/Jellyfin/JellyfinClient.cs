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

    /// <summary>Per-user playback state Jellyfin tracks for an item.</summary>
    public class JellyfinUserData
    {
        /// <summary>Resume position in 100-ns ticks (10,000,000 per second).</summary>
        public long? PlaybackPositionTicks { get; set; }
        public bool Played { get; set; }
        public double? PlayedPercentage { get; set; }
        /// <summary>When the item was last played — the only Jellyfin-side timestamp for newer-wins.</summary>
        public DateTime? LastPlayedDate { get; set; }
    }

    public class JellyfinItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public JellyfinUserData UserData { get; set; }
    }

    public class JellyfinItemsResponse
    {
        public List<JellyfinItem> Items { get; set; }
    }

    public interface IJellyfinClient
    {
        /// <summary>Resolves a Jellyfin user id from its username (case-insensitive), or null if not found.</summary>
        Task<string> ResolveUserIdAsync(string username);

        /// <summary>
        /// Returns the user's video items with their file paths and per-user playback state (played +
        /// resume position). Unlike a played-only query this includes in-progress items, so positions can
        /// be reconciled both ways.
        /// </summary>
        Task<IReadOnlyList<JellyfinItem>> GetItemsWithUserDataAsync(string userId);

        /// <summary>
        /// Writes back the user's playback state for one item (resume position + played). Best-effort: a
        /// failure (offline, or an older Jellyfin that lacks the endpoint) returns false and never throws,
        /// so local state is never corrupted.
        /// </summary>
        Task<bool> UpdateUserDataAsync(string userId, string itemId, long positionTicks, bool played);

        /// <summary>Verifies the configured base URL + API key can reach Jellyfin.</summary>
        Task<bool> TestConnectionAsync();
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

        public async Task<IReadOnlyList<JellyfinItem>> GetItemsWithUserDataAsync(string userId)
        {
            // No IsPlayed filter: in-progress items must come back too so resume positions sync both ways.
            var response = await http.GetFromJsonAsync<JellyfinItemsResponse>(
                $"Users/{userId}/Items?Recursive=true&Fields=Path,UserData&IncludeItemTypes=Video,Movie,Episode");
            return response?.Items ?? new List<JellyfinItem>();
        }

        public async Task<bool> UpdateUserDataAsync(string userId, string itemId, long positionTicks, bool played)
        {
            try
            {
                // Jellyfin 10.9+ accepts the full user-data DTO here (older spelling:
                // /UserItems/{itemId}/UserData?userId=). Best-effort: any non-success is swallowed.
                var body = new { PlaybackPositionTicks = positionTicks, Played = played };
                var resp = await http.PostAsJsonAsync($"Users/{userId}/Items/{itemId}/UserData", body);
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            if (http.BaseAddress == null)
                return false;
            try
            {
                // System/Info requires a valid API key, so this validates both the URL and the key.
                var response = await http.GetAsync("System/Info");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
