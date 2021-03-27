using Regard.Backend.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Downloader
{
    public enum VideoDownloadState
    { 
        NotDownloading,
        Queued,
        Downloading,
        Completed
    }

    public class VideoDownloadStateChangedEventArgs : EventArgs
    {
        public int VideoId { get; set; }

        public VideoDownloadState State { get; set; }

        public float? Progress { get; set; }
    }

    public interface IVideoDownloaderService
    {
        /// <summary>
        /// Event called when the download state of a video changes
        /// </summary>
        event EventHandler<VideoDownloadStateChangedEventArgs> VideoStateChanged;

        /// <summary>
        /// Checks if new videos need to be downloaded, and queues them for download.
        /// </summary>
        /// <param name="sub"></param>
        /// <returns></returns>
        Task ProcessDownloadRules(Subscription sub);

        /// <summary>
        /// Determines the maximum number of videos that can be downloaded according to settings, 
        /// quotas, and currently downloaded videos.
        /// </summary>
        /// <param name="sub">Subscription</param>
        /// <returns>Limit, or null if there is no limit</returns>
        int? DetermineMaximumVideoCount(Subscription sub);

        /// <summary>
        /// Determines the maximum number of bytes that can be downloaded according to settings, 
        /// quotas, and currently downloaded videos.
        /// </summary>
        /// <param name="sub">Subscription</param>
        /// <returns>Limit in bytes, or null if there is no limit</returns>
        long? DetermineMaximumAllowedSize(Subscription sub);

        /// <summary>
        /// Called when a video is queued for download
        /// </summary>
        /// <param name="videoId">Video ID</param>
        void OnVideoQueued(int videoId);

        /// <summary>
        /// Called when a video is being downloaded
        /// </summary>
        /// <param name="videoId">Video ID</param>
        /// <param name="progress">Progress (value between 0 and 1)</param>
        void OnVideoDownloading(int videoId, float progress);

        /// <summary>
        /// Called when a video finishes downloading
        /// </summary>
        /// <param name="videoId">Video ID</param>
        void OnDownloadFinished(int videoId);
    }
}
