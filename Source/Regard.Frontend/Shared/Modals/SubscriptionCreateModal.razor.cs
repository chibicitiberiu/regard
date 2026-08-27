using Microsoft.AspNetCore.Components;
using Regard.Common.API.Subscriptions;
using Regard.Frontend.Shared.Controls;
using Regard.Services;
using System;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Modals
{
    public partial class SubscriptionCreateModal
    {
        private Modal modal;

        [Inject] protected BackendService Backend { get; set; }

        [Inject] protected AppState AppState { get; set; }

        [Parameter] public EventCallback Submitted { get; set; }

        protected SubscriptionCreateRequest Request { get; set; } = new SubscriptionCreateRequest();

        protected bool Submitting { get; set; }

        protected string ErrorMessage { get; set; }

        // Always clickable; only disabled while a create is in flight.
        protected bool SubmitEnabled => !Submitting;

        private void Reset()
        {
            Request = new SubscriptionCreateRequest();
            Submitting = false;
            ErrorMessage = null;
        }

        private async Task OnSubmit()
        {
            Submitting = true;
            ErrorMessage = null;
            StateHasChanged();

            try
            {
                var (resp, httpResp) = await Backend.SubscriptionCreate(Request);
                if (httpResp.IsSuccessStatusCode)
                {
                    // The list normally updates from the SignalR NotifySubscriptionCreated push;
                    // force a refresh too so the new subscription appears even if that push is
                    // missed (keyed by id, so it can't double up with the push).
                    AppState.RequestRefresh();
                    await Submitted.InvokeAsync(null);
                    await Dismiss();
                    return;
                }

                ErrorMessage = string.IsNullOrWhiteSpace(resp?.Message)
                    ? "Could not create the subscription. Check the URL and try again."
                    : resp.Message;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Could not create the subscription: " + ex.Message;
            }
            finally
            {
                Submitting = false;
                StateHasChanged();
            }
        }

        public async Task Show()
        {
            Reset();
            await modal.Show();
        }

        public async Task Dismiss()
        {
            await modal.Close();
        }
    }
}
