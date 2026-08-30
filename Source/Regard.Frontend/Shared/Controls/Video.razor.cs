using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Controls
{
    public struct VideoJsSource
    {
        public string Url { get; set; }

        public string MimeType { get; set; }
    }

    public partial class Video : IAsyncDisposable
    {
        private ElementReference videoElement;
        private DotNetObjectReference<Video> selfRef;
        private bool watchProgressRegistered;
        private bool skipSegmentsRegistered;
        private bool positionReportRegistered;
        private bool textTracksBound;

        [Inject] protected IJSRuntime JS { get; set; }

        [Parameter] public string Class { get; set; }

        [Parameter] public bool ShowControls { get; set; } = true;

        [Parameter] public bool AutoPlay { get; set; } = true;

        [Parameter] public int Width { get; set; } = 640;

        [Parameter] public int Height { get; set; } = 360;

        [Parameter] public string Poster { get; set; }

        /// <summary>
        /// The element's crossorigin mode. Needed for &lt;track&gt;: a text track is only exposed to the
        /// page when its response is CORS-readable, and in development the API is a different origin
        /// (:9585) from the dev server (:5000). In production the two are the same origin and the
        /// attribute is inert. Null omits it entirely.
        /// </summary>
        [Parameter] public string CrossOrigin { get; set; }

        [Parameter] public EventCallback Ended { get; set; }

        [Parameter] public EventCallback Paused { get; set; }

        [Parameter] public EventCallback<ErrorEventArgs> Error { get; set; }

        /// <summary>
        /// Fraction of the duration (0..1) at which <see cref="WatchedThresholdReached"/> fires once.
        /// 0 (default) disables progress tracking. Set to e.g. 0.9 to count a video as watched near the end.
        /// </summary>
        [Parameter] public double WatchedThreshold { get; set; }

        [Parameter] public EventCallback WatchedThresholdReached { get; set; }

        /// <summary>SponsorBlock segments to skip during playback (original-timeline seconds). Null = none.</summary>
        [Parameter] public IReadOnlyList<Regard.Common.API.Model.ApiSponsorSegment> SkipSegments { get; set; }

        /// <summary>Raised (throttled, ~5 s) with the current playback position in seconds, plus once on
        /// pause/ended and on dispose. Setting a delegate enables position reporting.</summary>
        [Parameter] public EventCallback<(double Seconds, double Duration)> PositionChanged { get; set; }

        /// <summary>Seconds to resume from once metadata loads. 0 (default) starts at the beginning.</summary>
        [Parameter] public double StartPosition { get; set; }

        [Parameter] public RenderFragment ChildContent { get; set; }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!firstRender)
                return;

            bool needWatchProgress = WatchedThreshold > 0 && WatchedThresholdReached.HasDelegate;
            bool needPositionReport = PositionChanged.HasDelegate;

            // One DotNetObjectReference shared by every JS handler that calls back into this component,
            // so position reporting works even when the watched-threshold handler isn't wired.
            if (needWatchProgress || needPositionReport)
                selfRef = DotNetObjectReference.Create(this);

            if (needWatchProgress)
            {
                await JS.InvokeVoidAsync("RegardHelpers.addWatchProgressHandler", videoElement, selfRef, WatchedThreshold);
                watchProgressRegistered = true;
            }

            if (needPositionReport)
            {
                await JS.InvokeVoidAsync("RegardHelpers.addPositionReportHandler", videoElement, selfRef, 5);
                positionReportRegistered = true;
            }

            if (StartPosition > 0)
                await JS.InvokeVoidAsync("RegardHelpers.seekOnLoad", videoElement, StartPosition);

            if (SkipSegments != null && SkipSegments.Count > 0)
            {
                var segs = SkipSegments.Select(s => new { start = s.Start, end = s.End }).ToArray();
                await JS.InvokeVoidAsync("RegardHelpers.addSkipSegmentsHandler", videoElement, segs);
                skipSegmentsRegistered = true;
            }
        }

        [JSInvokable]
        public async Task OnWatchThresholdReached()
        {
            await WatchedThresholdReached.InvokeAsync(null);
        }

        /// <summary>Seek the underlying player to an absolute time (seconds). Used by chapter clicks.</summary>
        public async Task SeekTo(double seconds)
        {
            try { await JS.InvokeVoidAsync("RegardHelpers.seekTo", videoElement, seconds); }
            catch (Exception) { /* player/JS not ready */ }
        }

        /// <summary>
        /// Turns on the track for <paramref name="preferredLanguage"/> (null for none) and starts
        /// reporting changes made through the player's own CC menu back to
        /// <c>OnTextTrackChanged</c> on <paramref name="owner"/>. Returns whether a track matched.
        ///
        /// A &lt;track&gt; added after the element was parsed defaults to "disabled" no matter what its
        /// `default` attribute says, so this has to run once the elements have rendered.
        /// </summary>
        public async Task<bool> BindTextTracks(DotNetObjectReference<Pages.Watch> owner, string preferredLanguage)
        {
            try
            {
                textTracksBound = true;
                return await JS.InvokeAsync<bool>("RegardHelpers.bindTextTracks", videoElement, owner, preferredLanguage);
            }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// Shows the track for this language and disables the rest; null turns subtitles off. The change
        /// is reported back through the binding set up by <see cref="BindTextTracks"/>, so the caller
        /// does not need to track state separately.
        /// </summary>
        public async Task SetTextTrack(string language)
        {
            try { await JS.InvokeVoidAsync("RegardHelpers.setTextTrack", videoElement, language); }
            catch (Exception) { /* player/JS not ready */ }
        }

        /// <summary>Diagnostic view of the player's text tracks (used by the end-to-end tests).</summary>
        public async Task<object> DescribeTextTracks()
        {
            try { return await JS.InvokeAsync<object>("RegardHelpers.describeTextTracks", videoElement); }
            catch (Exception) { return null; }
        }

        [JSInvokable]
        public async Task OnPositionReport(double seconds, double duration)
        {
            await PositionChanged.InvokeAsync((seconds, duration));
        }

        protected async Task OnEnded(EventArgs _)
        {
            await Ended.InvokeAsync(null);
        }

        protected async Task OnPaused(EventArgs _)
        {
            await Paused.InvokeAsync(null);
        }
        protected async Task OnError(ErrorEventArgs e)
        {
            await Error.InvokeAsync(e);
        }

        public async ValueTask DisposeAsync()
        {
            if (positionReportRegistered)
            {
                // Flush the final position BEFORE removing the handler — in-app navigation disposes the
                // component without firing pause/unload, so this is the only chance to save where we were.
                try
                {
                    await JS.InvokeVoidAsync("RegardHelpers.flushPositionReport", videoElement);
                    await JS.InvokeVoidAsync("RegardHelpers.removePositionReportHandler", videoElement);
                }
                catch (Exception) { /* circuit/JS already gone during teardown */ }
            }
            if (watchProgressRegistered)
            {
                try { await JS.InvokeVoidAsync("RegardHelpers.removeWatchProgressHandler", videoElement); }
                catch (Exception) { /* circuit/JS already gone during teardown */ }
            }
            if (skipSegmentsRegistered)
            {
                try { await JS.InvokeVoidAsync("RegardHelpers.removeSkipSegmentsHandler", videoElement); }
                catch (Exception) { /* circuit/JS already gone during teardown */ }
            }
            if (textTracksBound)
            {
                try { await JS.InvokeVoidAsync("RegardHelpers.unbindTextTracks", videoElement); }
                catch (Exception) { /* circuit/JS already gone during teardown */ }
            }
            selfRef?.Dispose();
        }
    }
}
