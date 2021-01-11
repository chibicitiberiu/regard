using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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

    public partial class Video
    {
        [Parameter] public string Class { get; set; }

        [Parameter] public bool ShowControls { get; set; } = true;

        [Parameter] public bool AutoPlay { get; set; } = true;

        [Parameter] public int Width { get; set; } = 640;

        [Parameter] public int Height { get; set; } = 360;

        [Parameter] public string Poster { get; set; }

        [Parameter] public EventCallback Ended { get; set; }

        [Parameter] public EventCallback Paused { get; set; }

        [Parameter] public EventCallback<ErrorEventArgs> Error { get; set; }

        [Parameter] public RenderFragment ChildContent { get; set; }

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
    }
}
