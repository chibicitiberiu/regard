using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Regard.Common.API.Subscriptions;
using Regard.Frontend.Shared.Controls;
using Regard.Services;
using System.Threading.Tasks;
using System.Timers;

namespace Regard.Frontend.Shared.Modals
{
    public partial class SubscriptionCreateEmptyModal
    {
        private readonly Timer nameValidateTimer;
        private Modal modal;

        [Inject] protected BackendService Backend { get; set; }

        [Parameter] public EventCallback Submitted { get; set; }

        protected SubscriptionCreateEmptyRequest Request { get; set; } = new SubscriptionCreateEmptyRequest();

        protected bool LoadingVisible { get; set; }

        protected string ValidationMessage { get; set; }

        protected bool? ValidationResult { get; set; }

        protected bool SubmitClicked { get; set; } = false;

        protected bool SubmitEnabled => ValidationResult.HasValue && ValidationResult.Value && !SubmitClicked;

        protected string NameStateClass
        {
            get
            {
                if (LoadingVisible)
                    return "loading";

                if (!string.IsNullOrEmpty(Request.Name) && ValidationResult.HasValue)
                    return ValidationResult.Value ? "valid" : "invalid";

                return string.Empty;
            }
        }

        public SubscriptionCreateEmptyModal()
        {
            nameValidateTimer = new Timer()
            {
                Interval = 300,
                AutoReset = false
            };
            nameValidateTimer.Elapsed += NameValidateTimer_Elapsed;
            Reset();
        }

        private void Reset()
        {
            Request = new SubscriptionCreateEmptyRequest();
            LoadingVisible = false;
            ValidationMessage = null;
            ValidationResult = null;
            SubmitClicked = false;
        }

        private void OnNameKeyUp(KeyboardEventArgs e)
        {
            // reset timer
            nameValidateTimer.Stop();
            nameValidateTimer.Start();
        }

        private async void NameValidateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (string.IsNullOrEmpty(Request.Name))
            {
                ValidationMessage = string.Empty;
            }
            else
            {
                LoadingVisible = true;
                StateHasChanged();
                var (resp, httpResp) = await Backend.SubscriptionValidateUrl(new SubscriptionValidateRequest() { Name = Request.Name });

                ValidationMessage = resp.Message;
                ValidationResult = httpResp.IsSuccessStatusCode;

                LoadingVisible = false;
            }
            StateHasChanged();
        }

        private async Task OnSubmit()
        {
            var (resp, httpResp) = await Backend.SubscriptionCreateEmpty(Request);
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
