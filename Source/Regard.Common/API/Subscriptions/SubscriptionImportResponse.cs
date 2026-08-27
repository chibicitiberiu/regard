namespace Regard.Common.API.Subscriptions
{
    /// <summary>
    /// Result of scheduling an import: how many subscriptions were parsed out of the input. The
    /// actual adds run as a background job whose progress shows in the notification bell and whose
    /// per-URL results show in the Settings Job Log.
    /// </summary>
    public class SubscriptionImportResponse
    {
        /// <summary>Number of subscription entries found in the input (the batch size).</summary>
        public int Count { get; set; }
    }
}
