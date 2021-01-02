using Humanizer;
using Microsoft.AspNetCore.Components;
using Regard.Common.API.Model;
using Regard.Common.API.Subscriptions;
using Regard.Frontend.Services;
using Regard.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Video
{
    public partial class VideoList : IDisposable
    {
        [Inject]
        protected BackendService Backend { get; set; }

        [Inject]
        protected MessagingService Messaging { get; set; }

        private readonly ObservableCollection<VideoViewModel> videos = new ObservableCollection<VideoViewModel>();

        private ApiSubscription selectedSubscription = null;

        private ApiSubscriptionFolder selectedFolder = null;

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            Messaging.VideoUpdated += Messaging_VideoUpdated;
            await Populate();
        }

        public async Task SetSelectedSubscription(ApiSubscription subscription)
        {
            selectedSubscription = subscription;
            selectedFolder = null;
            await Populate();
        }

        public async Task SetSelectedFolder(ApiSubscriptionFolder folder)
        {
            selectedSubscription = null;
            selectedFolder = folder;
            await Populate();
        }
        public async Task DeselectAll()
        {
            selectedFolder = null;
            selectedSubscription = null;
            await Populate();
        }

        public async Task Populate()
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

        private void Messaging_VideoUpdated(object sender, ApiVideo e)
        {
            Console.WriteLine($"Video updated: {e.Id}");

            for (int i = 0; i < videos.Count; i++)
            {
                if (videos[i].ApiVideo.Id == e.Id)
                {
                    videos[i].ApiVideo = e;
                    break;
                }
            }
        }

        void OnShowContextMenu(VideoViewModel videoVM)
        {
            videoVM.IsContextMenuVisible = true;
            StateHasChanged();
        }

        async Task OnVideoMarkWatched(VideoViewModel videoVM) 
        {
            await Backend.VideoMarkWatched(new VideoMarkWatchedRequest() { VideoIds = new[] { videoVM.ApiVideo.Id } });
        }

        async Task OnVideoMarkNotWatched(VideoViewModel videoVM)
        {
            await Backend.VideoMarkNotWatched(new VideoMarkNotWatchedRequest() { VideoIds = new[] { videoVM.ApiVideo.Id } });
        }

        async Task OnVideoDownload(VideoViewModel videoVM)
        {
            await Backend.VideoDownload(new VideoDownloadRequest() { VideoIds = new[] { videoVM.ApiVideo.Id } });
        }

        async Task OnVideoDeleteFiles(VideoViewModel videoVM)
        {
            await Backend.VideoDeleteFiles(new VideoDeleteFilesRequest() { VideoIds = new[] { videoVM.ApiVideo.Id } });
        }

        public void Dispose()
        {
            Messaging.VideoUpdated -= Messaging_VideoUpdated;
        }
    }
}
