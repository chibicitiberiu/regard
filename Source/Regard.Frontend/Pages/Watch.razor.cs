using Microsoft.AspNetCore.Components;
using Regard.Common.API.Model;
using Regard.Common.API.Subscriptions;
using Regard.Frontend.Utils;
using Regard.Model;
using Regard.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Pages
{
    public partial class Watch
    {
        private const int UpNextTarget = 8;

        private ApiVideo video;
        private ApiSubscription subscription;
        private Uri videoStreamUri;
        private string errorMessage;
        private string watchOnHost;      // null when OriginalUrl isn't a usable http(s) link
        private bool embeddingAllowed;   // effective user setting, for the placeholder message
        private bool downloadQueued;
        private bool streamFailed;        // the downloaded file wouldn't load (missing/unreadable) -> show the fallback
        private List<ApiVideo> upNext;
        private Regard.Frontend.Shared.Controls.Video playerRef;   // set when the downloaded <video> is shown
        private double currentPlaybackSeconds;                     // driven by playback, for chapter highlight

        // Chapters exist to show at all.
        private bool HasChapters => video?.Chapters != null && video.Chapters.Count > 0;

        // The downloaded <video> player is active (so we can drive currentTime) and its timeline still
        // matches the chapters' original timeline (SponsorBlock didn't cut it). Only then is click-to-seek
        // meaningful; the YouTube embed and trimmed files show the list read-only.
        private bool ChaptersSeekable => HasChapters && video.IsDownloaded && !streamFailed && !video.SponsorsRemoved;

        // Resume point handed to the player: only for an unwatched video that has one.
        private double ResumeFromSeconds =>
            (video != null && !video.IsWatched && video.PlaybackPositionSeconds.HasValue)
                ? video.PlaybackPositionSeconds.Value : 0;

        [Inject] protected BackendService Backend { get; set; }

        [Inject] protected AppState AppState { get; set; }

        [Parameter] public int VideoId { get; set; }

        public MarkupString FormattedDescription { get; set; }

        protected override async Task OnParametersSetAsync()
        {
            await base.OnParametersSetAsync();
            // Reset so navigating between /watch/{id} entries (e.g. from Up next) reloads cleanly.
            video = null;
            subscription = null;
            upNext = null;
            errorMessage = null;
            watchOnHost = null;
            downloadQueued = false;
            streamFailed = false;

            var (resp, httpResp) = await Backend.VideoList(new VideoListRequest() { Ids = new[] { VideoId } });
            if (!httpResp.IsSuccessStatusCode)
            {
                errorMessage = "An error occurred while getting video details: " + resp.Message;
                return;
            }

            video = resp.Data.Videos.FirstOrDefault();
            if (video == null)
            {
                errorMessage = "An error occurred while getting video details.";
                return;
            }

            FormattedDescription = new MarkupString((video.Description ?? "").FormatAsHtml());
            watchOnHost = HostOf(video.OriginalUrl);
            videoStreamUri = await Backend.VideoViewUrl(VideoId);

            // Effective embedding preference (default off) — only used to tailor the placeholder text.
            var settings = await Backend.GetSettings();
            embeddingAllowed = settings?.Data?.AllowEmbedding ?? false;

            // Channel avatar (ApiVideo carries none).
            var (subResp, subHttp) = await Backend.SubscriptionList(
                new SubscriptionListRequest() { Ids = new[] { video.SubscriptionId } });
            if (subHttp.IsSuccessStatusCode)
            {
                subscription = subResp.Data.Subscriptions.FirstOrDefault();
                if (subscription?.ThumbnailUrl != null && !subscription.ThumbnailUrl.IsAbsoluteUri)
                    subscription.ThumbnailUrl = new Uri(AppState.BackendBase, subscription.ThumbnailUrl);
            }

            await LoadUpNext();
        }

        // Builds the "Up next" queue: 5-10 unwatched videos, priority
        // (1) same subscription (next then previous), (2) same folder, (3) everything.
        private async Task LoadUpNext()
        {
            var result = new List<ApiVideo>();
            var seen = new HashSet<int> { VideoId };

            void AddFrom(IEnumerable<ApiVideo> vids)
            {
                foreach (var v in vids)
                {
                    if (result.Count >= UpNextTarget) break;
                    if (seen.Add(v.Id)) result.Add(v);
                }
            }

            // (1) Same subscription, unwatched. Newest-first, so "next" (older than the current one)
            // comes first, then wrap around to the newer ones.
            var (subResp, subHttp) = await Backend.VideoList(new VideoListRequest
            {
                SubscriptionId = video.SubscriptionId,
                IsWatched = false,
                Order = VideoOrder.Newest,
                Limit = 50,
            });
            if (subHttp.IsSuccessStatusCode && subResp?.Data?.Videos != null)
            {
                var subs = subResp.Data.Videos.Where(v => v.Id != VideoId).ToList();
                AddFrom(subs.Where(v => v.Published <= video.Published));
                AddFrom(subs.Where(v => v.Published > video.Published));
            }

            // (2) Same folder.
            if (result.Count < UpNextTarget && subscription?.ParentFolderId != null)
            {
                var (fResp, fHttp) = await Backend.VideoList(new VideoListRequest
                {
                    SubscriptionFolderId = subscription.ParentFolderId,
                    IsWatched = false,
                    Order = VideoOrder.Newest,
                    Limit = 50,
                });
                if (fHttp.IsSuccessStatusCode && fResp?.Data?.Videos != null)
                    AddFrom(fResp.Data.Videos);
            }

            // (3) Anything unwatched.
            if (result.Count < UpNextTarget)
            {
                var (aResp, aHttp) = await Backend.VideoList(new VideoListRequest
                {
                    IsWatched = false,
                    Order = VideoOrder.Newest,
                    Limit = 50,
                });
                if (aHttp.IsSuccessStatusCode && aResp?.Data?.Videos != null)
                    AddFrom(aResp.Data.Videos);
            }

            upNext = result;
        }

        private async Task OnDownloadNow()
        {
            var (resp, http) = await Backend.VideoDownload(new VideoDownloadRequest { VideoIds = new[] { VideoId } });
            if (http.IsSuccessStatusCode)
                downloadQueued = true;
            else
                errorMessage = "Failed to queue download: " + resp?.Message;
        }

        private async Task OnMarkWatched()
        {
            var (_, http) = await Backend.VideoMarkWatched(new VideoMarkWatchedRequest { VideoIds = new[] { VideoId } });
            if (http.IsSuccessStatusCode)
                video.IsWatched = true;
        }

        private async Task OnMarkNotWatched()
        {
            var (_, http) = await Backend.VideoMarkNotWatched(new VideoMarkNotWatchedRequest { VideoIds = new[] { VideoId } });
            if (http.IsSuccessStatusCode)
                video.IsWatched = false;
        }

        // Fired once the playhead crosses ~90% of the downloaded video (and again on end as a fallback):
        // mark it watched. Idempotent — only marks if not already watched, and never runs on page open.
        private async Task OnMarkWatchedFromPlayback()
        {
            if (video != null && !video.IsWatched)
                await OnMarkWatched();
        }

        // Throttled playback-position callback (also fires on pause/ended and on the player's dispose).
        // Persist the resume point and keep the chapter highlight in sync. Skips once watched, so a video
        // that just crossed the 90% watched mark doesn't immediately get a fresh resume position.
        private async Task OnPositionReport(double seconds)
        {
            currentPlaybackSeconds = seconds;
            if (video == null || video.IsWatched)
                return;

            int secs = (int)seconds;
            video.PlaybackPositionSeconds = secs;
            await Backend.VideoReportProgress(new VideoReportProgressRequest { VideoId = VideoId, PositionSeconds = secs });
        }

        // The <video> couldn't load its source (e.g. the downloaded file was moved/removed while the DB
        // still flags it downloaded). Fall through to the same placeholder a not-downloaded video shows,
        // instead of leaving a dead black player. Not an error banner — this is a recoverable state.
        private void OnStreamError()
        {
            streamFailed = true;
            StateHasChanged();
        }

        // Click a chapter row: seek the downloaded player to its start (no-op when not seekable).
        private async Task OnChapterClicked(ApiChapter chapter)
        {
            if (ChaptersSeekable && playerRef != null)
            {
                await playerRef.SeekTo(chapter.Start);
                currentPlaybackSeconds = chapter.Start;
            }
        }

        // Index of the chapter covering the current playhead, or -1. Used to highlight the active row.
        private int ActiveChapterIndex
        {
            get
            {
                if (!HasChapters) return -1;
                var t = currentPlaybackSeconds;
                for (int i = video.Chapters.Count - 1; i >= 0; i--)
                {
                    if (t >= video.Chapters[i].Start) return i;
                }
                return -1;
            }
        }

        private static string FormatTimestamp(double seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds < 0 ? 0 : seconds);
            return ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}:{ts.Minutes:00}:{ts.Seconds:00}"
                : $"{ts.Minutes}:{ts.Seconds:00}";
        }

        private string ThumbUrl(ApiVideo v)
        {
            if (v.ThumbnailUrl == null)
                return null;
            return v.ThumbnailUrl.IsAbsoluteUri
                ? v.ThumbnailUrl.ToString()
                : new Uri(AppState.BackendBase, v.ThumbnailUrl).ToString();
        }

        // Human-friendly host for the "Watch on X" link; null if the URL isn't a usable web link.
        private static string HostOf(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;
            try
            {
                var uri = new Uri(url);
                if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                    return null;
                var host = uri.Host.ToLowerInvariant();
                if (host.StartsWith("www.")) host = host.Substring(4);
                return host;
            }
            catch
            {
                return null;
            }
        }
    }
}
