using Microsoft.AspNetCore.Components;
using Regard.Common.API.Model;
using Regard.Common.API.Subscriptions;
using Regard.Frontend.Shared.Controls;
using Regard.Model;
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

        protected Modal previewModal;

        protected List<FilterPreviewItem> PreviewItems { get; set; } = new();

        protected bool PreviewTruncated { get; set; }

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

        // Tri-state string binds for the subtitle bools. The shared RgSimpleInputSelect bool? control
        // renders its "True" option with an empty value attribute (a Blazor boolean-attribute quirk),
        // so selecting it round-trips to null — hence plain "" / "on" / "off" selects here instead.
        private static string BoolToTri(bool? v) => v == null ? "" : (v.Value ? "on" : "off");
        private static bool? TriToBool(string s) => string.IsNullOrEmpty(s) ? (bool?)null : s == "on";

        public string WriteSubtitlesStr
        {
            get => BoolToTri(Request.WriteSubtitles);
            set => Request.WriteSubtitles = TriToBool(value);
        }

        public string WriteAutoSubStr
        {
            get => BoolToTri(Request.WriteAutoSub);
            set => Request.WriteAutoSub = TriToBool(value);
        }

        public string AllSubsStr
        {
            get => BoolToTri(Request.AllSubs);
            set => Request.AllSubs = TriToBool(value);
        }

        protected string PatternPreview => Shared.PatternPreviewHelper.Render(Request.DownloadPath);

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            ValidationMessage = "Loading...";

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
                    Request.Id = SubscriptionId;
                    Request.Name = Subscription.Name;
                    Request.Description = Subscription.Description;
                    Request.ParentFolderId = Subscription.ParentFolderId;
                    Request.AutoDownload = Subscription.Config.AutoDownload;
                    Request.DownloadMaxCount = Subscription.Config.DownloadMaxCount;
                    Request.DownloadOrder = Subscription.Config.DownloadOrder;
                    Request.DeleteWatched = Subscription.Config.DeleteWatched;
                    Request.MarkDeletedAsWatched = Subscription.Config.MarkDeletedAsWatched;
                    Request.DownloadPath = Subscription.Config.DownloadPath;
                    Request.WriteSubtitles = Subscription.Config.WriteSubtitles;
                    Request.WriteAutoSub = Subscription.Config.WriteAutoSub;
                    Request.AllSubs = Subscription.Config.AllSubs;
                    Request.SubFormat = Subscription.Config.SubFormat;
                    Request.SubLang = Subscription.Config.SubLang;
                    Request.SponsorblockActions = Subscription.Config.SponsorblockActions;
                    Request.Filters = Subscription.Config.Filters?.ToList() ?? new();
                    SubmitEnabled = true;
                    ValidationMessage = string.Empty;
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
            var (resp, httpResp) = await Backend.SubscriptionEdit(Request);
            if (httpResp.IsSuccessStatusCode)
            {
                ValidationMessage = "Success!";
            }

            else ValidationMessage = resp.Message;
        }

        private void AddFilter()
        {
            Request.Filters.Add(new ApiSubscriptionFilter { Action = FilterAction.Include, Pattern = "" });
        }

        private async Task ShowPreview()
        {
            var (resp, httpResp) = await Backend.SubscriptionFilterPreview(new SubscriptionFilterPreviewRequest
            {
                SubscriptionId = SubscriptionId,
                Filters = Request.Filters,
            });
            if (httpResp.IsSuccessStatusCode)
            {
                PreviewItems = resp.Data.Videos;
                PreviewTruncated = resp.Data.Truncated;
                previewModal?.Show();
            }
            else
            {
                ValidationMessage = "Preview failed: " + resp.Message;
            }
        }
    }
}
