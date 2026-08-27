using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Regard.Common.API.Subscriptions;
using Regard.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace Regard.Frontend.Shared.Forms
{
    public partial class FolderCreateForm
    {
        [Inject]
        BackendService Backend { get; set; }

        [Inject]
        AppState AppState { get; set; }

        SubscriptionFolderCreateRequest Request { get; set; } = new SubscriptionFolderCreateRequest();

        string ValidationMessage { get; set; }

        bool SubmitClicked { get; set; } = false;

        bool SubmitEnabled => !SubmitClicked;

        [Parameter]
        public EventCallback Submitted { get; set; }

        private async Task OnSubmit()
        {
            var (resp, httpResp) = await Backend.SubscriptionFolderCreate(Request);
            if (httpResp.IsSuccessStatusCode)
            {
                // Refresh the tree so the new folder shows immediately, even if the SignalR
                // push is missed (mirrors the subscription-create flow; keyed by id, no dup).
                AppState.RequestRefresh();
                await Submitted.InvokeAsync(null);

                // clear request
                Request = new SubscriptionFolderCreateRequest();
            }

            ValidationMessage = resp.Message;
        }
    }
}
