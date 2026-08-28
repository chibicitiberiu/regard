namespace Regard.Common.API.Model
{
    /// <summary>A SponsorBlock segment to skip during playback, in seconds on the original timeline.</summary>
    public class ApiSponsorSegment
    {
        public double Start { get; set; }

        public double End { get; set; }

        public string Category { get; set; }
    }
}
