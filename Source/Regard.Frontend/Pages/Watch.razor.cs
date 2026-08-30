using Humanizer;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
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
    public partial class Watch : IDisposable, IAsyncDisposable
    {
        private const int UpNextTarget = 8;

        /// <summary>Where the last picked subtitle language is remembered. "off" is a real stored value.</summary>
        private const string SubtitleLangStorageKey = "regard.subtitleLang";
        private const string SubtitleOff = "off";

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

        // --- Subtitles ---------------------------------------------------------------------------
        // Track URLs by language, built once per video. The <track> elements are all mounted so the
        // browser shows its native CC control in the player toolbar; it fetches a track's cues only when
        // that track is switched on, so mounting them all is not a download.
        private Dictionary<string, string> subtitleTrackUrls;
        private DotNetObjectReference<Watch> subtitleSelfRef;
        private bool subtitlePreferenceApplied;
        private bool subtitleMenuVisible;
        private ElementReference subtitleButton;
        // The language currently showing, kept in step with the tracks themselves — it changes both from
        // our own menu and from the browser's built-in captions menu, and OnTextTrackChanged is what
        // keeps the two in agreement.
        private string activeTrackLang;

        private string ActiveTrackLabel =>
            video?.SubtitleTracks?.FirstOrDefault(t => t.Lang == activeTrackLang)?.Label ?? "Subtitles";

        private bool HasSubtitles => video?.SubtitleTracks != null && video.SubtitleTracks.Count > 0;

        // Only meaningful for the local player; the YouTube embed brings its own captions.
        private bool SubtitlesAvailable => HasSubtitles && video.IsDownloaded && !streamFailed;

        /// <summary>
        /// The dislike count to show. Prefers the exact number Return YouTube Dislike returned on this
        /// fetch, and falls back to deriving it from the like count and the stored ratio.
        ///
        /// The fallback is what keeps the two numbers consistent: a live push carries Likes (a persisted
        /// column) but not Dislikes (a single-fetch enrichment), so after any unrelated update — marking
        /// watched, a download finishing — the served Dislikes goes null while Likes may have moved.
        /// Re-deriving means the pair always describes the same moment rather than pinning a fresh like
        /// count next to a stale dislike count.
        ///
        /// From rating = likes / (likes + dislikes): dislikes = likes * (1 - rating) / rating. Computed
        /// in double; Video.Rating is a float, so a ratio above ~0.9999 rounds to 1 and yields zero.
        /// </summary>
        private long? DislikeEstimate
        {
            get
            {
                if (video == null)
                    return null;
                if (video.Dislikes.HasValue)
                    return video.Dislikes;
                if (!video.Likes.HasValue || !video.Rating.HasValue)
                    return null;

                double rating = video.Rating.Value;
                if (rating <= 0d || rating >= 1d)
                    return null;

                return (long)System.Math.Round(video.Likes.Value * (1d - rating) / rating);
            }
        }

        /// <summary>The liked share as a percentage, e.g. "97% liked".</summary>
        private string LikedPercent =>
            video?.Rating is float r ? $"{(r * 100d).ToString("0.#")}% liked" : null;

        // Chapters exist to show at all.
        private bool HasChapters => video?.Chapters != null && video.Chapters.Count > 0;

        // The downloaded <video> player is active (so we can drive currentTime) and its timeline still
        // matches the chapters' original timeline (SponsorBlock didn't cut it). Only then is click-to-seek
        // meaningful; the YouTube embed and trimmed files show the list read-only.
        private bool ChaptersSeekable => HasChapters && video.IsDownloaded && !streamFailed && !video.SponsorsRemoved;

        private const int MinInProgressSeconds = Regard.Model.PlaybackConstants.MinInProgressSeconds;
        private const int ResumeRewindSeconds = Regard.Model.PlaybackConstants.ResumeRewindSeconds;

        // Resume point handed to the player: only for an unwatched video that's actually "in progress"
        // (>= MinInProgressSeconds), rewound a little for context (clamped to 0).
        private double ResumeFromSeconds =>
            (video != null && !video.IsWatched
                && video.PlaybackPositionSeconds.HasValue
                && video.PlaybackPositionSeconds.Value >= MinInProgressSeconds)
                ? System.Math.Max(0, video.PlaybackPositionSeconds.Value - ResumeRewindSeconds)
                : 0;

        [Inject] protected BackendService Backend { get; set; }

        [Inject] protected AppState AppState { get; set; }

        [Inject] protected Regard.Frontend.Services.MessagingService Messaging { get; set; }

        [Inject] protected Regard.Frontend.Services.NotificationsService Notifications { get; set; }

        [Inject] protected Microsoft.JSInterop.IJSRuntime JS { get; set; }

        [Inject] protected Blazored.LocalStorage.ILocalStorageService LocalStorage { get; set; }

        [Parameter] public int VideoId { get; set; }

        /// <summary>
        /// @handles and #hashtags only mean something on YouTube, so they're linkified only there. The
        /// description itself is rendered by DescriptionText straight from video.Description, which is why
        /// a live description update now repaints (it merges, and Text is a parameter).
        /// </summary>
        private bool LinkifyYouTube =>
            watchOnHost != null && watchOnHost.Contains("youtu", StringComparison.OrdinalIgnoreCase);

        protected override void OnInitialized()
        {
            base.OnInitialized();
            // Until now the watch page ignored live updates entirely, so a download finishing (or the
            // video being deleted) while you sat here was invisible until you navigated away.
            Messaging.VideoUpdated += Messaging_VideoUpdated;
            Messaging.VideosChanged += Messaging_VideosChanged;
            // Download progress arrives as notifications, which fire neither of the two events above.
            Notifications.ActivityChanged += OnNotificationsActivityChanged;
        }

        public void Dispose()
        {
            Messaging.VideoUpdated -= Messaging_VideoUpdated;
            Messaging.VideosChanged -= Messaging_VideosChanged;
            Notifications.ActivityChanged -= OnNotificationsActivityChanged;
        }

        /// <summary>
        /// Releases the subtitle blob. Blazor calls both Dispose and DisposeAsync when a component
        /// implements both; revoking needs JS interop, which a synchronous Dispose can't await.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            subtitleSelfRef?.Dispose();
            subtitleSelfRef = null;
            await Task.CompletedTask;
        }

        /// <summary>
        /// Builds the &lt;track&gt; URLs for this video. Called once per video load; the browser decides
        /// when (and whether) to fetch each one.
        /// </summary>
        private async Task BuildSubtitleTrackUrls()
        {
            subtitleTrackUrls = null;
            subtitlePreferenceApplied = false;
            activeTrackLang = null;
            subtitleMenuVisible = false;

            if (!SubtitlesAvailable)
                return;

            var urls = new Dictionary<string, string>();
            foreach (var track in video.SubtitleTracks)
                urls[track.Lang] = (await Backend.VideoSubtitleUrl(VideoId, track.Lang)).ToString();

            subtitleTrackUrls = urls;
        }

        /// <summary>
        /// Turns the remembered language on once the track elements exist, and starts listening for the
        /// viewer changing it from the player's own CC menu so the choice is remembered either way.
        /// </summary>
        private async Task ApplyStoredSubtitlePreference()
        {
            string stored = null;
            try { stored = await LocalStorage.GetItemAsync<string>(SubtitleLangStorageKey); }
            catch (Exception) { /* storage unavailable (private mode) — just leave subtitles off */ }

            if (stored == SubtitleOff)
                stored = null;

            subtitleSelfRef ??= DotNetObjectReference.Create(this);

            try
            {
                if (await playerRef.BindTextTracks(subtitleSelfRef, stored))
                    activeTrackLang = stored;
                subtitlePreferenceApplied = true;
            }
            catch (Exception)
            {
                // Player torn down mid-flight; the next render will try again.
            }
        }

        /// <summary>
        /// The viewer switched tracks in the player's native CC menu. Remember it, so the next video that
        /// has the language starts with it on. "off" is stored as a real value rather than as an absence,
        /// so turning subtitles off actually sticks.
        /// </summary>
        [JSInvokable]
        public async Task OnTextTrackChanged(string language)
        {
            activeTrackLang = language;
            StateHasChanged();          // keeps our CC button in step with the browser's own menu

            try { await LocalStorage.SetItemAsync(SubtitleLangStorageKey, language ?? SubtitleOff); }
            catch (Exception) { /* not fatal — the choice just won't be remembered */ }
        }

        /// <summary>Switches the showing track from our CC menu. Null turns subtitles off.</summary>
        private async Task SelectSubtitleTrack(string lang)
        {
            subtitleMenuVisible = false;
            if (playerRef == null)
                return;

            // Setting the mode fires the tracks' `change` event, which comes back through
            // OnTextTrackChanged — so state and persistence are handled in exactly one place regardless
            // of which menu the viewer used.
            await playerRef.SetTextTrack(lang);
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            // Tracks Blazor inserts start out "disabled" whatever their `default` attribute says, so the
            // stored preference has to be applied once the elements actually exist.
            if (!subtitlePreferenceApplied && subtitleTrackUrls != null && playerRef != null)
                await ApplyStoredSubtitlePreference();
        }

        private void OnNotificationsActivityChanged(object sender, EventArgs e) => InvokeAsync(StateHasChanged);

        /// <summary>
        /// The in-flight download card for this video, or null. Looked up per render rather than held:
        /// a progress tick REPLACES the object in the collection, so a cached reference would freeze at
        /// the first value.
        /// </summary>
        private ApiNotification DownloadNotification =>
            Notifications?.Notifications?.FirstOrDefault(n => n.Ongoing && n.VideoId == VideoId);

        private void Messaging_VideoUpdated(object sender, ApiVideo e)
        {
            // Merge, never replace: a pushed DTO carries no EmbedUrl/StreamMimeType/SponsorSegments,
            // so assigning it wholesale would tear those out from under the running player.
            if (video != null && e.Id == video.Id)
            {
                bool wasDownloaded = video.IsDownloaded;
                video.MergeLiveFields(e);

                // A download just finished while we were sitting here. The merge deliberately doesn't
                // carry StreamMimeType (it's filled only by VideoList), so rendering the player now would
                // emit <source type=""> and it wouldn't play — the exact moment this feature exists for.
                // Re-fetch to fill it in. Strictly on the false->true EDGE: a level check would keep
                // firing against the pushes this page's own RYD write produces.
                if (!wasDownloaded && video.IsDownloaded)
                {
                    _ = InvokeAsync(RefreshAfterDownload);
                    return;
                }

                StateHasChanged();
                return;
            }

            if (upNext != null)
            {
                var existing = upNext.FirstOrDefault(v => v.Id == e.Id);
                if (existing != null)
                {
                    existing.MergeLiveFields(e);
                    StateHasChanged();
                }
            }
        }

        /// <summary>
        /// Re-reads the single video after its download completes, so the fields that only
        /// VideoController.List produces (StreamMimeType above all) are present before the player
        /// renders. Deliberately does not touch videoStreamUri — OnParametersSetAsync owns it and it
        /// doesn't change when a file lands.
        ///
        /// Subtitle tracks are in the same category and DO appear for the first time here (the sidecars
        /// only exist once the download finishes), so the remembered language is applied again.
        /// </summary>
        private async Task RefreshAfterDownload()
        {
            try
            {
                var (resp, http) = await Backend.VideoList(new VideoListRequest() { Ids = new[] { VideoId } });
                var fresh = http.IsSuccessStatusCode ? resp?.Data?.Videos?.FirstOrDefault() : null;
                if (fresh != null)
                    video = fresh;
            }
            catch (Exception)
            {
                // Keep the merged copy; the player may still work once the user reloads.
            }

            // Sidecars only exist once the download finishes, so this is where a video's tracks first
            // appear. RefreshAfterDownload deliberately leaves videoStreamUri alone, but the track URLs
            // are not in that category — they have to be built now or the picker never shows up.
            await BuildSubtitleTrackUrls();

            StateHasChanged();
        }

        private void Messaging_VideosChanged(object sender, int subscriptionId)
        {
            // The surrounding set changed (a sync added videos); refresh what plays next.
            if (video != null && subscriptionId == video.SubscriptionId)
                _ = InvokeAsync(async () => { await LoadUpNext(); StateHasChanged(); });
        }

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

            // Navigating between watch pages reuses this component (only the route parameter changes),
            // so the previous video's tracks have to be cleared here — Dispose won't run.
            subtitleTrackUrls = null;
            subtitlePreferenceApplied = false;

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

            watchOnHost = HostOf(video.OriginalUrl);
            videoStreamUri = await Backend.VideoViewUrl(VideoId);
            await BuildSubtitleTrackUrls();

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

            // Downloaded first — they play instantly, the rest need a fetch. Applied AFTER the cascade,
            // never inside it: OrderBy is stable, so same-subscription items still precede folder and
            // global ones within each group, and the "next episode" ordering of tier 1 survives.
            upNext = result.OrderBy(v => !v.IsDownloaded).ToList();
        }

        private async Task OnDownloadNow()
        {
            // "Download again": force a real re-fetch when the video is already marked downloaded, or
            // when the player couldn't load its file (streamFailed) — which is exactly the case where the
            // DB says downloaded but the file is missing or half-written. Without this the job would
            // no-op and the button would appear to work while doing nothing.
            bool force = streamFailed || (video?.IsDownloaded ?? false);

            var (resp, http) = await Backend.VideoDownload(
                new VideoDownloadRequest { VideoIds = new[] { VideoId }, Force = force });
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

        // No local state flip here, unlike the watched buttons above: DeleteScheduledAt is carried by the
        // live change feed and merged by MergeLiveFields, so the button repaints itself. With a grace
        // period of 0 the files are deleted outright and IsDownloaded flips instead — also pushed, which
        // is why this doesn't look like nothing happened.
        private async Task OnMarkForDeletion()
            => await Backend.VideoMarkForDeletion(new VideoMarkForDeletionRequest { VideoIds = new[] { VideoId } });

        private async Task OnUnmarkForDeletion()
            => await Backend.VideoUnmarkForDeletion(new VideoUnmarkForDeletionRequest { VideoIds = new[] { VideoId } });

        /// <summary>Countdown for the "marked for deletion" state, matching the grid's badge tooltip.</summary>
        private string DeletionTooltip
        {
            get
            {
                if (video?.DeleteScheduledAt == null)
                    return null;
                var remaining = video.DeleteScheduledAt.Value - DateTimeOffset.Now;
                return remaining > TimeSpan.Zero
                    ? $"Files will be deleted in {remaining.Humanize()}"
                    : "Files are queued for deletion";
            }
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
        private async Task OnPositionReport((double Seconds, double Duration) report)
        {
            currentPlaybackSeconds = report.Seconds;
            if (video == null || video.IsWatched)
                return;

            int secs = (int)report.Seconds;

            // Don't record a resume point for a barely-started video — under MinInProgressSeconds it isn't
            // meaningfully "in progress" (so no bar, no "Started" state, no resume).
            if (secs < MinInProgressSeconds)
                return;

            video.PlaybackPositionSeconds = secs;

            // Backfill duration so the grid's resume bar can be drawn (enrichment doesn't always set it).
            int? dur = (report.Duration > 0 && double.IsFinite(report.Duration)) ? (int?)report.Duration : null;
            if (dur.HasValue && (video.Duration == null || video.Duration <= 0))
                video.Duration = dur;

            await Backend.VideoReportProgress(new VideoReportProgressRequest
            {
                VideoId = VideoId,
                PositionSeconds = secs,
                DurationSeconds = dur,
            });
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

        /// <summary>
        /// Whether a "12:34" in the description can seek. Same condition as chapters and for the same
        /// reason: those times describe the original timeline, so on a file SponsorBlock trimmed they
        /// would land in the wrong place. Unlike subtitles, which yt-dlp re-times when it cuts.
        /// </summary>
        private bool DescriptionSeekable =>
            video != null && video.IsDownloaded && !streamFailed && !video.SponsorsRemoved;

        private async Task OnDescriptionSeek(double seconds)
        {
            // streamFailed can flip between render and click, so re-check rather than trusting the
            // rendered state.
            if (DescriptionSeekable && playerRef != null)
            {
                await playerRef.SeekTo(seconds);
                currentPlaybackSeconds = seconds;
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
