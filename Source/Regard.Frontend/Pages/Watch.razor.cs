using Microsoft.AspNetCore.Components;
using Regard.Common.API.Model;
using Regard.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Regard.Common.API.Subscriptions;
using Regard.Frontend.Utils;

namespace Regard.Frontend.Pages
{
    public partial class Watch
    {
        private ApiVideo video;
        private ApiSubscription subscription;
        private Uri videoStreamUri;
        private string errorMessage;

        [Inject] protected BackendService Backend { get; set; }

        [Inject] protected AppState AppState { get; set; }

        [Parameter] public int VideoId { get; set; }

        public MarkupString FormattedDescription { get; set; }

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();

            var (resp, httpResp) = await Backend.VideoList(new VideoListRequest() { Ids = new[] { VideoId } });
            if (httpResp.IsSuccessStatusCode)
            {
                video = resp.Data.Videos.FirstOrDefault();
                if (video == null)
                    errorMessage = "An error occurred while getting video details.";

                string plainDesc = video?.Description ?? "";
                FormattedDescription = new MarkupString(plainDesc.FormatAsHtml());

                // Fetch the subscription so we can show its real channel avatar (ApiVideo has none).
                if (video != null)
                {
                    var (subResp, subHttp) = await Backend.SubscriptionList(
                        new SubscriptionListRequest() { Ids = new[] { video.SubscriptionId } });
                    if (subHttp.IsSuccessStatusCode)
                    {
                        subscription = subResp.Data.Subscriptions.FirstOrDefault();
                        // Normalize a relative thumbnail URL against the backend base, like the tree does.
                        if (subscription?.ThumbnailUrl != null && !subscription.ThumbnailUrl.IsAbsoluteUri)
                            subscription.ThumbnailUrl = new Uri(AppState.BackendBase, subscription.ThumbnailUrl);
                    }
                }
            }
            else
                errorMessage = "An error occurred while getting video details: " + resp.Message;

            videoStreamUri = await Backend.VideoViewUrl(VideoId);
        }
    }
}
