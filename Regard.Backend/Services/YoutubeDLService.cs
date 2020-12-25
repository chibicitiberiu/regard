using Microsoft.Extensions.Configuration;
using Regard.Backend.Common.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YoutubeDLWrapper;

namespace Regard.Backend.Services
{
    public class YoutubeDLService : IYoutubeDlService
    {
        private readonly YoutubeDLManager ytdlManager = new YoutubeDLManager();
        private readonly ReaderWriterLock ytdlLock = new ReaderWriterLock();
        private YoutubeDL ytdl = null;
        
        public Version CurrentVersion { get; private set; }

        public YoutubeDLService(IConfiguration configuration)
        {
            ytdlManager.StorePath = configuration["DataDirectory"];
            ytdlManager.LatestUrl = configuration["YoutubeDLLatestUrl"];
        }

        public async Task Initialize()
        {
            await ytdlManager.Initialize();
            if (ytdlManager.Versions.Count > 0)
            {
                CurrentVersion = ytdlManager.Versions.Keys.Max();
                ytdl = ytdlManager.Versions[CurrentVersion];
            }
        }

        public async Task DownloadLatest()
        {
            // This will download the new version and add it to "Versions" map
            await ytdlManager.DownloadLatestVersion();

            // Critical section - replace ytdl when nobody uses it            
            try
            {
                ytdlLock.AcquireWriterLock(int.MaxValue);

                // replace ytdl
                CurrentVersion = ytdlManager.Versions.Keys.Max();
                ytdl = ytdlManager.Versions[CurrentVersion];
            }
            finally
            {
                ytdlLock.ReleaseWriterLock();
            }

            // Delete old versions which are no longer required
            await ytdlManager.CleanupOldVersions();
        }

        public async Task UsingYoutubeDL(Func<YoutubeDL, Task> action, int waitTimeout = int.MaxValue)
        {
            try
            {
                ytdlLock.AcquireReaderLock(waitTimeout);

                if (ytdl == null)
                    throw new Exception("YoutubeDL not yet downloaded!");

                await action.Invoke(ytdl);
            }
            finally
            {
                ytdlLock.ReleaseReaderLock();
            }
        }
    }
}
