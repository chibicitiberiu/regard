/*
 * Option definitions for Regard.
 *
 * This used to be generated from Options.csv by Options.tt, but that T4 template only ran
 * inside Visual Studio (never during `dotnet build`), so the generation was dropped. This file
 * is now the single source of truth — edit it directly to add or change an option.
 */
using Regard.Model;

namespace Regard.Backend.Configuration
{
    public static class Options
    {
        /// <summary>
        /// Indicates if the first time setup was performed.
        /// </summary>
        public static readonly OptionDefinition<bool> Server_Initialized = new OptionDefinition<bool>(
            false,
            "server.initialized",
            null,
            null,
            0
        );

        /// <summary>
        /// Allow user registrations from the frontend.
        /// </summary>
        public static readonly OptionDefinition<bool> Server_AllowRegistrations = new OptionDefinition<bool>(
            true,
            "server.allow_registrations",
            "AllowRegistrations",
            "REGARD_ALLOW_REGISTRATIONS",
            0
        );

        /// <summary>
        /// Send debugging information to the frontend
        /// </summary>
        public static readonly OptionDefinition<bool> Server_Debug = new OptionDefinition<bool>(
            false,
            "server.debug",
            "Debug",
            "REGARD_DEBUG",
            0
        );

        /// <summary>
        /// How many days of finished jobs to keep in the Job Log (completed jobs are pruned past
        /// this; failed jobs are kept ~3x longer). 0 = never prune.
        /// </summary>
        public static readonly OptionDefinition<int> Server_JobHistoryRetentionDays = new OptionDefinition<int>(
            30,
            "server.job_history_retention_days",
            "JobHistoryRetentionDays",
            "REGARD_JOB_HISTORY_RETENTION_DAYS",
            0
        );

        /// <summary>
        /// If enabled, videos will be downloaded automatically
        /// </summary>
        public static readonly OptionDefinition<bool> Subscriptions_AutoDownload = new OptionDefinition<bool>(
            true,
            "subscriptions.auto_download",
            "Subscriptions:AutoDownload",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// Order in which to download videos
        /// </summary>
        public static readonly OptionDefinition<VideoOrder> Subscriptions_DownloadOrder = new OptionDefinition<VideoOrder>(
            VideoOrder.Newest,
            "subscriptions.download_order",
            "Subscriptions:DownloadOrder",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// Maximum number of downloaded videos to keep per subscription (-1 = no limit). This limit only applies to the automatic downloader, but the user can manually download more videos.
        /// </summary>
        public static readonly OptionDefinition<int> Subscriptions_MaxCount = new OptionDefinition<int>(
            3,
            "subscriptions.max_count",
            "Subscriptions:MaxCount",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// Maximum size a subscription can take on disk in MB (-1 = no limit). This limit only applies to the automatic downloader, but the user can manually download more videos.
        /// </summary>
        public static readonly OptionDefinition<long> Subscriptions_MaxSize = new OptionDefinition<long>(
            -1,
            "subscriptions.max_size",
            "Subscriptions:MaxSize",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// Forward: when a video is marked as watched, delete its downloaded files from disk.
        /// </summary>
        public static readonly OptionDefinition<bool> Subscriptions_DeleteWatched = new OptionDefinition<bool>(
            true,
            "subscriptions.delete_watched",
            "Subscriptions:DeleteWatched",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// Reverse: when a downloaded video's files are deleted (manually or externally),
        /// mark the video as watched so it isn't re-downloaded.
        /// </summary>
        public static readonly OptionDefinition<bool> Subscriptions_MarkDeletedAsWatched = new OptionDefinition<bool>(
            true,
            "subscriptions.mark_deleted_as_watched",
            "Subscriptions:MarkDeletedAsWatched",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// Pattern indicating where files are downloaded automatically.
        /// </summary>
        public static readonly OptionDefinition<string> Subscriptions_DownloadPath = new OptionDefinition<string>(
            "{DownloadDirectory}/{FolderPath}/{Subscription.Name}/{EpisodeCode} - {Video.Name}",
            "subscriptions.download_path",
            "Subscriptions:DownloadPath",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// Maximum number of downloaded videos to keep per user (-1 = no limit). This setting only applies to the automatic downloader, the user can download more videos than this limit. Use the User_CountQuota option for a hard limit.
        /// </summary>
        public static readonly OptionDefinition<int> User_MaxCount = new OptionDefinition<int>(
            -1,
            "user.max_count",
            "User:MaxCount",
            null,
            OptionFlags.User
        );

        /// <summary>
        /// Maximum size a user's downloaded videos can take in MB (-1 = no limit). This setting only applies to the automatic downloader, the user can download more videos than this limit. Use the User_SizeQuota option for a hard limit.
        /// </summary>
        public static readonly OptionDefinition<long> User_MaxSize = new OptionDefinition<long>(
            -1,
            "user.max_size",
            "User:MaxSize",
            null,
            OptionFlags.User
        );

        /// <summary>
        /// Hard limit on numbers of downloaded videos a user can keep (-1 = no limit). Also applies to manual downloads.
        /// </summary>
        public static readonly OptionDefinition<int> User_CountQuota = new OptionDefinition<int>(
            -1,
            "user.count_quota",
            "User:CountQuota",
            null,
            OptionFlags.User
        );

        /// <summary>
        /// Hard limit on total size of downloaded videos a user can keep in MB (-1 = no limit). Also applies to manual downloads.
        /// </summary>
        public static readonly OptionDefinition<long> User_SizeQuota = new OptionDefinition<long>(
            -1,
            "user.size_quota",
            "User:SizeQuota",
            null,
            OptionFlags.User
        );

        /// <summary>
        /// Maximum download rate in bytes per second (e.g. 50K or 4.2M)
        /// </summary>
        public static readonly OptionDefinition<string> Ytdl_LimitRate = new OptionDefinition<string>(
            null,
            "ytdl.limit_rate",
            "Ytdl:LimitRate",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// Number of retries (default is 10), or "infinite".
        /// </summary>
        public static readonly OptionDefinition<string> Ytdl_Retries = new OptionDefinition<string>(
            null,
            "ytdl.retries",
            "Ytdl:Retries",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// Write video description to a .description file.
        /// </summary>
        public static readonly OptionDefinition<bool> Ytdl_WriteDescription = new OptionDefinition<bool>(
            false,
            "ytdl.write_description",
            "Ytdl:WriteDescription",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// Write video metadata to a .info.json file
        /// </summary>
        public static readonly OptionDefinition<bool> Ytdl_WriteInfoJson = new OptionDefinition<bool>(
            false,
            "ytdl.write_info_json",
            "Ytdl:WriteInfoJson",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// Write thumbnail image to disk
        /// </summary>
        public static readonly OptionDefinition<bool> Ytdl_WriteThumbnail = new OptionDefinition<bool>(
            false,
            "ytdl.write_thumbnail",
            "Ytdl:WriteThumbnail",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// Contact the youtube-dl server for debugging
        /// </summary>
        public static readonly OptionDefinition<bool?> Ytdl_CallHome = new OptionDefinition<bool?>(
            null,
            "ytdl.call_home",
            "Ytdl:CallHome",
            null,
            0
        );

        /// <summary>
        /// Advanced raw yt-dlp format selector (-f). When set, it overrides the structured
        /// resolution/codec options below. Null/empty = compose from Ytdl_MaxResolution +
        /// Ytdl_ExcludedVideoCodecs + Ytdl_ExcludedAudioCodecs.
        /// </summary>
        public static readonly OptionDefinition<string> Ytdl_Format = new OptionDefinition<string>(
            null,
            "ytdl.format",
            "Ytdl:Format",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// Download all possible formats
        /// </summary>
        public static readonly OptionDefinition<bool> Ytdl_AllFormats = new OptionDefinition<bool>(
            false,
            "ytdl.all_formats",
            "Ytdl:AllFormats",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// Prefer free video formats
        /// </summary>
        public static readonly OptionDefinition<bool> Ytdl_PreferFreeFormats = new OptionDefinition<bool>(
            false,
            "ytdl.prefer_free_formats",
            "Ytdl:PreferFreeFormats",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// If a merge is required (e.g. bestvideo+bestaudio), output to given container format. One of mkv, mp4, ogg, webm, flv. Ignored if no merge is required.
        /// </summary>
        public static readonly OptionDefinition<string> Ytdl_MergeOutputFormat = new OptionDefinition<string>(
            "mp4",
            "ytdl.merge_output_format",
            "Ytdl:MergeOutputFormat",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// Maximum video resolution (height in pixels) to download; 0 = unlimited.
        /// </summary>
        public static readonly OptionDefinition<int> Ytdl_MaxResolution = new OptionDefinition<int>(
            0,
            "ytdl.max_resolution",
            "Ytdl:MaxResolution",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// Comma-separated video codec tokens to avoid (yt-dlp vcodec substring match), e.g. "av01,vp09".
        /// Use yt-dlp's real prefixes: AV1=av01, VP9=vp09, H.265=hev1, H.264=avc1.
        /// </summary>
        public static readonly OptionDefinition<string> Ytdl_ExcludedVideoCodecs = new OptionDefinition<string>(
            "av01",
            "ytdl.excluded_video_codecs",
            "Ytdl:ExcludedVideoCodecs",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// Comma-separated audio codec tokens to avoid (yt-dlp acodec substring match), e.g. "opus,mp4a".
        /// </summary>
        public static readonly OptionDefinition<string> Ytdl_ExcludedAudioCodecs = new OptionDefinition<string>(
            null,
            "ytdl.excluded_audio_codecs",
            "Ytdl:ExcludedAudioCodecs",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// Container to convert the downloaded video to (e.g. mp4, mkv, webm). Null/empty = no conversion.
        /// </summary>
        public static readonly OptionDefinition<string> Ytdl_TranscodeVideo = new OptionDefinition<string>(
            null,
            "ytdl.transcode_video",
            "Ytdl:TranscodeVideo",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// How to convert when Ytdl_TranscodeVideo is set: "remux" (lossless container change) or
        /// "recode" (re-encode with ffmpeg). Ignored when no transcode target is set.
        /// </summary>
        public static readonly OptionDefinition<string> Ytdl_TranscodeMode = new OptionDefinition<string>(
            "remux",
            "ytdl.transcode_mode",
            "Ytdl:TranscodeMode",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// Write subtitle files
        /// </summary>
        public static readonly OptionDefinition<bool> Ytdl_WriteSubtitles = new OptionDefinition<bool>(
            false,
            "ytdl.write_sub",
            "Ytdl:WriteSub",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// Write automatically generated subtitles (YouTube only)
        /// </summary>
        public static readonly OptionDefinition<bool> Ytdl_WriteAutoSub = new OptionDefinition<bool>(
            false,
            "ytdl.write_auto_sub",
            "Ytdl:WriteAutoSub",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// Write all available subtitles of the video
        /// </summary>
        public static readonly OptionDefinition<bool> Ytdl_AllSubs = new OptionDefinition<bool>(
            false,
            "ytdl.all_subs",
            "Ytdl:AllSubs",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// Subtitle format, accepts formats preference, for example: "srt" or "ass/srt/best"
        /// </summary>
        public static readonly OptionDefinition<string> Ytdl_SubFormat = new OptionDefinition<string>(
            "best",
            "ytdl.sub_format",
            "Ytdl:SubFormat",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// Languages of the subtitles to download (optional) separated by commas.
        /// </summary>
        public static readonly OptionDefinition<string> Ytdl_SubLang = new OptionDefinition<string>(
            "en",
            "ytdl.sub_lang",
            "Ytdl:SubLang",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

    }
}