using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Controls
{
    public partial class DropDownPanel : IAsyncDisposable
    {
        private ElementReference theDiv;
        private DotNetObjectReference<DropDownPanel> dotNetRef;
        private readonly string clickOutsideId = Guid.NewGuid().ToString("N");
        private bool isVisible, isVisibleDelay;
        private bool popperCreated;
        private DateTime lastSetVisible;

        [Inject]
        public Popper.Popper Popper { get; set; }

        [Inject]
        public IJSRuntime JSRuntime { get; set; }

        [Parameter]
        public RenderFragment ChildContent { get; set; }

        [Parameter]
        public string Class { get; set; }

        [Parameter]
        public bool IsVisible 
        {
            get => isVisible; 
            set
            {
                if (isVisible != value)
                {
                    isVisible = value;
                    if (value)
                        lastSetVisible = DateTime.Now;
                    Task.Run(() => UpdateIsVisibleDelayed(value, (value) ? 50 : 500));
                    IsVisibleChanged.InvokeAsync(value);
                }
            }
        }

        [Parameter]
        public EventCallback<bool> IsVisibleChanged { get; set; }

        [Parameter]
        public ElementReference AttachTo { get; set; }

        [Parameter]
        public Frontend.Popper.Placement Placement { get; set; } = Frontend.Popper.Placement.Auto;

        [Parameter]
        public bool ShowArrow { get; set; } = true;

        [Parameter]
        public double Offset { get; set; } = 0;

        [Parameter]
        public bool AutoDismiss { get; set; } = true;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (firstRender)
                dotNetRef = DotNetObjectReference.Create(this);

            // The popper div only exists in the DOM while (IsVisible || isVisibleDelay). Creating the
            // Popper on every render — including the many renders where the div isn't shown — meant a
            // grid of N menus fired N JS-interop round-trips per open (the dominant cost of the ~1s lag).
            // Create it once, when the div actually appears, and reset when it leaves the DOM so the next
            // open re-creates against the fresh @ref (otherwise the second open renders unpositioned).
            // All click-outside registration happens HERE, not in the IsVisible setter's async path:
            // theDiv is only a valid @ref once the div has actually rendered (shown == true). Registering
            // earlier (as UpdateIsVisibleDelayed used to) passes a default/null ElementReference to JS,
            // which stores a null-element handler that then crashes _onClick / removeClickOutsideHandler.
            bool shown = IsVisible || isVisibleDelay;
            if (shown && !popperCreated)
            {
                await Popper.Create(AttachTo, theDiv, new Frontend.Popper.Options()
                {
                    Placement = Placement,
                    // Fixed positioning so the popup escapes a scrolling/clipping ancestor (the video grid
                    // has overflow:auto) instead of being cut off or hidden.
                    PositioningStrategy = Frontend.Popper.PositioningStrategy.Fixed,
                    Modifiers = new Popper.Modifier[]
                    {
                        new Popper.OffsetModifier() { Distance = Offset }
                    }
                });
                popperCreated = true;
                await RegisterClickOutsideHandler();   // div is in the DOM now
            }
            else if (!shown && popperCreated)
            {
                popperCreated = false;
                await UnregisterClickOutsideHandler();
            }
        }

        [JSInvokable]
        public void InvokeClickOutside()
        {
            // Only hide after a short delay, otherwise the dismiss happens during the click which shows the dropdown
            if (AutoDismiss && (DateTime.Now - lastSetVisible).TotalSeconds > .2)
            {
                IsVisible = false;
                StateHasChanged();
            }
        }

        private async Task RegisterClickOutsideHandler()
        {
            if (dotNetRef != null)
                // Pass AttachTo (the toggle button) so a click on it isn't treated as an outside click —
                // otherwise the opening click could immediately dismiss the panel on a slow build.
                await JSRuntime.InvokeVoidAsync("RegardHelpers.addClickOutsideHandler", theDiv, dotNetRef, clickOutsideId, AttachTo);
        }

        private async Task UnregisterClickOutsideHandler()
        {
            // Remove by stable id, not theDiv: the @ref is already nulled by the time dispose runs, so a
            // by-element removal would leak this handler (with a soon-disposed .NET ref) and every later
            // click would invoke a disposed object ("no tracked object with id").
            await JSRuntime.InvokeVoidAsync("RegardHelpers.removeClickOutsideHandler", clickOutsideId);
        }

        public async ValueTask DisposeAsync()
        {
            // Unregister the click-outside handler BEFORE disposing the .NET ref, and tolerate JS being
            // unavailable during teardown (page unload).
            try { await UnregisterClickOutsideHandler(); } catch { }
            dotNetRef?.Dispose();
        }

        private async Task UpdateIsVisibleDelayed(bool value, int ms)
        {
            // Click-outside register/unregister is handled in OnAfterRenderAsync (where theDiv is a valid
            // ref); doing it here raced the render and passed a null element to JS.
            await Task.Delay(ms);
            isVisibleDelay = value;
            StateHasChanged();
        }
    }
}
