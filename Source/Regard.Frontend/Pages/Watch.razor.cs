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

        // Downloaded-video playback finished: mark watched (no auto-advance).
        private async Task OnPlaybackEnded()
        {
            if (video != null && !video.IsWatched)
                await OnMarkWatched();
        }

        // The <video> couldn't load its source (e.g. the downloaded file was moved/removed while the DB
        // still flags it downloaded). Fall through to the same placeholder a not-downloaded video shows,
        // instead of leaving a dead black player. Not an error banner — this is a recoverable state.
        private void OnStreamError()
        {
            streamFailed = true;
            StateHasChanged();
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
