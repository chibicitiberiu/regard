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
    public partial class FolderEdit
    {
        [Inject] protected BackendService Backend { get; set; }

        [Parameter] public int FolderId { get; set; }

        public ApiSubscriptionFolder Folder { get; set; }

        public SubscriptionFolderEditRequest Request { get; set; } = new SubscriptionFolderEditRequest();

        public string ValidationMessage { get; set; }

        public bool SubmitEnabled { get; set; }

        private string DownloadMaxCountStr 
        {
            get => Request.DownloadMaxCount?.ToString() ?? string.Empty;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    Request.DownloadMaxCount = null;
                    ValidationMessage = string.Empty;
                }
                else if (int.TryParse(value, out int valueInt))
                {
                    Request.DownloadMaxCount = valueInt;
                    ValidationMessage = string.Empty;
                }
                else
                {
                    ValidationMessage = "Download maximum count must be a number!";
                }
            }
        }

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            ValidationMessage = "Loading...";

            var (resp, httpResp) = await Backend.SubscriptionFolderList(new SubscriptionFolderListRequest() 
            {
                Ids = new[] { FolderId },
                Parts = ApiSubscriptionFolder.Parts.Config
            });

            if (httpResp.IsSuccessStatusCode)
            {
                Folder = resp.Data.Folders.FirstOrDefault();
                if (Folder != null)
                {
                    Request.Id = FolderId;
                    Request.Name = Folder.Name;
                    Request.ParentFolderId = Folder.ParentId;
                    Request.AutoDownload = Folder.Config.AutoDownload;
                    Request.DownloadMaxCount = Folder.Config.DownloadMaxCount;
                    Request.DownloadOrder = Folder.Config.DownloadOrder;
                    Request.AutomaticDeleteWatched = Folder.Config.AutomaticDeleteWatched;
                    Request.DownloadPath = Folder.Config.DownloadPath;
                    SubmitEnabled = true;
                    ValidationMessage = string.Empty;
                }
                else
                {
                    ValidationMessage = "An error occurred while getting folder details.";
                }
            }
            else
                ValidationMessage = "An error occurred while getting folder details: " + resp.Message;
        }

        private async Task OnSubmit()
        {
            var (resp, httpResp) = await Backend.SubscriptionFolderEdit(Request);
            if (httpResp.IsSuccessStatusCode)
            {
                ValidationMessage = "Success!";
            }

            else ValidationMessage = resp.Message;
        }
    }
}
