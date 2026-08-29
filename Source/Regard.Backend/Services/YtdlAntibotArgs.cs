using Regard.Backend.Configuration;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Regard.Backend.Services
{
    /// <summary>
    /// Server-wide yt-dlp anti-bot arguments applied to EVERY yt-dlp invocation (download + extraction):
    /// cookies (to clear YouTube's "confirm you're not a bot" gate) and an inter-request sleep. Built as a
    /// fresh per-call list on demand so it is never shared mutable state on the singleton YoutubeDL
    /// instance (which is used concurrently once the job pool is > 1).
    /// </summary>
    public static class YtdlAntibotArgs
    {
        public static List<string> Build(IOptionManager optionManager)
        {
            var args = new List<string>();

            // Cookies apply regardless of the throttle toggle — they're what clears the bot gate.
            var cookiesFile = optionManager.GetGlobal(Options.Server_Ytdl_CookiesFile);
            if (!string.IsNullOrWhiteSpace(cookiesFile) && File.Exists(cookiesFile))
            {
                args.Add("--cookies");
                args.Add(cookiesFile);
            }

            // Sleeps are throttle behavior.
            if (optionManager.GetGlobal(Options.Server_Throttle_Enabled))
            {
                int sleepRequests = optionManager.GetGlobal(Options.Server_Ytdl_SleepRequests);
                if (sleepRequests > 0)
                {
                    args.Add("--sleep-requests");
                    args.Add(sleepRequests.ToString(CultureInfo.InvariantCulture));
                }
            }

            return args;
        }
    }
}
