using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Regard.Common.API.Admin;
using Regard.Common.API.Settings;
using Regard.Frontend.Shared;
using Regard.Model;
using Regard.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Pages
{
    public partial class Settings
    {
        [Inject] protected BackendService Backend { get; set; }

        // Which tab to open, from the URL (?tab=joblog). Null selects the first tab.
        [Parameter, SupplyParameterFromQuery(Name = "tab")]
        public string TabQuery { get; set; }

        protected ApiThrottleStatus throttle;
        protected bool loading = true;
        protected bool saving = false;
        protected bool saved = false;
        protected string statusMessage = string.Empty;

        protected ApiUserUsage usage;

        // Subscription defaults (a subscription/folder overrides these). "" = inherit; for the bool
        // selects "on"/"off"; the numeric ones are "" = inherit else a number.
        protected string AutoDownloadStr { get; set; } = string.Empty;
        protected string DownloadOrderStr { get; set; } = string.Empty;
        protected string SubMaxCountStr { get; set; } = string.Empty;
        protected string SubMaxSizeStr { get; set; } = string.Empty;
        protected string DeleteWatchedStr { get; set; } = string.Empty;
        protected string MarkDeletedAsWatchedStr { get; set; } = string.Empty;
        protected string DeleteGracePeriodStr { get; set; } = string.Empty;

        // "Default (…)" labels for the inherit option, filled from the resolved global defaults.
        protected string AutoDownloadDefaultLabel { get; set; } = "Default";
        protected string DownloadOrderDefaultLabel { get; set; } = "Default";
        protected string DeleteWatchedDefaultLabel { get; set; } = "Default";
        protected string MarkDeletedAsWatchedDefaultLabel { get; set; } = "Default";

        // "" = inherit default; "0" = unlimited; otherwise a height cap.
        protected string MaxResolutionStr { get; set; } = string.Empty;

        protected bool OverrideVideoCodecs { get; set; }
        protected bool OverrideAudioCodecs { get; set; }
        protected readonly HashSet<string> videoCodecs = new();
        protected readonly HashSet<string> audioCodecs = new();

        // "" = inherit default; "off" = explicitly keep original; otherwise a target container.
        protected string TranscodeVideoStr { get; set; } = string.Empty;
        protected string TranscodeModeVal { get; set; } = "remux";

        protected string RawFormatOverride { get; set; } = string.Empty;
        // "" = inherit default; otherwise a container.
        protected string MergeOutputFormat { get; set; } = string.Empty;

        // "" = inherit default (off); "on" = allow embedding; "off" = never embed.
        protected string AllowEmbeddingStr { get; set; } = string.Empty;

        // Subtitles. "" = inherit default (off); "on" = download; "off" = never.
        protected string SubtitlesStr { get; set; } = string.Empty;
        protected bool IncludeAutoSub { get; set; }
        // "specific" = use the language list; "all" = every available language.
        protected string SubLangMode { get; set; } = "specific";
        protected string SubLangStr { get; set; } = "en";
        // "" = inherit default (best); otherwise a subtitle format token.
        protected string SubFormatStr { get; set; } = string.Empty;

        protected bool ShowSubtitleOptions => SubtitlesStr == "on";

        // "" = inherit the default template; otherwise a yt-dlp -o path/filename template.
        protected string DownloadPath { get; set; } = string.Empty;

        // Per-category SponsorBlock actions ("category:action" CSV); "" = none.
        protected string SponsorblockActions { get; set; } = string.Empty;

        // Per-user cookies. Same convention as the admin page's global jar: null = leave alone,
        // "" = remove, non-empty = replace. Only the CONTENT ever leaves the browser — the server
        // decides where it lands.
        protected bool CookiesConfigured { get; set; }
        private string cookiesFileContent = null;
        protected string cookiesNote = string.Empty;

        protected async Task OnCookiesFile(Microsoft.AspNetCore.Components.Forms.InputFileChangeEventArgs e)
        {
            try
            {
                using var reader = new System.IO.StreamReader(e.File.OpenReadStream(1024 * 1024));
                cookiesFileContent = await reader.ReadToEndAsync();
                cookiesNote = $"{e.File.Name} ready — click Save to apply";
            }
            catch (Exception ex)
            {
                cookiesFileContent = null;
                cookiesNote = "Could not read file: " + ex.Message;
            }
        }

        protected void OnClearCookies()
        {
            // Empty string is the "remove it" signal; the server deletes the file and clears the option.
            cookiesFileContent = string.Empty;
            cookiesNote = "Cookies will be removed — click Save to apply";
        }

        protected string PatternPreview => PatternPreviewHelper.Render(DownloadPath);

        protected bool IsTranscodeTarget =>
            TranscodeVideoStr != string.Empty && TranscodeVideoStr != "off";

        // yt-dlp codec prefixes (substring-matched against vcodec/acodec). Labels are for humans.
        protected record CodecOption(string Label, string Token);

        protected static readonly CodecOption[] VideoCodecOptions =
        {
            new("AV1", "av01"),
            new("VP9", "vp09"),
            new("H.265 / HEVC", "hev1"),
            new("H.264 / AVC", "avc1"),
        };

        protected static readonly CodecOption[] AudioCodecOptions =
        {
            new("Opus", "opus"),
            new("AAC", "mp4a"),
        };

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            throttle = (await Backend.GetThrottleStatus())?.Data;
            var resp = await Backend.GetSettings();
            var s = resp?.Data;
            if (s != null)
                LoadFrom(s);
            usage = (await Backend.GetUserUsage())?.Data;
            loading = false;
        }

        // Human-friendly size, one decimal, largest fitting unit.
        protected static string FormatSize(long bytes)
        {
            const double gb = 1024d * 1024 * 1024, mb = 1024d * 1024, kb = 1024d;
            if (bytes >= gb) return $"{bytes / gb:0.0} GB";
            if (bytes >= mb) return $"{bytes / mb:0.0} MB";
            if (bytes >= kb) return $"{bytes / kb:0.0} KB";
            return $"{bytes} B";
        }

        protected int StoragePercent => (usage?.StorageQuotaBytes is long q && q > 0)
            ? (int)Math.Min(100, 100 * usage.UsedBytes / q)
            : 0;

        private static string BoolToStr(bool? v) => v.HasValue ? (v.Value ? "on" : "off") : string.Empty;
        private static bool? StrToBool(string v) => v == string.Empty ? (bool?)null : v == "on";

        private void LoadFrom(ApiUserSettings s)
        {
            AutoDownloadStr = BoolToStr(s.AutoDownload);
            DownloadOrderStr = s.DownloadOrder.HasValue ? s.DownloadOrder.Value.ToString() : string.Empty;
            SubMaxCountStr = s.DownloadMaxCount?.ToString() ?? string.Empty;
            SubMaxSizeStr = s.DownloadMaxSize?.ToString() ?? string.Empty;
            DeleteWatchedStr = BoolToStr(s.DeleteWatched);
            MarkDeletedAsWatchedStr = BoolToStr(s.MarkDeletedAsWatched);
            DeleteGracePeriodStr = s.DeleteGracePeriod?.ToString() ?? string.Empty;

            AutoDownloadDefaultLabel = $"Default ({(s.AutoDownloadDefault ? "On" : "Off")})";
            DownloadOrderDefaultLabel = $"Default ({SubscriptionEdit.OrderText(s.DownloadOrderDefault)})";
            DeleteWatchedDefaultLabel = $"Default ({(s.DeleteWatchedDefault ? "On" : "Off")})";
            MarkDeletedAsWatchedDefaultLabel = $"Default ({(s.MarkDeletedAsWatchedDefault ? "On" : "Off")})";

            MaxResolutionStr = s.MaxResolution.HasValue ? s.MaxResolution.Value.ToString() : string.Empty;

            OverrideVideoCodecs = s.ExcludedVideoCodecs != null;
            videoCodecs.Clear();
            if (s.ExcludedVideoCodecs != null)
                foreach (var c in s.ExcludedVideoCodecs) videoCodecs.Add(c);

            OverrideAudioCodecs = s.ExcludedAudioCodecs != null;
            audioCodecs.Clear();
            if (s.ExcludedAudioCodecs != null)
                foreach (var c in s.ExcludedAudioCodecs) audioCodecs.Add(c);

            // null = Default (""), "" = Off ("off"), value = the container.
            TranscodeVideoStr = s.TranscodeVideo == null
                ? string.Empty
                : (s.TranscodeVideo.Length == 0 ? "off" : s.TranscodeVideo);
            TranscodeModeVal = string.IsNullOrEmpty(s.TranscodeMode) ? "remux" : s.TranscodeMode;

            RawFormatOverride = s.RawFormatOverride ?? string.Empty;
            MergeOutputFormat = s.MergeOutputFormat ?? string.Empty;

            AllowEmbeddingStr = s.AllowEmbedding.HasValue ? (s.AllowEmbedding.Value ? "on" : "off") : string.Empty;

            SubtitlesStr = s.WriteSubtitles.HasValue ? (s.WriteSubtitles.Value ? "on" : "off") : string.Empty;
            IncludeAutoSub = s.WriteAutoSub ?? false;
            SubLangMode = (s.AllSubs == true) ? "all" : "specific";
            SubLangStr = s.SubLang ?? "en";
            SubFormatStr = s.SubFormat ?? string.Empty;

            DownloadPath = s.DownloadPath ?? string.Empty;
            SponsorblockActions = s.SponsorblockActions ?? string.Empty;
            CookiesConfigured = s.CookiesConfigured;
            cookiesFileContent = null;
            cookiesNote = string.Empty;
        }

        private void ToggleCodec(HashSet<string> set, string token, ChangeEventArgs e)
        {
            if (e.Value is bool on && on) set.Add(token);
            else set.Remove(token);
        }

        private async Task OnSave()
        {
            saving = true;
            statusMessage = string.Empty;

            var request = new ApiUserSettings
            {
                AutoDownload = StrToBool(AutoDownloadStr),
                DownloadOrder = string.IsNullOrEmpty(DownloadOrderStr)
                    ? (VideoOrder?)null
                    : Enum.Parse<VideoOrder>(DownloadOrderStr),
                DownloadMaxCount = string.IsNullOrWhiteSpace(SubMaxCountStr) ? (int?)null : int.Parse(SubMaxCountStr),
                DownloadMaxSize = string.IsNullOrWhiteSpace(SubMaxSizeStr) ? (long?)null : long.Parse(SubMaxSizeStr),
                DeleteWatched = StrToBool(DeleteWatchedStr),
                MarkDeletedAsWatched = StrToBool(MarkDeletedAsWatchedStr),
                DeleteGracePeriod = string.IsNullOrWhiteSpace(DeleteGracePeriodStr) ? (int?)null : int.Parse(DeleteGracePeriodStr),
                MaxResolution = string.IsNullOrEmpty(MaxResolutionStr)
                    ? (int?)null
                    : int.Parse(MaxResolutionStr),
                ExcludedVideoCodecs = OverrideVideoCodecs ? videoCodecs.ToArray() : null,
                ExcludedAudioCodecs = OverrideAudioCodecs ? audioCodecs.ToArray() : null,
                // "" -> inherit (null); "off" -> explicit off (empty string); else the container.
                TranscodeVideo = TranscodeVideoStr == string.Empty
                    ? null
                    : (TranscodeVideoStr == "off" ? string.Empty : TranscodeVideoStr),
                // Only pin the mode when an actual target is chosen; otherwise inherit.
                TranscodeMode = IsTranscodeTarget ? TranscodeModeVal : null,
                RawFormatOverride = string.IsNullOrWhiteSpace(RawFormatOverride) ? null : RawFormatOverride,
                MergeOutputFormat = string.IsNullOrEmpty(MergeOutputFormat) ? null : MergeOutputFormat,
                // "" -> inherit (null); "on" -> true; "off" -> false.
                AllowEmbedding = AllowEmbeddingStr == string.Empty ? (bool?)null : AllowEmbeddingStr == "on",
                // Subtitles: only pin the sub-options when subtitles are explicitly enabled.
                WriteSubtitles = SubtitlesStr == string.Empty ? (bool?)null : SubtitlesStr == "on",
                WriteAutoSub = SubtitlesStr == "on" ? IncludeAutoSub : (bool?)null,
                AllSubs = SubtitlesStr == "on" ? (SubLangMode == "all") : (bool?)null,
                SubLang = (SubtitlesStr == "on" && SubLangMode == "specific" && !string.IsNullOrWhiteSpace(SubLangStr))
                    ? SubLangStr : null,
                SubFormat = (SubtitlesStr == "on" && !string.IsNullOrEmpty(SubFormatStr)) ? SubFormatStr : null,
                DownloadPath = string.IsNullOrWhiteSpace(DownloadPath) ? null : DownloadPath,
                CookiesFileContent = cookiesFileContent,
                SponsorblockActions = string.IsNullOrWhiteSpace(SponsorblockActions) ? null : SponsorblockActions,
            };

            var (resp, httpResp) = await Backend.SaveSettings(request);
            saving = false;
            saved = httpResp.IsSuccessStatusCode;
            statusMessage = saved ? "Saved." : ("Save failed: " + resp?.Message);
        }
    }
}
