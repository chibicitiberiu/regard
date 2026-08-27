using Microsoft.AspNetCore.Components;
using Regard.Common.API.Subscriptions;
using Regard.Frontend.Shared.Controls;
using Regard.Services;
using System;
using System.Net;
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

        // Set when the backend reports the URL duplicates an existing subscription. The message is
        // shown as a warning (not an error) and the button becomes "Create anyway".
        protected bool DuplicateWarning { get; set; }

        // Always clickable; only disabled while a create is in flight.
        protected bool SubmitEnabled => !Submitting;

        private void Reset()
        {
            Request = new SubscriptionCreateRequest();
            Submitting = false;
            ErrorMessage = null;
            DuplicateWarning = false;
        }

        private async Task OnSubmit()
        {
            // A second submit after a duplicate warning is the user's "create anyway".
            if (DuplicateWarning)
                Request.AllowDuplicate = true;

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

                DuplicateWarning = httpResp.StatusCode == HttpStatusCode.Conflict;
                ErrorMessage = string.IsNullOrWhiteSpace(resp?.Message)
                    ? (DuplicateWarning
                        ? "You're already subscribed to this. Create another anyway?"
                        : "Could not create the subscription. Check the URL and try again.")
                    : resp.Message;
            }
            catch (Exception ex)
            {
                DuplicateWarning = false;
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
