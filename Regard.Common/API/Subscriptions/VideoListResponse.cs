using Regard.Common.API.Model;

namespace Regard.Common.API.Subscriptions
{
    public class VideoListResponse
    {
        public ApiVideo[] Videos { get; set; }

        public int TotalCount { get; set; }
    }
}
