using Microsoft.AspNetCore.Components;
using Regard.Common.API.Model;
using Regard.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Regard.Common.API.Subscriptions;

namespace Regard.Frontend.Pages
{
    public partial class Watch
    {
        private ApiVideo video;
        private Uri videoStreamUri;
        private string errorMessage;

        [Inject] protected BackendService Backend { get; set; }

        [Parameter] public int VideoId { get; set; }



        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();

            var (resp, httpResp) = await Backend.VideoList(new VideoListRequest() { Ids = new[] { VideoId } });
            if (httpResp.IsSuccessStatusCode)
            {
                video = resp.Data.Videos.FirstOrDefault();
                if (video == null)
                    errorMessage = "An error occurred while getting video details.";
            }
            else
                errorMessage = "An error occurred while getting video details: " + resp.Message;

            videoStreamUri = await Backend.VideoViewUrl(VideoId);
        }
    }
}
