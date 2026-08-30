using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Regard.Backend.Services
{
    /// <summary>
    /// Turns SubRip (.srt) into WebVTT, because a browser &lt;track&gt; only understands WebVTT.
    ///
    /// We get .srt files at all because DownloadVideoJob forces "--convert-subs srt" when SponsorBlock
    /// *remove* is active, so yt-dlp's ModifyChapters postprocessor can re-time the cues to match the cut
    /// file. Everything else lands as .vtt and is served untouched.
    /// </summary>
    public static class SubtitleConverter
    {
        // SubRip separates milliseconds with a comma; WebVTT requires a dot. Both timestamps on the line
        // are rewritten in one pass. Hours are optional in some writers, hence the {1,3} on the first group.
        private static readonly Regex TimecodeLine = new Regex(
            @"^(?<start>\d{1,3}:\d{2}:\d{2}),(?<sms>\d{3})\s*-->\s*(?<end>\d{1,3}:\d{2}:\d{2}),(?<ems>\d{3})(?<rest>.*)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // A bare integer on its own line is SubRip's cue counter. WebVTT allows cue identifiers, so these
        // are harmless to keep, but dropping them keeps the output clean and avoids a numeric id colliding
        // with anything a player does with cue ids.
        private static readonly Regex CueIndexLine = new Regex(
            @"^\d+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Cue placement settings that follow the timecode ("align:start position:0% line:90%"). YouTube's
        // ASR tracks pin every cue to the left edge, which is why they render ragged-left instead of
        // centred like hand-written ones. Dropping the settings lets the player use its default
        // bottom-centre placement, so both kinds of track look the same.
        //
        // The trade-off, stated plainly: a subtitle that deliberately positions a cue (on-screen signage,
        // a speaker label pinned to one side) loses that placement. Nothing in this library does it, and
        // consistent centring is what a viewer expects.
        private static readonly Regex CuePlacement = new Regex(
            @"\s+(?:align|position|line|size|vertical|region):\S+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex VttTimecodeLine = new Regex(
            @"^(?<times>\d{1,3}:\d{2}:\d{2}\.\d{3}\s*-->\s*\d{1,3}:\d{2}:\d{2}\.\d{3})(?<settings>.*)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Serves a sidecar as WebVTT the player can use: converts SubRip, and strips per-cue placement
        /// so every track renders in the same place.
        /// </summary>
        public static string ToWebVtt(string content, string format)
        {
            string vtt = string.Equals(format, "srt", StringComparison.OrdinalIgnoreCase)
                ? SrtToVtt(content)
                : content ?? string.Empty;

            return NormalizeCuePlacement(vtt);
        }

        /// <summary>Removes cue placement settings, leaving the timecodes untouched.</summary>
        public static string NormalizeCuePlacement(string vtt)
        {
            if (string.IsNullOrEmpty(vtt))
                return vtt;

            if (vtt[0] == '﻿')
                vtt = vtt.Substring(1);

            var lines = vtt.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var output = new StringBuilder();

            foreach (var line in lines)
            {
                var match = VttTimecodeLine.Match(line);
                if (match.Success && match.Groups["settings"].Value.Length > 0)
                    output.Append(match.Groups["times"].Value).Append('\n');
                else
                    output.Append(line).Append('\n');
            }

            return output.ToString();
        }

        public static string SrtToVtt(string srt)
        {
            if (srt == null)
                return "WEBVTT\n\n";

            // Strip a UTF-8 BOM if the decoder left one: a BOM before "WEBVTT" makes the whole file fail
            // to parse in some browsers.
            if (srt.Length > 0 && srt[0] == '﻿')
                srt = srt.Substring(1);

            var output = new StringBuilder();
            output.Append("WEBVTT\n\n");

            var lines = srt.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            bool previousWasBlank = true;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                // Only drop a counter that sits where a counter goes: at the start of a cue block and
                // immediately followed by a timecode. A subtitle whose entire text is a number ("42")
                // would otherwise vanish.
                if (previousWasBlank && CueIndexLine.IsMatch(line.Trim())
                    && i + 1 < lines.Length && TimecodeLine.IsMatch(lines[i + 1]))
                {
                    continue;
                }

                var match = TimecodeLine.Match(line);
                if (match.Success)
                {
                    output.Append(match.Groups["start"].Value).Append('.').Append(match.Groups["sms"].Value)
                          .Append(" --> ")
                          .Append(match.Groups["end"].Value).Append('.').Append(match.Groups["ems"].Value)
                          .Append(match.Groups["rest"].Value)
                          .Append('\n');
                }
                else
                {
                    output.Append(line).Append('\n');
                }

                previousWasBlank = string.IsNullOrWhiteSpace(line);
            }

            return output.ToString();
        }
    }
}
