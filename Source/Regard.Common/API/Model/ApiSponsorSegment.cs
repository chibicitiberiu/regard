namespace Regard.Common.API.Model
{
    /// <summary>A SponsorBlock segment to skip during playback, in seconds on the original timeline.</summary>
    public class ApiSponsorSegment
    {
        public double Start { get; set; }

        public double End { get; set; }

        public string Category { get; set; }

        /// <summary>
        /// True when this segment's category is configured to skip, i.e. it is skipped automatically
        /// unless the viewer turns it off for this playback.
        ///
        /// The watch page asks SponsorBlock for every category it models, not just the configured ones,
        /// so the segment list can offer an intro or an outro to skip ad hoc without a trip to settings.
        /// Those extra rows arrive with this false and are inert until ticked.
        /// </summary>
        public bool Skip { get; set; }
    }
}
