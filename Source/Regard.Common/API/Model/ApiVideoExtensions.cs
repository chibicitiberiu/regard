namespace Regard.Common.API.Model
{
    public static class ApiVideoExtensions
    {
        /// <summary>
        /// Copies the fields a live push can actually carry from <paramref name="pushed"/> onto
        /// <paramref name="target"/>, leaving everything else untouched.
        ///
        /// A pushed ApiVideo is built by ApiModelFactory.ToApi, which projects the Video row and nothing
        /// else. The playback fields — StreamMimeType, EmbedUrl, SponsorSegments, Chapters,
        /// SubtitleTracks, Likes, Dislikes — are filled in only by VideoController.List (some per row,
        /// some only when fetching a single video), so replacing a DTO wholesale with a pushed one
        /// silently blanks them: on the watch page that breaks the player outright. Merge instead of
        /// replace.
        ///
        /// Do NOT "fix" the omissions below by adding them here. Likes is present because it is a real
        /// column that ToApi projects; SubtitleTracks and Chapters are absent because they are not, and
        /// copying a null over a populated list is exactly the bug this method exists to prevent.
        /// </summary>
        public static ApiVideo MergeLiveFields(this ApiVideo target, ApiVideo pushed)
        {
            if (target == null || pushed == null || target.Id != pushed.Id)
                return target;

            target.Name = pushed.Name;
            target.Description = pushed.Description;
            target.IsWatched = pushed.IsWatched;
            target.IsNew = pushed.IsNew;
            target.IsDownloaded = pushed.IsDownloaded;
            target.DownloadedSize = pushed.DownloadedSize;
            target.SubscriptionId = pushed.SubscriptionId;
            target.PlaylistIndex = pushed.PlaylistIndex;
            target.Published = pushed.Published;
            target.IsEnriched = pushed.IsEnriched;
            target.LastUpdated = pushed.LastUpdated;
            target.ThumbnailUrl = pushed.ThumbnailUrl;
            target.UploaderName = pushed.UploaderName;
            target.Views = pushed.Views;
            target.Duration = pushed.Duration;
            target.Likes = pushed.Likes;
            target.Rating = pushed.Rating;
            target.OriginalUrl = pushed.OriginalUrl;
            target.SponsorsRemoved = pushed.SponsorsRemoved;
            target.PlaybackPositionSeconds = pushed.PlaybackPositionSeconds;
            target.DeleteScheduledAt = pushed.DeleteScheduledAt;

            return target;
        }
    }
}
