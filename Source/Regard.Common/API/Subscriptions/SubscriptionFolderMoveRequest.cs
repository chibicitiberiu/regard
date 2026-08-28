namespace Regard.Common.API.Subscriptions
{
    /// <summary>
    /// Reparents a single folder without touching its other settings. See
    /// <see cref="SubscriptionMoveRequest"/> for why this is separate from the full-replace edit path.
    /// </summary>
    public class SubscriptionFolderMoveRequest
    {
        public int Id { get; set; }

        /// <summary>The destination folder, or null for the tree root.</summary>
        public int? ParentFolderId { get; set; }
    }
}
