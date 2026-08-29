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

        public Version CurrentVersion { get; private set; }

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
            }
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
                using (var @lock = await ytdlLock.WriterLockAsync())
                {
                    // replace ytdl
                    CurrentVersion = ytdlManager.Versions.Keys.Max();
                    ytdl = ytdlManager.Versions[CurrentVersion];
                    log.LogInformation("Update to {0} completed.", CurrentVersion);
                }
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
            return YtdlAntibotArgs.Build(optionManager);
        }

        public Task PaceExtractionAsync(string host) => hostThrottle.PaceExtractionAsync(host);
    }
}
