using Microsoft.Extensions.Configuration;
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
        private readonly IConfiguration configuration;

        private string RootPath => configuration["DownloadDirectory"];

        public VideoStorageService(IConfiguration configuration)
        {
            this.configuration = configuration;
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

                foreach (var file in await Task.Run(() => Directory.GetFiles(dir)))
                {
                    if (file.StartsWith(filePrefix))
                        yield return file;
                }
            }
        }

        public async Task<string> FindVideoFile(Video video)
        {
            return await GetFiles(video)
                .FirstOrDefaultAsync(file => MimeUtility.GetMimeMapping(file).StartsWith("video"));
        }

        public async Task<bool> VerifyIsDownloaded(Video video)
        {
            return (await FindVideoFile(video)) != null;
        }

        public async Task Delete(Video video)
        {
            await foreach (var file in GetFiles(video))
                await Task.Run(() => File.Delete(file));
        }
    }
}
