using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace Regard.Backend.Services
{
    public class StorageManager
    {
        protected readonly ILogger Log;

        public string DataDirectory { get; }

        public string ThumbnailsDirectory { get; }

        public string DownloadDirectory { get; }

        public Uri ThumbnailsBaseUrl { get; } = new Uri("thumbs", UriKind.Relative);

        public StorageManager(ILogger<VideoStorageService> log,
                              IConfiguration configuration)
        {
            Log = log;
            DataDirectory = configuration["DataDirectory"];
            ThumbnailsDirectory = Path.Combine(DataDirectory, "Thumbnails");
            DownloadDirectory = configuration["DownloadDirectory"];
        }

        public void Initialize(IApplicationBuilder app)
        {
            Directory.CreateDirectory(ThumbnailsDirectory);

            app.UseStaticFiles(new StaticFileOptions()
            {
                FileProvider = new PhysicalFileProvider(ThumbnailsDirectory),
                RequestPath = "/thumbs"
            });

            Directory.CreateDirectory(DownloadDirectory);
        }
    }
}
