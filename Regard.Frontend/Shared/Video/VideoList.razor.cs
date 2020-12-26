using Microsoft.AspNetCore.Components;
using Regard.Common.API.Model;
using Regard.Common.API.Subscriptions;
using Regard.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Video
{
    public partial class VideoList
    {
        [Inject]
        protected BackendService Backend { get; set; }

        private readonly ObservableCollection<VideoViewModel> videos = new ObservableCollection<VideoViewModel>();

        private ApiSubscription selectedSubscription = null;

        private ApiSubscriptionFolder selectedFolder = null;

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            await Populate();
        }

        private async Task Populate()
        {
            var (resp, httpResp) = await Backend.VideoList(new VideoListRequest()
            {
                SubscriptionFolderId = selectedFolder?.Id,
                SubscriptionId = selectedSubscription?.Id
            });

            if (httpResp.IsSuccessStatusCode)
            {
                videos.Clear();
                foreach (var video in resp.Data.Videos)
                    videos.Add(new VideoViewModel(video));
            }
        }
    }
}
