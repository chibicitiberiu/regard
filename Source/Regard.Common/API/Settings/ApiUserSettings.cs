namespace Regard.Common.API.Settings
{
    /// <summary>
    /// A user's download-related settings. Every field is nullable and null means "inherit the
    /// global/default value" (the server unsets any stored override); a non-null value pins an
    /// explicit per-user override.
    /// </summary>
    public class ApiUserSettings
    {
        /// <summary>Maximum video resolution (height in px); 0 = unlimited. null = inherit.</summary>
        public int? MaxResolution { get; set; }

        /// <summary>
        /// Video codec tokens to exclude (yt-dlp vcodec prefixes, e.g. av01/vp09/hev1/avc1).
        /// null = inherit; empty array = explicitly exclude none.
        /// </summary>
        public string[] ExcludedVideoCodecs { get; set; }

        /// <summary>Audio codec tokens to exclude. null = inherit; empty array = exclude none.</summary>
        public string[] ExcludedAudioCodecs { get; set; }

        /// <summary>
        /// Transcode target container (mp4/mkv/webm). null = inherit; empty string = explicitly off
        /// (keep the original); a value = convert to that container.
        /// </summary>
        public string TranscodeVideo { get; set; }

        /// <summary>"remux" (lossless container change) or "recode" (re-encode). null = inherit.</summary>
        public string TranscodeMode { get; set; }

        /// <summary>
        /// Advanced raw yt-dlp -f selector. When set it overrides the structured options above.
        /// null = inherit (compose from resolution/codecs).
        /// </summary>
        public string RawFormatOverride { get; set; }

        /// <summary>Container to merge separate video+audio into. null = inherit.</summary>
        public string MergeOutputFormat { get; set; }

        /// <summary>
        /// Allow embedding the source site's player (e.g. YouTube) on the watch page for
        /// non-downloaded videos. null = inherit the default (off).
        /// </summary>
        public bool? AllowEmbedding { get; set; }

        /// <summary>Download subtitles alongside videos. null = inherit.</summary>
        public bool? WriteSubtitles { get; set; }

        /// <summary>Also download auto-generated (machine) captions. null = inherit.</summary>
        public bool? WriteAutoSub { get; set; }

        /// <summary>Download every available subtitle language. null = inherit.</summary>
        public bool? AllSubs { get; set; }

        /// <summary>Subtitle file format (e.g. best/srt/vtt/ass). null = inherit.</summary>
        public string SubFormat { get; set; }

        /// <summary>Subtitle language list, comma-separated (e.g. "en,en-US,es"). null = inherit.</summary>
        public string SubLang { get; set; }
    }
}
