using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    public class RydVotes
    {
        [JsonPropertyName("likes")] public long Likes { get; set; }
        [JsonPropertyName("dislikes")] public long Dislikes { get; set; }
        [JsonPropertyName("rating")] public double Rating { get; set; }
        [JsonPropertyName("viewCount")] public long ViewCount { get; set; }
    }

    /// <summary>
    /// Fetches real dislike estimates for a YouTube video from the ReturnYouTubeDislike API
    /// (returnyoutubedislikeapi.com). Best-effort: any failure (network, 429 rate-limit, 404, malformed)
    /// yields null. Attribution to returnyoutubedislike.com is shown wherever the data is displayed.
    /// Limits: 100 req/min, 10 000/day — Regard only calls this once per watch-page open.
    /// </summary>
    public class ReturnYouTubeDislikeClient
    {
        private readonly HttpClient http;
        private readonly ILogger<ReturnYouTubeDislikeClient> log;

        public ReturnYouTubeDislikeClient(HttpClient http, ILogger<ReturnYouTubeDislikeClient> log)
        {
            this.http = http;
            this.log = log;
        }

        public async Task<RydVotes> GetVotes(string videoId)
        {
            if (string.IsNullOrEmpty(videoId))
                return null;

            try
            {
                using var resp = await http.GetAsync($"/votes?videoId={Uri.EscapeDataString(videoId)}");
                if (!resp.IsSuccessStatusCode)
                {
                    if (resp.StatusCode == (HttpStatusCode)429)
                        log.LogWarning("ReturnYouTubeDislike rate-limited (429); skipping {0}", videoId);
                    else
                        log.LogDebug("ReturnYouTubeDislike returned {0} for {1}", (int)resp.StatusCode, videoId);
                    return null;
                }
                return await resp.Content.ReadFromJsonAsync<RydVotes>();
            }
            catch (Exception ex)
            {
                log.LogDebug(ex, "ReturnYouTubeDislike lookup failed for {0}", videoId);
                return null;
            }
        }
    }
}
