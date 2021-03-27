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
        private bool isVisible, isVisibleDelay;
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
            await Popper.Create(AttachTo, theDiv, new Frontend.Popper.Options() 
            {
                Placement = Placement,
                Modifiers = new Popper.Modifier[]
                {
                    new Popper.OffsetModifier() { Distance = Offset }
                }
            });

            if (firstRender)
            {
                dotNetRef = DotNetObjectReference.Create(this);
                if (IsVisible)
                    await RegisterClickOutsideHandler();
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
                await JSRuntime.InvokeVoidAsync("RegardHelpers.addClickOutsideHandler", theDiv, dotNetRef);
        }

        private async Task UnregisterClickOutsideHandler()
        {
            if (dotNetRef != null)
                await JSRuntime.InvokeVoidAsync("RegardHelpers.removeClickOutsideHandler", theDiv);
        }

        public async ValueTask DisposeAsync()
        {
            await UnregisterClickOutsideHandler();
            dotNetRef.Dispose();
        }

        private async Task UpdateIsVisibleDelayed(bool value, int ms)
        {
            if (value)
                await RegisterClickOutsideHandler();
            else
                await UnregisterClickOutsideHandler();

            await Task.Delay(ms);
            isVisibleDelay = value;
            StateHasChanged();
        }
    }
}
