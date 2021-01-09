using Humanizer;
using Microsoft.AspNetCore.Components;
using Regard.Common.API.Model;
using Regard.Common.API.Subscriptions;
using Regard.Common.Utils;
using Regard.Frontend.Services;
using Regard.Model;
using Regard.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace Regard.Frontend.Shared.Video
{
    public partial class VideoList : IDisposable
    {
        private readonly BulkObservableCollection<VideoViewModel> videos = new BulkObservableCollection<VideoViewModel>();
        
        private int page = 0;
        private int videosPerPage = 60;
        private int totalVideoCount = 0;

        private ApiSubscription selectedSubscription = null;
        private ApiSubscriptionFolder selectedFolder = null;

        private string query = "";

        private VideoOrder order;
        private ElementReference orderButton;
        private bool orderMenuVisible = false;

        private bool hideWatched;
        
        private bool? isDownloaded;
        private ElementReference downloadedButton;
        private bool downloadedMenuVisible = false;

        [Inject]
        protected BackendService Backend { get; set; }

        [Inject]
        protected MessagingService Messaging { get; set; }

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
            page = 0;
            await Populate();
        }

        public async Task SetSelectedFolder(ApiSubscriptionFolder folder)
        {
            selectedSubscription = null;
            selectedFolder = folder;
            page = 0;
            await Populate();
        }

        public async Task DeselectAll()
        {
            selectedFolder = null;
            selectedSubscription = null;
            page = 0;
            await Populate();
        }

        public async Task SetPage(int page)
        {
            this.page = page;
            await Populate();
        }

        public async Task SetQuery(string value)
        {
            this.query = value;
            this.page = 0;
            await Populate();
        }

        private async Task OnQueryChanged(ChangeEventArgs e)
        {
            await SetQuery((string)e.Value);
        }

        private async Task SetOrder(VideoOrder order)
        {
            this.order = order;
            this.page = 0;
            await Populate();
        }

        private void OnOrderClicked()
        {
            orderMenuVisible = true;
        }

        public async Task SetHideWatched(bool value)
        {
            this.hideWatched = value;
            this.page = 0;
            await Populate();
        }

        private async Task OnToggleHideWatched()
        {
            await SetHideWatched(!hideWatched);
        }

        private void OnFilterClicked()
        {
            downloadedMenuVisible = true;
        }

        private async Task SetFilterIsDownloaded(bool? isDownloaded)
        {
            this.isDownloaded = isDownloaded;
            this.page = 0;
            await Populate();
        }

        public async Task Populate()
        {
            var (resp, httpResp) = await Backend.VideoList(new VideoListRequest()
            {
                SubscriptionFolderId = selectedFolder?.Id,
                SubscriptionId = selectedSubscription?.Id,
                Query = query,
                IsWatched = (hideWatched) ? (bool?)false : null,
                IsDownloaded = isDownloaded,
                Order = order,
                Limit = videosPerPage,
                Offset = page * videosPerPage,
            });

            if (httpResp.IsSuccessStatusCode)
            {
                videos.BeginBatch();
                videos.Clear();
                foreach (var video in resp.Data.Videos)
                    videos.Add(new VideoViewModel(video));
                videos.EndBatch();

                totalVideoCount = resp.Data.TotalCount;
                StateHasChanged();
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

        void OnVideoShowContextMenu(VideoViewModel videoVM)
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
