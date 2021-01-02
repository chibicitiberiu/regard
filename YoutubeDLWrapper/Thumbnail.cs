using System;
using System.Collections.Generic;
using System.Text;

namespace YoutubeDLWrapper
{
    public class Thumbnail
    {
        /// <summary>
        /// Opt: Thumbnail format ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// URL
        /// </summary>
        public Uri Url { get; set; }

        /// <summary>
        /// Opt: Quality of the image
        /// </summary>
        public int? Preference { get; set; }

        /// <summary>
        /// Opt: width
        /// </summary>
        public int? Width { get; set; }

        /// <summary>
        /// Opt: height
        /// </summary>
        public int? Height { get; set; }

        /// <summary>
        /// Opt: string {width}x{height}, deprecated
        /// </summary>
        [Obsolete]
        public string Resolution { get; set; }

        /// <summary>
        /// Opt: file size
        /// </summary>
        public int? Filesize { get; set; }
    }
}
