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

    public struct VideoJsTrack
    {

    }

    public partial class Video : IAsyncDisposable
    {
        private ElementReference videoElement;
        private DotNetObjectReference<Video> selfRef;
        private bool watchProgressRegistered;

        [Inject] protected IJSRuntime JS { get; set; }

        [Parameter] public string Class { get; set; }

        [Parameter] public bool ShowControls { get; set; } = true;

        [Parameter] public bool AutoPlay { get; set; } = true;

        [Parameter] public int Width { get; set; } = 640;

        [Parameter] public int Height { get; set; } = 360;

        [Parameter] public string Poster { get; set; }

        [Parameter] public EventCallback Ended { get; set; }

        [Parameter] public EventCallback Paused { get; set; }

        [Parameter] public EventCallback<ErrorEventArgs> Error { get; set; }

        /// <summary>
        /// Fraction of the duration (0..1) at which <see cref="WatchedThresholdReached"/> fires once.
        /// 0 (default) disables progress tracking. Set to e.g. 0.9 to count a video as watched near the end.
        /// </summary>
        [Parameter] public double WatchedThreshold { get; set; }

        [Parameter] public EventCallback WatchedThresholdReached { get; set; }

        [Parameter] public RenderFragment ChildContent { get; set; }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender && WatchedThreshold > 0 && WatchedThresholdReached.HasDelegate)
            {
                selfRef = DotNetObjectReference.Create(this);
                await JS.InvokeVoidAsync("RegardHelpers.addWatchProgressHandler", videoElement, selfRef, WatchedThreshold);
                watchProgressRegistered = true;
            }
        }

        [JSInvokable]
        public async Task OnWatchThresholdReached()
        {
            await WatchedThresholdReached.InvokeAsync(null);
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
            if (watchProgressRegistered)
            {
                try { await JS.InvokeVoidAsync("RegardHelpers.removeWatchProgressHandler", videoElement); }
                catch (Exception) { /* circuit/JS already gone during teardown */ }
            }
            selfRef?.Dispose();
        }
    }
}
