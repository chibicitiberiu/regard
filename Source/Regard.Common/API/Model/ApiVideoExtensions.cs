namespace Regard.Common.API.Model
{
    public static class ApiVideoExtensions
    {
        /// <summary>
        /// Copies the fields a live push can actually carry from <paramref name="pushed"/> onto
        /// <paramref name="target"/>, leaving everything else untouched.
        ///
        /// A pushed ApiVideo is built by ApiModelFactory.ToApi, which projects the Video row and nothing
        /// else. The playback fields — StreamMimeType, EmbedUrl, SponsorSegments, Chapters, Likes,
        /// Dislikes — are filled in only by VideoController.List (some per row, some only when fetching a
        /// single video), so replacing a DTO wholesale with a pushed one silently blanks them: on the
        /// watch page that breaks the player outright. Merge instead of replace.
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
            target.Rating = pushed.Rating;
            target.OriginalUrl = pushed.OriginalUrl;
            target.SponsorsRemoved = pushed.SponsorsRemoved;
            target.PlaybackPositionSeconds = pushed.PlaybackPositionSeconds;
            target.DeleteScheduledAt = pushed.DeleteScheduledAt;

            return target;
        }
    }
}
