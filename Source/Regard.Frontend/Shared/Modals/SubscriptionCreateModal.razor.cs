using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Regard.Common.API.Subscriptions;
using Regard.Frontend.Shared.Controls;
using Regard.Services;
using System.Threading.Tasks;
using System.Timers;

namespace Regard.Frontend.Shared.Modals
{
    public partial class SubscriptionCreateModal
    {
        private readonly Timer urlValidateTimer;
        private Modal modal;

        [Inject] protected BackendService Backend { get; set; }

        [Parameter] public EventCallback Submitted { get; set; }

        protected SubscriptionCreateRequest Request { get; set; } = new SubscriptionCreateRequest();

        protected bool LoadingVisible { get; set; }

        protected string ValidationMessage { get; set; }

        protected bool? ValidationResult { get; set; }

        protected bool SubmitClicked { get; set; } = false;

        protected bool SubmitEnabled => ValidationResult.HasValue && ValidationResult.Value && !SubmitClicked;

        protected string UrlStateClass
        {
            get
            {
                if (LoadingVisible)
                    return "loading";

                if (!string.IsNullOrEmpty(Request.Url) && ValidationResult.HasValue)
                    return ValidationResult.Value ? "valid" : "invalid";

                return string.Empty;
            }
        }

        public SubscriptionCreateModal()
        {
            urlValidateTimer = new Timer()
            {
                Interval = 300,
                AutoReset = false
            };
            urlValidateTimer.Elapsed += UrlValidateTimer_Elapsed;
            Reset();
        }

        private void Reset()
        {
            Request = new SubscriptionCreateRequest();
            LoadingVisible = false;
            ValidationMessage = null;
            ValidationResult = null;
            SubmitClicked = false;
        }

        private void OnUrlKeyUp(KeyboardEventArgs e)
        {
            // reset timer
            urlValidateTimer.Stop();
            urlValidateTimer.Start();
        }

        private async void UrlValidateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (string.IsNullOrEmpty(Request.Url))
            {
                ValidationMessage = string.Empty;
            }
            else
            {
                LoadingVisible = true;
                StateHasChanged();
                var (resp, httpResp) = await Backend.SubscriptionValidateUrl(new SubscriptionValidateRequest() { Url = Request.Url });

                ValidationMessage = resp.Message;
                ValidationResult = httpResp.IsSuccessStatusCode;

                LoadingVisible = false;
            }
            StateHasChanged();
        }

        private async Task OnSubmit()
        {
            var (resp, httpResp) = await Backend.SubscriptionCreate(Request);
            if (httpResp.IsSuccessStatusCode)
            {
                await Submitted.InvokeAsync(null);
                await Dismiss();
            }

            ValidationMessage = resp.Message;
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
