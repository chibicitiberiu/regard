namespace Regard.Common.API.Subscriptions
{
    /// <summary>
    /// Reparents a single subscription without touching any of its other settings. Distinct from
    /// <see cref="SubscriptionEditRequest"/> on purpose: the edit path is a full replace that unsets
    /// every option absent from the request, so it must not be used for a drag-and-drop move.
    /// </summary>
    public class SubscriptionMoveRequest
    {
        public int Id { get; set; }

        /// <summary>The destination folder, or null for the tree root.</summary>
        public int? ParentFolderId { get; set; }
    }
}
