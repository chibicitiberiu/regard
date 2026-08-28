using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Regard.Backend.Hubs;
using Regard.Backend.Model;
using Regard.Common;
using System;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    /// <summary>
    /// Pushes a "video updated" notification to a user's connected clients so their video cards refresh
    /// live (e.g. a download finished, or downloaded files were deleted). The scoped MessagingService
    /// bridge is never instantiated, and the download/delete jobs write Video.DownloadedPath directly
    /// without raising VideoManager.VideoUpdated, so those transitions are broadcast explicitly through
    /// this helper. IHubContext and ApiModelFactory are both singletons, so this is DI-safe anywhere.
    /// </summary>
    public class VideoUpdateNotifier
    {
        private readonly IHubContext<MessagingHub, IMessagingClient> hub;
        private readonly ApiModelFactory modelFactory;
        private readonly ILogger<VideoUpdateNotifier> log;

        public VideoUpdateNotifier(IHubContext<MessagingHub, IMessagingClient> hub,
                                   ApiModelFactory modelFactory,
                                   ILogger<VideoUpdateNotifier> log)
        {
            this.hub = hub;
            this.modelFactory = modelFactory;
            this.log = log;
        }

        /// <summary>Broadcast the current state of <paramref name="video"/> to the owning user's clients.</summary>
        public async Task NotifyVideoUpdated(Video video, string userId)
        {
            if (video == null || string.IsNullOrEmpty(userId))
                return;

            try
            {
                var api = modelFactory.ToApi(video);
                await hub.Clients.User(userId).NotifyVideoUpdated(api);
                log.LogInformation("Broadcast video-updated: video {0} (downloaded={1}) to user {2}", video.Id, api.IsDownloaded, userId);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Failed to broadcast video-updated for video {0}", video.Id);
            }
        }
    }
}
