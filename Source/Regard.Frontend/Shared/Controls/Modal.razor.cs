using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Controls
{
    public partial class Modal
    {
        /// <summary>
        /// Visibility on initialization
        /// </summary>
        [Parameter]
        public bool IsInitialVisible { get; set; } = false;

        /// <summary>
        /// Indicates if the modal is currently being shown
        /// </summary>
        public bool IsVisible { get; private set; }

        /// <summary>
        /// If set to true, the "close" button is displayed and the user can dismiss the modal by clicking outside its area.
        /// Otherwise, the modal can only be closed programatically.
        /// </summary>
        [Parameter]
        public bool IsCloseable { get; set; } = true;

        /// <summary>
        /// Content that will be displayed in the title bar area
        /// </summary>
        [Parameter]
        public RenderFragment Title { get; set; }

        /// <summary>
        /// Main content of the modal
        /// </summary>
        [Parameter]
        public RenderFragment Body { get; set; }

        /// <summary>
        /// Event triggered when the modal is shown
        /// </summary>
        [Parameter]
        public EventCallback Shown { get; set; }

        /// <summary>
        /// Event triggered after the modal is closed
        /// </summary>
        [Parameter]
        public EventCallback Closed { get; set; }

        private bool clickOriginatedInside = false;

        /// <summary>
        /// Shows the modal.
        /// </summary>
        /// <returns></returns>
        public async Task Show()
        {
            if (!IsVisible)
            {
                IsVisible = true;
                await Shown.InvokeAsync(null);
                StateHasChanged();
            }
        }

        /// <summary>
        /// Closes the modal
        /// </summary>
        /// <returns></returns>
        public async Task Close()
        {
            if (IsVisible)
            {
                IsVisible = false;
                await Closed.InvokeAsync(null);
                StateHasChanged();
            }
        }

        protected override void OnInitialized()
        {
            IsVisible = IsInitialVisible;
            base.OnInitialized();
        }

        private async Task OnClickedBehind()
        {
            if (IsCloseable && clickOriginatedInside)
                await Close();

            clickOriginatedInside = false;
        }

        private async Task OnCloseClicked()
        {
            if (IsCloseable)
                await Close();
        }

        private void OnMouseDown()
        {
            clickOriginatedInside = true;
        }
    }
}
