using Regard.Common.API.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Video
{
    public class VideoViewModel
    {
        public ApiVideo ApiVideo { get; set; }

        public VideoViewModel(ApiVideo apiVideo)
        {
            ApiVideo = apiVideo;
        }

        public string ThumbnailUrl => ApiVideo.ThumbnailUrl;

        public string Name => ApiVideo.Name;
    }
}
