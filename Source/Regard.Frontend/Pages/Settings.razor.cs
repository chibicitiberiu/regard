using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Regard.Common.API.Settings;
using Regard.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Pages
{
    public partial class Settings
    {
        [Inject] protected BackendService Backend { get; set; }

        protected bool loading = true;
        protected bool saving = false;
        protected bool saved = false;
        protected string statusMessage = string.Empty;

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
            var resp = await Backend.GetSettings();
            var s = resp?.Data;
            if (s != null)
                LoadFrom(s);
            loading = false;
        }

        private void LoadFrom(ApiUserSettings s)
        {
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
            };

            var (resp, httpResp) = await Backend.SaveSettings(request);
            saving = false;
            saved = httpResp.IsSuccessStatusCode;
            statusMessage = saved ? "Saved." : ("Save failed: " + resp?.Message);
        }
    }
}
