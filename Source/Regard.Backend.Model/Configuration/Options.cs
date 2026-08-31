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
        /// Fetch real dislike counts for YouTube videos from the ReturnYouTubeDislike API and show them on
        /// the watch page, with attribution to returnyoutubedislike.com.
        ///
        /// On by default: without it the watch page can show a like count but no ratio and no dislikes,
        /// since YouTube stopped publishing dislike counts in 2021. The trade-off is one external call
        /// per watch-page open, which tells returnyoutubedislike.com which videos get watched here; their
        /// documented limits are 100/min and 10k/day per source IP, shared by every user of the server.
        /// Admins can turn it off in Settings.
        /// </summary>
        public static readonly OptionDefinition<bool> ReturnYouTubeDislike_Enabled = new OptionDefinition<bool>(
            true,
            "ryd.enabled",
            "ReturnYouTubeDislike:Enabled",
            "REGARD_RYD_ENABLED",
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
        /// How many days to keep bell notifications before they age out. Deliberately shorter than the
        /// Job Log retention, so a failed download's captured log stays inspectable in the Job Log after
        /// its notification is gone. 0 = never prune.
        /// </summary>
        public static readonly OptionDefinition<int> Server_NotificationRetentionDays = new OptionDefinition<int>(
            7,
            "server.notification_retention_days",
            "NotificationRetentionDays",
            "REGARD_NOTIFICATION_RETENTION_DAYS",
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
            false,
            "subscriptions.mark_deleted_as_watched",
            "Subscriptions:MarkDeletedAsWatched",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// Grace period in MINUTES between a downloaded video being marked for deletion (on watch, or
        /// manually) and its files actually being removed by the periodic sweep. 0 = delete immediately
        /// (legacy behavior). During the grace window the video shows a "marked for deletion" badge and
        /// still counts toward the subscription quota; "Unmark for deletion" cancels it.
        /// </summary>
        public static readonly OptionDefinition<int> Subscriptions_DeleteGracePeriod = new OptionDefinition<int>(
            1440,
            "subscriptions.delete_grace_period_minutes",
            "Subscriptions:DeleteGracePeriodMinutes",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// Whether YouTube Shorts are taken into the library at all. Off by default.
        ///
        /// Applied during sync, so an excluded Short is never stored: see
        /// SynchronizeJob.CheckForNewVideos. Turning this on makes them appear on the next sync, because
        /// a sync re-lists the whole channel; turning it off does NOT remove Shorts already stored.
        /// </summary>
        public static readonly OptionDefinition<bool> Subscriptions_IncludeShorts = new OptionDefinition<bool>(
            false,
            "subscriptions.include_shorts",
            "Subscriptions:IncludeShorts",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// Whether members-only videos are taken into the library at all. Off by default, because
        /// without the channel membership in the user's cookie jar they can be listed but never
        /// downloaded, so they would sit in the list failing forever.
        /// </summary>
        public static readonly OptionDefinition<bool> Subscriptions_IncludeMembersOnly = new OptionDefinition<bool>(
            false,
            "subscriptions.include_members_only",
            "Subscriptions:IncludeMembersOnly",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// Lower bound of the auto-download publish-date window, as "yyyy-MM-dd" ("" = no bound).
        /// Inclusive from midnight UTC of that day. Applies to the automatic downloader only — an
        /// explicit download always wins.
        /// </summary>
        public static readonly OptionDefinition<string> Subscriptions_PublishedAfter = new OptionDefinition<string>(
            "",
            "subscriptions.published_after",
            "Subscriptions:PublishedAfter",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// Upper bound of the auto-download publish-date window, as "yyyy-MM-dd" ("" = no bound).
        /// Inclusive of the whole of that day (see PublishDateFilter.PassesDateWindow).
        /// </summary>
        public static readonly OptionDefinition<string> Subscriptions_PublishedBefore = new OptionDefinition<string>(
            "",
            "subscriptions.published_before",
            "Subscriptions:PublishedBefore",
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
        /// How many of a subscription's newest videos to enrich with full metadata during sync (the
        /// rest are listed flat and enriched lazily when opened or downloaded). Also bounded below by a
        /// subscription's auto-download count so the download window is always fully enriched.
        /// Global/server option.
        /// </summary>
        public static readonly OptionDefinition<int> Sync_EagerEnrichCount = new OptionDefinition<int>(
            20,
            "sync.eager_enrich_count",
            "Sync:EagerEnrichCount",
            "REGARD_SYNC_EAGER_ENRICH_COUNT",
            0
        );

        /// <summary>
        /// Minutes with no output from a running download before it's treated as stalled and killed
        /// (the job then retries). Guards against a frozen .part that keeps the pipe open. 0 disables.
        /// Global/server option.
        /// </summary>
        public static readonly OptionDefinition<int> Ytdl_IdleTimeout = new OptionDefinition<int>(
            10,
            "ytdl.idle_timeout_minutes",
            "Ytdl:IdleTimeoutMinutes",
            "REGARD_YTDL_IDLE_TIMEOUT_MINUTES",
            0
        );

        // ---- Download throttling / anti-bot (server-wide) ----

        /// <summary>Master switch for download pacing + per-host throttling. Global/server option.</summary>
        public static readonly OptionDefinition<bool> Server_Throttle_Enabled = new OptionDefinition<bool>(
            true,
            "server.throttle.enabled",
            "Server:Throttle:Enabled",
            "REGARD_THROTTLE_ENABLED",
            0
        );

        /// <summary>
        /// Absolute path to a Netscape-format cookies.txt used for yt-dlp (--cookies), to clear YouTube's
        /// bot gate. Uploaded via the admin page (written to DataDirectory/cookies.txt). Global/server.
        /// </summary>
        /// <remarks>
        /// User-scoped: a user with their own uploaded cookies gets theirs, everyone else falls through
        /// to the server-wide file, so an existing global jar keeps working unchanged. The value is a
        /// filesystem path, and it is written **only** by the server from the authenticated user's id —
        /// never from a request body. Letting a user set this string would hand them an arbitrary file
        /// read (yt-dlp parses whatever it points at) and an arbitrary overwrite (yt-dlp saves the jar
        /// back when the run ends).
        /// </remarks>
        public static readonly OptionDefinition<string> Server_Ytdl_CookiesFile = new OptionDefinition<string>(
            null,
            "server.ytdl.cookies_file",
            "Server:Ytdl:CookiesFile",
            "REGARD_YTDL_COOKIES_FILE",
            OptionFlags.User
        );

        /// <summary>
        /// yt-dlp browser TLS-fingerprint impersonation target (--impersonate), e.g. "chrome",
        /// "chrome-110", "chrome:windows-10". Empty/null disables it; "auto" means "any available target"
        /// (yt-dlp's <c>--impersonate=</c>). Requires curl_cffi in the Python running yt-dlp — the flag is
        /// only passed when a matching target actually resolves, because yt-dlp hard-fails at startup on
        /// an unavailable one. Global/server option.
        /// </summary>
        public static readonly OptionDefinition<string> Server_Ytdl_Impersonate = new OptionDefinition<string>(
            "",
            "server.ytdl.impersonate",
            "Server:Ytdl:Impersonate",
            "REGARD_YTDL_IMPERSONATE",
            0
        );

        /// <summary>Seconds yt-dlp sleeps between HTTP requests during extraction (--sleep-requests). Global/server.</summary>
        public static readonly OptionDefinition<int> Server_Ytdl_SleepRequests = new OptionDefinition<int>(
            2,
            "server.ytdl.sleep_requests",
            "Server:Ytdl:SleepRequests",
            "REGARD_YTDL_SLEEP_REQUESTS",
            0
        );

        /// <summary>Minimum seconds yt-dlp sleeps before each download (--sleep-interval). Global/server.</summary>
        public static readonly OptionDefinition<int> Server_Ytdl_SleepInterval = new OptionDefinition<int>(
            5,
            "server.ytdl.sleep_interval",
            "Server:Ytdl:SleepInterval",
            "REGARD_YTDL_SLEEP_INTERVAL",
            0
        );

        /// <summary>Maximum seconds yt-dlp sleeps before each download (--max-sleep-interval). Global/server.</summary>
        public static readonly OptionDefinition<int> Server_Ytdl_MaxSleepInterval = new OptionDefinition<int>(
            30,
            "server.ytdl.max_sleep_interval",
            "Server:Ytdl:MaxSleepInterval",
            "REGARD_YTDL_MAX_SLEEP_INTERVAL",
            0
        );

        /// <summary>
        /// Global default download bandwidth cap (yt-dlp --limit-rate, e.g. "2M"); a per-subscription
        /// Ytdl_LimitRate overrides it. Empty/null = no global cap. Global/server option.
        /// </summary>
        public static readonly OptionDefinition<string> Server_Ytdl_LimitRate = new OptionDefinition<string>(
            "2M",
            "server.ytdl.limit_rate",
            "Server:Ytdl:LimitRate",
            "REGARD_YTDL_LIMIT_RATE",
            0
        );

        /// <summary>Min/max seconds between consecutive DOWNLOADS on one host (jittered pacing). Global/server.</summary>
        public static readonly OptionDefinition<int> Server_Throttle_DownloadMinSeconds = new OptionDefinition<int>(
            60, "server.throttle.download_min_seconds", "Server:Throttle:DownloadMinSeconds", "REGARD_THROTTLE_DOWNLOAD_MIN_SECONDS", 0);

        public static readonly OptionDefinition<int> Server_Throttle_DownloadMaxSeconds = new OptionDefinition<int>(
            180, "server.throttle.download_max_seconds", "Server:Throttle:DownloadMaxSeconds", "REGARD_THROTTLE_DOWNLOAD_MAX_SECONDS", 0);

        /// <summary>Min/max seconds between consecutive metadata EXTRACTIONS on one host. Global/server.</summary>
        public static readonly OptionDefinition<int> Server_Throttle_ExtractMinSeconds = new OptionDefinition<int>(
            5, "server.throttle.extract_min_seconds", "Server:Throttle:ExtractMinSeconds", "REGARD_THROTTLE_EXTRACT_MIN_SECONDS", 0);

        public static readonly OptionDefinition<int> Server_Throttle_ExtractMaxSeconds = new OptionDefinition<int>(
            20, "server.throttle.extract_max_seconds", "Server:Throttle:ExtractMaxSeconds", "REGARD_THROTTLE_EXTRACT_MAX_SECONDS", 0);

        /// <summary>Max downloads per host per hour / per day (backstop; 0 or less = unlimited). Global/server.</summary>
        public static readonly OptionDefinition<int> Server_Throttle_MaxPerHour = new OptionDefinition<int>(
            15, "server.throttle.max_per_hour", "Server:Throttle:MaxPerHour", "REGARD_THROTTLE_MAX_PER_HOUR", 0);

        public static readonly OptionDefinition<int> Server_Throttle_MaxPerDay = new OptionDefinition<int>(
            200, "server.throttle.max_per_day", "Server:Throttle:MaxPerDay", "REGARD_THROTTLE_MAX_PER_DAY", 0);

        /// <summary>Max simultaneous downloads per host. Global/server.</summary>
        public static readonly OptionDefinition<int> Server_Throttle_PerHostConcurrency = new OptionDefinition<int>(
            1, "server.throttle.per_host_concurrency", "Server:Throttle:PerHostConcurrency", "REGARD_THROTTLE_PER_HOST_CONCURRENCY", 0);

        // --- Background metadata refresh -----------------------------------------------------------
        // A deliberately small, low-priority trickle. yt-dlp extractions are paced 5-20 s apart per host
        // and are NOT covered by the throttle's hour/day caps (those count downloads only), so batch size
        // is the only real control over how much of the budget this spends. It also yields to downloads
        // and syncs outright — see RefreshMetadataJob. Global/server options.

        /// <summary>Master switch for the periodic metadata refresh. Global/server option.</summary>
        public static readonly OptionDefinition<bool> Server_MetadataRefresh_Enabled = new OptionDefinition<bool>(
            true, "server.metadata_refresh.enabled", "Server:MetadataRefresh:Enabled", "REGARD_METADATA_REFRESH_ENABLED", 0);

        /// <summary>How often the refresh job wakes up, in minutes. Global/server option.</summary>
        public static readonly OptionDefinition<int> Server_MetadataRefresh_IntervalMinutes = new OptionDefinition<int>(
            60, "server.metadata_refresh.interval_minutes", "Server:MetadataRefresh:IntervalMinutes", "REGARD_METADATA_REFRESH_INTERVAL_MINUTES", 0);

        /// <summary>
        /// yt-dlp extractions per run. Each costs one paced round-trip and pushes the shared pacing floor
        /// forward, so keep it small: at 5 a badly-timed run delays a download by under two minutes.
        /// Global/server option.
        /// </summary>
        public static readonly OptionDefinition<int> Server_MetadataRefresh_BatchSize = new OptionDefinition<int>(
            5, "server.metadata_refresh.batch_size", "Server:MetadataRefresh:BatchSize", "REGARD_METADATA_REFRESH_BATCH_SIZE", 0);

        /// <summary>
        /// Return YouTube Dislike lookups per run. Far cheaper than a yt-dlp extraction (a plain HTTP GET
        /// against a different host, 100/min allowed), so this can be an order of magnitude larger.
        /// Global/server option.
        /// </summary>
        public static readonly OptionDefinition<int> Server_MetadataRefresh_RydBatchSize = new OptionDefinition<int>(
            50, "server.metadata_refresh.ryd_batch_size", "Server:MetadataRefresh:RydBatchSize", "REGARD_METADATA_REFRESH_RYD_BATCH_SIZE", 0);

        /// <summary>
        /// Downloaded videos per run to queue a subtitle refetch for. Each becomes a ReprocessVideoJob,
        /// which competes for the same 3 Quartz workers, so keep it low. 0 disables the sweep.
        /// Global/server option.
        /// </summary>
        public static readonly OptionDefinition<int> Server_MetadataRefresh_SubtitleSweepSize = new OptionDefinition<int>(
            2, "server.metadata_refresh.subtitle_sweep_size", "Server:MetadataRefresh:SubtitleSweepSize", "REGARD_METADATA_REFRESH_SUBTITLE_SWEEP_SIZE", 0);

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

        /// <summary>
        /// Per-SponsorBlock-category action, stored as a CSV of "category:action" pairs where action is
        /// chapter | remove | skip (an absent category means keep). Example:
        /// "sponsor:remove,selfpromo:remove,interaction:skip,intro:chapter". YouTube-only. "chapter" and
        /// "remove" are applied by yt-dlp at download time; "skip" is applied non-destructively in the
        /// player. Remove and Skip are mutually exclusive (Remove cuts the file, which would misalign the
        /// player-side Skip timestamps). See Regard.Common.SponsorBlock.SponsorBlockActions.
        ///
        /// Defaults to SponsorBlockActions.DefaultActions ("sponsor:skip"), matching the extension's own
        /// shipped default. Because that default is non-empty, an empty/null value means "unset, inherit
        /// the default" — "off" has to be stored as the literal "none" sentinel instead.
        /// </summary>
        public static readonly OptionDefinition<string> Sponsorblock_Actions = new OptionDefinition<string>(
            Regard.Common.SponsorBlock.SponsorBlockActions.DefaultActions,
            "sponsorblock.actions",
            "Sponsorblock:Actions",
            null,
            OptionFlags.User | OptionFlags.SubscriptionFolder | OptionFlags.Subscription
        );

        /// <summary>
        /// Allow embedding the source site's player (e.g. YouTube) on the watch page for videos that
        /// aren't downloaded. Off by default for privacy: when off, a non-downloaded video shows a
        /// placeholder with "Download now" and "Watch on the original site" instead of loading a
        /// third-party player.
        /// </summary>
        public static readonly OptionDefinition<bool> Ui_AllowEmbedding = new OptionDefinition<bool>(
            false,
            "ui.allow_embedding",
            "Ui:AllowEmbedding",
            null,
            OptionFlags.User
        );

        /// <summary>
        /// External base URL of this Regard instance (e.g. "https://regard.example.com"), used to build
        /// absolute links in outgoing mail such as the password-reset link. This is authoritative: set it
        /// in any real deployment. TLS is terminated by a reverse proxy and no forwarded-headers middleware
        /// is configured, so when this is empty the backend falls back to deriving scheme+host from the
        /// incoming request, which is only correct for same-origin/dev setups.
        /// </summary>
        public static readonly OptionDefinition<string> Server_PublicBaseUrl = new OptionDefinition<string>(
            null,
            "server.public_base_url",
            "PublicBaseUrl",
            "REGARD_PUBLIC_BASE_URL",
            0
        );

        /// <summary>
        /// SMTP server host used to send mail (e.g. password-reset links). When empty, mail is considered
        /// unconfigured and the reset link is written to the server log instead of emailed.
        /// </summary>
        public static readonly OptionDefinition<string> Server_SmtpHost = new OptionDefinition<string>(
            null,
            "smtp.host",
            "Smtp:Host",
            "REGARD_SMTP_HOST",
            0
        );

        /// <summary>
        /// SMTP server port. 587 (STARTTLS) is the usual submission port; 465 is implicit TLS.
        /// </summary>
        public static readonly OptionDefinition<int> Server_SmtpPort = new OptionDefinition<int>(
            587,
            "smtp.port",
            "Smtp:Port",
            "REGARD_SMTP_PORT",
            0
        );

        /// <summary>
        /// SMTP username. When empty, the connection is made without authentication.
        /// </summary>
        public static readonly OptionDefinition<string> Server_SmtpUser = new OptionDefinition<string>(
            null,
            "smtp.user",
            "Smtp:User",
            "REGARD_SMTP_USER",
            0
        );

        /// <summary>
        /// SMTP password (used with Server_SmtpUser).
        /// </summary>
        public static readonly OptionDefinition<string> Server_SmtpPassword = new OptionDefinition<string>(
            null,
            "smtp.password",
            "Smtp:Password",
            "REGARD_SMTP_PASSWORD",
            0
        );

        /// <summary>
        /// From address for outgoing mail. Falls back to Server_SmtpUser when empty.
        /// </summary>
        public static readonly OptionDefinition<string> Server_SmtpFrom = new OptionDefinition<string>(
            null,
            "smtp.from",
            "Smtp:From",
            "REGARD_SMTP_FROM",
            0
        );

        /// <summary>
        /// Use a secure connection (TLS/STARTTLS chosen automatically from the port). Set false only for a
        /// local relay that speaks plaintext.
        /// </summary>
        public static readonly OptionDefinition<bool> Server_SmtpUseSsl = new OptionDefinition<bool>(
            true,
            "smtp.use_ssl",
            "Smtp:UseSsl",
            "REGARD_SMTP_USESSL",
            0
        );

    }
}