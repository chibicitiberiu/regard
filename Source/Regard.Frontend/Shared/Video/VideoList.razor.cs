using Humanizer;
using Microsoft.AspNetCore.Components;
using Regard.Common.API.Model;
using Regard.Common.API.Subscriptions;
using Regard.Common.Utils;
using Regard.Common.Utils.Collections;
using Microsoft.JSInterop;
using Regard.Frontend.Services;
using Regard.Frontend.Shared.Modals;
using Regard.Model;
using Regard.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace Regard.Frontend.Shared.Video
{
    public partial class VideoList : IDisposable
    {
        private bool initialized = false;
        private readonly BulkObservableCollection<VideoViewModel> videos = new BulkObservableCollection<VideoViewModel>();
        
        private int page = 0;
        private int videosPerPage = 60;
        private int totalVideoCount = 0;

        private int? selectedSubscription = null;
        private int? selectedFolder = null;

        private string query = "";

        private VideoOrder order;
        private ElementReference orderButton;
        private bool orderMenuVisible = false;

        private VideoWatchState watchState = VideoWatchState.All;
        private bool? isDownloaded;
        private ElementReference filterButton;
        private bool filterMenuVisible = false;

        private VideoAddModal videoAddModal;

        /// <summary>True when the list is scoped to a single subscription (not the home or folder view).</summary>
        private bool CanAddVideo => selectedSubscription.HasValue;

        /// <summary>True when any non-default filter is active (drives the Filter button accent).</summary>
        private bool HasActiveFilter => watchState != VideoWatchState.All || isDownloaded.HasValue;

        // Context-aware empty-state text so the page never sits blank.
        private string EmptyStateTitle
        {
            get
            {
                if (selectedSubscription.HasValue)
                    return HasActiveFilter ? "No videos match this filter." : "No videos in this subscription yet.";
                if (selectedFolder.HasValue)
                {
                    if (!FolderHasSubscriptions(selectedFolder.Value))
                        return "No subscriptions in this folder yet.";
                    return HasActiveFilter ? "No videos match this filter." : "No videos in this folder yet.";
                }
                return HasActiveFilter ? "No videos match this filter." : "No videos yet.";
            }
        }

        private string EmptyStateHint
        {
            get
            {
                if (HasActiveFilter)
                    return "Try adjusting or clearing the filter.";
                if (selectedSubscription.HasValue)
                    return "New videos appear here after the next sync.";
                if (selectedFolder.HasValue)
                    return FolderHasSubscriptions(selectedFolder.Value) ? null : "Drag subscriptions into this folder, or add one.";
                return "Add a subscription from the sidebar to get started.";
            }
        }

        /// <summary>True if the folder (or any nested subfolder) contains at least one subscription.</summary>
        private bool FolderHasSubscriptions(int folderId)
        {
            var ids = new HashSet<int> { folderId };
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var f in AppState.Folders.Values)
                {
                    int? parent = (int?)f.ParentId;
                    if (parent.HasValue && ids.Contains(parent.Value) && ids.Add(f.Id))
                        changed = true;
                }
            }
            return AppState.Subscriptions.Values.Any(s => s.ParentFolderId.HasValue && ids.Contains(s.ParentFolderId.Value));
        }

        [Inject] protected BackendService Backend { get; set; }

        [Inject] protected MessagingService Messaging { get; set; }

        [Inject] protected AppState AppState { get; set; }

        [Inject] protected Microsoft.JSInterop.IJSRuntime JS { get; set; }

        [Inject] protected NotificationsService Notifications { get; set; }

        [Parameter] public int? SelectedSubscription
        {
            get => selectedSubscription;
            set => SetSelectedSubscription(value);
        }

        [Parameter] public int? SelectedFolder 
        {
            get => selectedFolder;
            set => SetSelectedFolder(value);
        }

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            Messaging.VideoUpdated += Messaging_VideoUpdated;
            Messaging.VideosChanged += Messaging_VideosChanged;
            Notifications.ActivityChanged += OnNotificationsActivityChanged;

            await LoadFilterState();
            await Populate();
            initialized = true;
        }

        private const string FilterStorageKey = "regard.videoFilters";

        private class PersistedFilters
        {
            public VideoWatchState WatchState { get; set; }
            public bool? IsDownloaded { get; set; }
            public VideoOrder Order { get; set; }
        }

        // Filters persist for the browser session (sessionStorage) so they survive navigation and reloads.
        private async Task LoadFilterState()
        {
            try
            {
                var json = await JS.InvokeAsync<string>("sessionStorage.getItem", FilterStorageKey);
                if (!string.IsNullOrEmpty(json))
                {
                    var s = System.Text.Json.JsonSerializer.Deserialize<PersistedFilters>(json);
                    if (s != null)
                    {
                        watchState = s.WatchState;
                        isDownloaded = s.IsDownloaded;
                        order = s.Order;
                    }
                }
            }
            catch
            {
                // sessionStorage unavailable / malformed value -> keep defaults.
            }
        }

        private async Task SaveFilterState()
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(new PersistedFilters
                {
                    WatchState = watchState,
                    IsDownloaded = isDownloaded,
                    Order = order,
                });
                await JS.InvokeVoidAsync("sessionStorage.setItem", FilterStorageKey, json);
            }
            catch { }
        }

        public async Task SetSelectedSubscription(int? subscriptionId)
        {
            selectedSubscription = subscriptionId;
            selectedFolder = null;
            page = 0;
            if (initialized)
                await Populate();
        }

        public async Task SetSelectedFolder(int? folderId)
        {
            selectedSubscription = null;
            selectedFolder = folderId;
            page = 0;
            if (initialized)
                await Populate();
        }

        public async Task DeselectAll()
        {
            selectedFolder = null;
            selectedSubscription = null;
            page = 0;
            if (initialized)
                await Populate();
        }

        public async Task SetPage(int page)
        {
            this.page = page;
            await Populate();
            await ScrollToTop();
        }

        public async Task SetQuery(string value)
        {
            this.query = value;
            this.page = 0;
            await Populate();
            await ScrollToTop();
        }

        private async Task ScrollToTop()
        {
            try
            {
                await JS.InvokeVoidAsync("RegardHelpers.scrollToTop", ".video-gallery");
            }
            catch
            {
                // Non-fatal: a missing element / prerender just means no scroll reset.
            }
        }

        private async Task OnQueryChanged(ChangeEventArgs e)
        {
            await SetQuery((string)e.Value);
        }

        private async Task SetOrder(VideoOrder order)
        {
            this.order = order;
            this.page = 0;
            await SaveFilterState();
            await Populate();
            await ScrollToTop();
        }

        private void OnOrderClicked()
        {
            orderMenuVisible = true;
        }

        private void OnFilterClicked()
        {
            filterMenuVisible = true;
        }

        public async Task SetWatchState(VideoWatchState value)
        {
            this.watchState = value;
            this.page = 0;
            await SaveFilterState();
            await Populate();
            await ScrollToTop();
        }

        private async Task SetFilterIsDownloaded(bool? isDownloaded)
        {
            this.isDownloaded = isDownloaded;
            this.page = 0;
            await SaveFilterState();
            await Populate();
            await ScrollToTop();
        }

        private async Task OnAddVideoClicked()
        {
            if (videoAddModal != null && selectedSubscription.HasValue)
                await videoAddModal.Show(selectedSubscription.Value);
        }

        public async Task Populate()
        {
            try
            {
                var (resp, httpResp) = await Backend.VideoList(new VideoListRequest()
                {
                    SubscriptionFolderId = selectedFolder,
                    SubscriptionId = selectedSubscription,
                    Query = query,
                    WatchState = watchState,
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
                    {
                        FixRelativeUrl(video);
                        videos.Add(new VideoViewModel(video));
                    }
                    videos.EndBatch();

                    totalVideoCount = resp.Data.TotalCount;
                    StateHasChanged();
                }
            }
            catch (Exception)
            {
            }
        }

        private void FixRelativeUrl(ApiVideo apiVideo)
        {
            if (!apiVideo.ThumbnailUrl.IsAbsoluteUri)
                apiVideo.ThumbnailUrl = new Uri(AppState.BackendBase, apiVideo.ThumbnailUrl);
        }

        private void Messaging_VideoUpdated(object sender, ApiVideo e)
        {
            // This fires on the SignalR callback context, not a UI event, so marshal to the renderer's
            // sync context with InvokeAsync — otherwise the collection change + StateHasChanged don't
            // actually re-render the card.
            _ = InvokeAsync(() =>
            {
                for (int i = 0; i < videos.Count; i++)
                {
                    if (videos[i].ApiVideo.Id == e.Id)
                    {
                        FixRelativeUrl(e);
                        // Replace the item (not just set a property): ListView only re-renders on
                        // collection changes, so a plain property assignment wouldn't refresh the card.
                        videos[i] = new VideoViewModel(e);
                        StateHasChanged();
                        break;
                    }
                }
            });
        }

        private System.Timers.Timer refetchDebounce;

        // A subscription's video set changed (sync discovered new videos, an import, a single add). Refetch
        // the current view if it could contain that subscription — the server re-applies filters/order/paging,
        // so we don't insert tiles by hand. Debounced so a sync burst collapses into one refetch.
        private void Messaging_VideosChanged(object sender, int subscriptionId)
        {
            if (selectedSubscription.HasValue && selectedSubscription.Value != subscriptionId)
                return;   // viewing a different single subscription — not affected

            if (refetchDebounce == null)
            {
                refetchDebounce = new System.Timers.Timer(600) { AutoReset = false };
                refetchDebounce.Elapsed += (_, __) => _ = InvokeAsync(async () =>
                {
                    await Populate();
                    StateHasChanged();
                });
            }
            refetchDebounce.Stop();
            refetchDebounce.Start();
        }

        void OnVideoShowContextMenu(VideoViewModel videoVM)
        {
            videoVM.IsContextMenuVisible = true;
            StateHasChanged();
        }

        async Task OnVideoMarkWatched(VideoViewModel videoVM)
        {
            await Backend.VideoMarkWatched(new VideoMarkWatchedRequest() { VideoIds = new[] { videoVM.ApiVideo.Id } });
            // Refresh so the card reflects the change immediately (ListView only re-renders on collection
            // changes, and the SignalR video-updated push isn't reliably delivered) and any watch-state
            // filter re-applies (a now-watched video leaves the "Unwatched"/"Started" view).
            await Populate();
        }

        async Task OnVideoMarkNotWatched(VideoViewModel videoVM)
        {
            await Backend.VideoMarkNotWatched(new VideoMarkNotWatchedRequest() { VideoIds = new[] { videoVM.ApiVideo.Id } });
            await Populate();
        }

        async Task OnVideoDownload(VideoViewModel videoVM)
        {
            await Backend.VideoDownload(new VideoDownloadRequest() { VideoIds = new[] { videoVM.ApiVideo.Id } });
        }

        async Task OnVideoDeleteFiles(VideoViewModel videoVM)
        {
            await Backend.VideoDeleteFiles(new VideoDeleteFilesRequest() { VideoIds = new[] { videoVM.ApiVideo.Id } });
            await Populate();   // reflect the removed download badge immediately
        }

        async Task OnVideoMarkForDeletion(VideoViewModel videoVM)
        {
            await Backend.VideoMarkForDeletion(new VideoMarkForDeletionRequest() { VideoIds = new[] { videoVM.ApiVideo.Id } });
            await Populate();   // show the "marked for deletion" badge immediately
        }

        async Task OnVideoUnmarkForDeletion(VideoViewModel videoVM)
        {
            await Backend.VideoUnmarkForDeletion(new VideoUnmarkForDeletionRequest() { VideoIds = new[] { videoVM.ApiVideo.Id } });
            await Populate();
        }

        // Tooltip for the "marked for deletion" badge, e.g. "Files marked for deletion in 3 hours".
        protected string DeletionTooltip(ApiVideo video)
        {
            if (!video.DeleteScheduledAt.HasValue)
                return string.Empty;
            var remaining = video.DeleteScheduledAt.Value - DateTimeOffset.Now;
            return remaining > TimeSpan.Zero
                ? $"Files marked for deletion in {remaining.Humanize()}"
                : "Files marked for deletion (pending)";
        }

        // Repaint the list when download notifications change, so the per-card progress pie animates live.
        private void OnNotificationsActivityChanged(object sender, EventArgs e) => InvokeAsync(StateHasChanged);

        // The live "ongoing" download notification for a given video (if any). Its Progress (0..1, or null
        // for indeterminate) drives the pie badge. Matches on VideoId set by DownloadVideoJob.GetOngoingNotification.
        protected ApiNotification DownloadNotification(int videoId)
            => Notifications.Notifications.FirstOrDefault(n => n.Ongoing && n.VideoId == videoId);

        protected string PieStyle(float? progress)
        {
            if (!progress.HasValue)
                return string.Empty;
            int deg = (int)(Math.Clamp(progress.Value, 0f, 1f) * 360);
            return $"background: conic-gradient(var(--color-bg-success) {deg}deg, rgba(0,0,0,0.45) {deg}deg);";
        }

        protected string PieTitle(float? progress)
            => progress.HasValue ? $"Downloading… {(int)(progress.Value * 100)}%" : "Downloading…";

        public void Dispose()
        {
            Messaging.VideoUpdated -= Messaging_VideoUpdated;
            Messaging.VideosChanged -= Messaging_VideosChanged;
            Notifications.ActivityChanged -= OnNotificationsActivityChanged;
            refetchDebounce?.Dispose();
        }
    }
}
