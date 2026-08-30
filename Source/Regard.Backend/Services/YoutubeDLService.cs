using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using Regard.Backend.Common.Services;
using Regard.Backend.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YoutubeDLWrapper;

namespace Regard.Backend.Services
{
    public class YoutubeDLService : IYoutubeDlService
    {
        private readonly ILogger log;
        private readonly YoutubeDLManager ytdlManager;
        private readonly AsyncReaderWriterLock ytdlLock = new AsyncReaderWriterLock();
        private readonly IServiceScopeFactory scopeFactory;
        private readonly HostThrottle hostThrottle;
        private YoutubeDL ytdl = null;
        private volatile IReadOnlyList<string> impersonateTargets = Array.Empty<string>();

        public Version CurrentVersion { get; private set; }

        public IReadOnlyList<string> ImpersonateTargets => impersonateTargets;

        public YoutubeDLService(ILoggerFactory logFactory, IConfiguration configuration,
                                IServiceScopeFactory scopeFactory, HostThrottle hostThrottle)
        {
            log = logFactory.CreateLogger<YoutubeDLService>();
            this.scopeFactory = scopeFactory;
            this.hostThrottle = hostThrottle;
            ytdlManager = new YoutubeDLManager(logFactory)
            {
                StorePath = configuration["DataDirectory"],
                LatestUrl = configuration["YoutubeDLLatestUrl"],
                Debug = configuration.GetValue<bool>("Debug"),
                DebugPath = Path.Combine(configuration["DataDirectory"], "Logs", "ytdl"),
            };
        }

        public async Task Initialize()
        {
            await ytdlManager.Initialize();
            if (ytdlManager.Versions.Count > 0)
            {
                CurrentVersion = ytdlManager.Versions.Keys.Max();
                ytdl = ytdlManager.Versions[CurrentVersion];
                log.LogInformation("Using version {0}:", CurrentVersion);
                await ProbeImpersonateTargets(ytdl);
            }
        }

        /// <summary>
        /// Asks yt-dlp which impersonation targets are usable. This has to be a probe rather than a
        /// config assumption: --impersonate with an unavailable target throws in YoutubeDL.__init__,
        /// i.e. every extraction and download would fail before touching the network. curl_cffi lives in
        /// the Python interpreter, not in the yt-dlp zipapp, so availability varies per host and can
        /// change under us without the version changing.
        /// </summary>
        private async Task ProbeImpersonateTargets(YoutubeDL instance)
        {
            try
            {
                string stdOut = null, stdErr = null;
                int rc = await Task.Run(() => instance.Run(
                    new[] { "--color", "no_color", "--list-impersonate-targets" },
                    out stdOut, out stdErr, timeoutMs: 60000));

                if (rc != 0)
                {
                    log.LogWarning("Could not list yt-dlp impersonate targets (exit {0}): {1}", rc, stdErr);
                    impersonateTargets = Array.Empty<string>();
                    return;
                }

                impersonateTargets = ParseImpersonateTargets(stdOut);
                if (impersonateTargets.Count > 0)
                    log.LogInformation("yt-dlp impersonation available: {0}", string.Join(", ", impersonateTargets));
                else
                    log.LogInformation("yt-dlp impersonation unavailable (curl_cffi is not installed for this Python).");
            }
            catch (Exception ex)
            {
                // Never let the probe break startup — impersonation is an enhancement, not a requirement.
                log.LogWarning(ex, "Failed to probe yt-dlp impersonate targets");
                impersonateTargets = Array.Empty<string>();
            }
        }

        /// <summary>
        /// Parses the --list-impersonate-targets table. Every known target is listed; the ones that can't
        /// be used are tagged "(unavailable)" in the Source column. Client names come back like
        /// "Chrome-110"; we keep just the client ("chrome"), which is what a configured target is matched
        /// against.
        ///
        /// Deliberately strict — only rows below the table rule, with the expected column count and a
        /// plausible client token, count. A phantom target invented from some unrelated stdout line would
        /// make the list non-empty, which is what enables the "auto" setting, and yt-dlp would then abort
        /// on every call.
        /// </summary>
        public static IReadOnlyList<string> ParseImpersonateTargets(string listing)
        {
            var targets = new List<string>();
            if (string.IsNullOrWhiteSpace(listing))
                return targets;

            bool inTable = false;
            foreach (var rawLine in listing.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0)
                    continue;

                // The header rule ("-----") separates the column names from the rows.
                if (!inTable)
                {
                    inTable = line.Length > 2 && line.All(c => c == '-');
                    continue;
                }

                if (line.Contains("(unavailable)"))
                    continue;

                var columns = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (columns.Length < 3)
                    continue;

                var client = columns[0].Split('-')[0].ToLowerInvariant();
                if (client.Length == 0 || !client.All(char.IsLetterOrDigit))
                    continue;

                if (!targets.Contains(client))
                    targets.Add(client);
            }

            return targets;
        }

        public async Task DownloadLatest()
        {
            // This will download the new version and add it to "Versions" map
            log.LogInformation("Checking for new youtube-dl version...");
            await ytdlManager.DownloadLatestVersion();

            // Critical section - replace ytdl when nobody uses it            
            var latest = ytdlManager.Versions.Keys.Max();

            if (CurrentVersion != latest)
            {
                log.LogInformation("New version found {0}!", CurrentVersion);
                YoutubeDL updated;
                using (var @lock = await ytdlLock.WriterLockAsync())
                {
                    // replace ytdl
                    CurrentVersion = ytdlManager.Versions.Keys.Max();
                    ytdl = updated = ytdlManager.Versions[CurrentVersion];
                    log.LogInformation("Update to {0} completed.", CurrentVersion);
                }

                // Outside the writer lock: the probe spawns yt-dlp, and holding the lock would block
                // every extraction for the duration.
                await ProbeImpersonateTargets(updated);
            }
            else log.LogInformation("No new version found!");

            // Delete old versions which are no longer required
            log.LogInformation("Cleaning up old youtube-dl versions...");
            await ytdlManager.CleanupOldVersions(2);
        }

        public async Task UsingYoutubeDL(Func<YoutubeDL, Task> action)
        {
            using var @lock = await ytdlLock.ReaderLockAsync();

            if (ytdl == null)
                throw new Exception("YoutubeDL not yet downloaded!");

            await action.Invoke(ytdl);
        }

        public async Task<T> UsingYoutubeDL<T>(Func<YoutubeDL, Task<T>> action)
        {
            using var @lock = await ytdlLock.ReaderLockAsync();

            if (ytdl == null)
                throw new Exception("YoutubeDL not yet downloaded!");

            return await action.Invoke(ytdl);
        }

        public IReadOnlyList<string> GetAntibotArgs()
        {
            // Scoped IOptionManager resolved per call (this service is a singleton).
            using var scope = scopeFactory.CreateScope();
            var optionManager = scope.ServiceProvider.GetRequiredService<IOptionManager>();
            return YtdlAntibotArgs.Build(optionManager, impersonateTargets, log);
        }

        public Task PaceExtractionAsync(string host) => hostThrottle.PaceExtractionAsync(host);
    }
}
