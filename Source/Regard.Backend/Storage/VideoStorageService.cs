using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeMapping;
using Regard.Backend.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    public class VideoStorageService : IVideoStorageService
    {
        private readonly ILogger log;
        private readonly StorageManager storageManager;

        private string RootPath => storageManager.DownloadDirectory;

        public VideoStorageService(ILogger<VideoStorageService> log,
                                   StorageManager storageManager)
        {
            this.log = log;
            this.storageManager = storageManager;
        }

        private string GetPath(Video video)
        {
            return Path.Combine(RootPath, video.DownloadedPath);
        }

        public async IAsyncEnumerable<string> GetFiles(Video video)
        {
            if (video.DownloadedPath != null)
            {
                var path = GetPath(video);
                var dir = Path.GetDirectoryName(path);
                var filePrefix = Path.GetFileName(path);

                if (Directory.Exists(dir))
                {
                    foreach (var file in await Task.Run(() => Directory.GetFiles(dir)))
                    {
                        string fileName = Path.GetFileName(file);
                        if (fileName.StartsWith(filePrefix))
                            yield return file;
                    }
                }
            }
        }

        public async Task<string> FindVideoFile(Video video)
        {
            await foreach (var file in GetFiles(video))
            {
                string mime = MimeUtility.GetMimeMapping(file);
                if (mime.StartsWith("video"))
                    return file;
            }

            return null;
        }

        public async Task<bool> VerifyIsDownloaded(Video video)
        {
            return (await FindVideoFile(video)) != null;
        }

        public async Task Delete(Video video)
        {
            await foreach (var file in GetFiles(video))
            {
                await Task.Run(() => File.Delete(file));
                log.LogInformation("Deleted file for video {0}: {1}", video, file);
            }
        }

        public async Task<long> CalculateSize(Video video)
        {
            return await GetFiles(video)
                .Select(file => new FileInfo(file))
                .SumAsync(fileInfo => fileInfo.Length);
        }

        public async Task<string> GetMimeType(Video video)
        {
            var videoFile = await FindVideoFile(video);
            return (videoFile != null) ? MimeUtility.GetMimeMapping(videoFile) : null;
        }

        public async Task<Stream> Open(Video video)
        {
            var videoFile = await FindVideoFile(video);
            return (videoFile != null) ? File.OpenRead(videoFile) : null;
        }
    }
}
