using Microsoft.AspNetCore.Components;
using Regard.Common.API.Model;
using Regard.Common.API.Subscriptions;
using Regard.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Pages
{
    public partial class SubscriptionEdit
    {
        [Inject] protected BackendService Backend { get; set; }

        [Parameter] public int SubscriptionId { get; set; }

        public ApiSubscription Subscription { get; set; }

        public SubscriptionEditRequest Request { get; set; } = new SubscriptionEditRequest();

        public string ValidationMessage { get; set; }

        public bool SubmitEnabled { get; set; }

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();

            var (resp, httpResp) = await Backend.SubscriptionList(new SubscriptionListRequest() 
            {
                Ids = new[] { SubscriptionId },
                Parts = ApiSubscription.Parts.Config
            });

            if (httpResp.IsSuccessStatusCode)
            {
                Subscription = resp.Data.Subscriptions.FirstOrDefault();
                if (Subscription != null)
                {
                    Request.Name = Subscription.Name;
                    Request.Description = Subscription.Description;
                    Request.ParentFolderId = Subscription.ParentFolderId;
                    Request.AutoDownload = Subscription.Config.AutoDownload;
                    Request.DownloadMaxCount = Subscription.Config.DownloadMaxCount;
                    Request.DownloadOrder = Subscription.Config.DownloadOrder;
                    Request.AutomaticDeleteWatched = Subscription.Config.AutomaticDeleteWatched;
                    Request.DownloadPath = Subscription.Config.DownloadPath;
                }
                else
                {
                    ValidationMessage = "An error occurred while getting video details.";
                }
            }
            else
                ValidationMessage = "An error occurred while getting video details: " + resp.Message;
        }

        private async Task OnSubmit()
        {
            //var (resp, httpResp) = await Backend.SubscriptionFolderCreate(Request);
            //if (httpResp.IsSuccessStatusCode)
            //{
            //    await Submitted.InvokeAsync(null);

            //    // clear request
            //    Request = new SubscriptionFolderCreateRequest();
            //}

            //ValidationMessage = resp.Message;
        }
    }
}
