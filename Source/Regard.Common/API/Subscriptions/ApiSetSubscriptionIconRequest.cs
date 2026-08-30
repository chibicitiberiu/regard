namespace Regard.Common.API.Subscriptions
{
    /// <summary>Sets a subscription's icon from an uploaded image (base64-encoded in the JSON body).</summary>
    public class ApiSetSubscriptionIconRequest
    {
        public int Id { get; set; }

        /// <summary>The image bytes, base64-encoded.</summary>
        public string IconBase64 { get; set; }

        /// <summary>Original filename — its extension picks the stored format (validated to a raster type).</summary>
        public string FileName { get; set; }
    }
}
