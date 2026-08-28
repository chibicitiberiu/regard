namespace Regard.Common.API.Model
{
    /// <summary>A video chapter (a titled time range), in seconds on the original timeline.</summary>
    public class ApiChapter
    {
        public double Start { get; set; }

        public double End { get; set; }

        public string Title { get; set; }
    }
}
