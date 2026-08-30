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

        /// <summary>
        /// Per-user yt-dlp cookie jars. Deliberately NOT under <see cref="ThumbnailsDirectory"/> and
        /// deliberately not served: unlike /thumbs there is no static-file mount for this, because these
        /// files are session credentials for the user's Google account.
        /// </summary>
        public string CookiesDirectory { get; }

        public Uri ThumbnailsBaseUrl { get; } = new Uri("thumbs", UriKind.Relative);

        public StorageManager(ILogger<VideoStorageService> log,
                              IConfiguration configuration)
        {
            Log = log;
            DataDirectory = configuration["DataDirectory"];
            ThumbnailsDirectory = Path.Combine(DataDirectory, "Thumbnails");
            CookiesDirectory = Path.Combine(DataDirectory, "Cookies");
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
            Directory.CreateDirectory(CookiesDirectory);   // no UseStaticFiles for this one, ever
        }
    }
}
