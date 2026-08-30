using Regard.Model;

namespace Regard.Common.API.Settings
{
    /// <summary>
    /// A user's download-related settings. Every field is nullable and null means "inherit the
    /// global/default value" (the server unsets any stored override); a non-null value pins an
    /// explicit per-user override.
    /// </summary>
    public class ApiUserSettings
    {
        // --- Subscription defaults (a subscription/folder can still override these). null = inherit. ---

        /// <summary>Automatically download new videos of a subscription. null = inherit.</summary>
        public bool? AutoDownload { get; set; }

        /// <summary>Order in which a subscription's videos are picked for download. null = inherit.</summary>
        public VideoOrder? DownloadOrder { get; set; }

        /// <summary>How many recent videos to keep per subscription (-1 = all). null = inherit.</summary>
        public int? DownloadMaxCount { get; set; }

        /// <summary>Max total size to keep per subscription, in MB (-1 = unlimited). null = inherit.</summary>
        public long? DownloadMaxSize { get; set; }

        /// <summary>Delete a video's files once it's watched. null = inherit.</summary>
        public bool? DeleteWatched { get; set; }

        /// <summary>Mark a video watched when its files are deleted. null = inherit.</summary>
        public bool? MarkDeletedAsWatched { get; set; }

        /// <summary>Grace period (minutes) before a marked video's files are deleted (0 = immediate). null = inherit.</summary>
        public int? DeleteGracePeriod { get; set; }

        // Effective global defaults for the subscription-default fields above (what "inherit" resolves to),
        // so the UI can label the "Default" option with the real value. Read-only from the client's view.
        public bool AutoDownloadDefault { get; set; }
        public VideoOrder DownloadOrderDefault { get; set; }
        public bool DeleteWatchedDefault { get; set; }
        public bool MarkDeletedAsWatchedDefault { get; set; }

        // --- Download / format settings ---

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

        /// <summary>Per-category SponsorBlock actions ("category:action" CSV). null/empty = none.</summary>
        public string SponsorblockActions { get; set; }

        /// <summary>
        /// Download path/filename template (yt-dlp -o), combining directory and filename. Supports
        /// tokens like {DownloadDirectory}, {FolderPath}, {Subscription.Name}, {EpisodeCode},
        /// {Video.Name}. This is the per-user default; a subscription can override it. null = inherit.
        /// </summary>
        public string DownloadPath { get; set; }

        /// <summary>
        /// Read-only: whether this user has their own yt-dlp cookies.txt. A bool, never a path — the
        /// server derives the path from the account, because a client-supplied one would let any user
        /// point yt-dlp at (and overwrite) arbitrary server files.
        /// </summary>
        public bool CookiesConfigured { get; set; }

        /// <summary>
        /// Write-only (never returned by GET): uploaded cookies.txt **content**. null = leave as-is;
        /// empty string = remove; non-empty = replace.
        /// </summary>
        public string CookiesFileContent { get; set; }
    }
}
