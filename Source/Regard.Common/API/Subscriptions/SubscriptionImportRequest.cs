namespace Regard.Common.API.Subscriptions
{
    /// <summary>
    /// Bulk-import request: an OPML document or a newline-separated list of URLs (in
    /// <see cref="Content"/>), added under <see cref="ParentFolderId"/>. OPML folder groupings are
    /// mirrored as sub-folders under it.
    /// </summary>
    public class SubscriptionImportRequest
    {
        /// <summary>Raw OPML XML, or a newline-separated URL list (the server auto-detects which).</summary>
        public string Content { get; set; }

        /// <summary>Target folder the batch is added under; null = root.</summary>
        public int? ParentFolderId { get; set; }

        /// <summary>If false, an already-subscribed channel is skipped instead of duplicated.</summary>
        public bool AllowDuplicate { get; set; }

        /// <summary>Whether imported subscriptions auto-download new videos.</summary>
        public bool AutoDownload { get; set; }
    }
}
