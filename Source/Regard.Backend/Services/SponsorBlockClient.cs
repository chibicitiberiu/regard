using Microsoft.Extensions.Logging;
using Regard.Common.API.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    /// <summary>
    /// Fetches SponsorBlock "skip" segments for a YouTube video from the public API
    /// (https://sponsor.ajay.app). Best-effort: any failure (network, 404 = no segments, malformed) yields
    /// an empty list. Data is CC BY-NC-SA — the watch page shows a SponsorBlock credit when it uses this.
    /// </summary>
    public class SponsorBlockClient
    {
        private readonly HttpClient http;
        private readonly ILogger<SponsorBlockClient> log;

        public SponsorBlockClient(HttpClient http, ILogger<SponsorBlockClient> log)
        {
            this.http = http;
            this.log = log;
        }

        // Shape of a segment in the API response.
        private class SbSegment
        {
            [JsonPropertyName("segment")] public double[] Segment { get; set; }
            [JsonPropertyName("category")] public string Category { get; set; }
            [JsonPropertyName("actionType")] public string ActionType { get; set; }
        }

        /// <summary>
        /// Fetches the segments that were (or would be) cut out of a file by yt-dlp's
        /// --sponsorblock-remove, so they can be recorded against the download.
        ///
        /// Same API and same action type as the skip lookup — yt-dlp's "remove" is the skip action
        /// applied destructively — so this returns what yt-dlp saw, give or take any submissions made in
        /// the seconds between the two calls. Call it at download time and never again: once a file is
        /// cut, later versions of this data no longer describe it.
        /// </summary>
        public Task<List<ApiSponsorSegment>> GetRemovedSegments(string videoId, IEnumerable<string> categories)
            => GetSkipSegments(videoId, categories);

        public async Task<List<ApiSponsorSegment>> GetSkipSegments(string videoId, IEnumerable<string> categories)
        {
            var cats = categories?.ToList() ?? new List<string>();
            if (string.IsNullOrEmpty(videoId) || cats.Count == 0)
                return new List<ApiSponsorSegment>();

            try
            {
                // Direct lookup (simple; a personal server already talks to YouTube, so the hash-prefix
                // privacy endpoint buys little here). categories is a JSON array; actionType=skip only.
                var catsJson = JsonSerializer.Serialize(cats);
                var url = $"/api/skipSegments?videoID={Uri.EscapeDataString(videoId)}"
                        + $"&categories={Uri.EscapeDataString(catsJson)}"
                        + "&actionType=skip";

                using var resp = await http.GetAsync(url);
                if (resp.StatusCode == HttpStatusCode.NotFound)
                    return new List<ApiSponsorSegment>();   // documented "no segments" response
                if (!resp.IsSuccessStatusCode)
                {
                    log.LogDebug("SponsorBlock lookup for {0} returned {1}", videoId, (int)resp.StatusCode);
                    return new List<ApiSponsorSegment>();
                }

                var segments = await resp.Content.ReadFromJsonAsync<List<SbSegment>>();
                return (segments ?? new List<SbSegment>())
                    .Where(s => s.Segment != null && s.Segment.Length == 2 && s.Segment[1] > s.Segment[0])
                    .Select(s => new ApiSponsorSegment { Start = s.Segment[0], End = s.Segment[1], Category = s.Category })
                    .ToList();
            }
            catch (Exception ex)
            {
                log.LogDebug(ex, "SponsorBlock lookup failed for {0}", videoId);
                return new List<ApiSponsorSegment>();
            }
        }
    }
}
