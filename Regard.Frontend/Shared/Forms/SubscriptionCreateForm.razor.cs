using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Regard.Common.API.Request;
using Regard.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace Regard.Shared.Forms
{
    public partial class SubscriptionCreateForm
    {
        [Inject]
        BackendService Backend { get; set; }

        SubscriptionCreate Request { get; set; } = new SubscriptionCreate();

        bool LoadingVisible { get; set; }

        string ValidationMessage { get; set; }

        bool? ValidationResult { get; set; }

        bool SubmitClicked { get; set; } = false;

        bool SubmitEnabled => ValidationResult.HasValue && ValidationResult.Value && !SubmitClicked;

        [Parameter]
        public EventCallback Submitted { get; set; }

        string UrlStateClass
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

        readonly Timer urlValidateTimer = new Timer() 
        { 
            Interval = 300, 
            AutoReset = false 
        };

        public SubscriptionCreateForm()
        {
            urlValidateTimer.Elapsed += UrlValidateTimer_Elapsed;
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
                var (resp, httpResp) = await Backend.SubscriptionValidateUrl(new SubscriptionValidate() { Url = Request.Url });

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

                // clear request
                Request = new SubscriptionCreate();
            }

            ValidationMessage = resp.Message;
        }
    }
}
