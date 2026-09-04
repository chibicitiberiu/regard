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

        /// <summary>
        /// Timestamped database snapshots. Derived from <see cref="DataDirectory"/> rather than being an
        /// admin setting, for the same reason the cookies path is: the retention policy *deletes files*
        /// here, so a settable value would be an arbitrary-deletion primitive pointed at whatever an
        /// admin typed — and the obvious thing to type is the data directory itself, which holds the
        /// live database, the JWT secret and the DataProtection keys.
        ///
        /// Like <see cref="CookiesDirectory"/> this must never be served: a snapshot contains every
        /// password hash in the install. Off-box copies come from bind-mounting this path, not from
        /// pointing the app somewhere else.
        /// </summary>
        public string BackupDirectory { get; }

        /// <summary>
        /// Where NLog writes <c>regard-&lt;date&gt;.log</c>. The path is otherwise expressed only inside
        /// nlog.config (via the DataDirectory that Program.cs seeds into NLog's GlobalDiagnosticsContext),
        /// so this property exists to give the C# side one definition instead of several ad-hoc
        /// Path.Combine calls.
        ///
        /// Never served, for the same reason as <see cref="CookiesDirectory"/> and
        /// <see cref="BackupDirectory"/>: the logs carry absolute paths, yt-dlp command lines and the
        /// location of the cookie jars. The admin log viewer reads them through an authorized endpoint
        /// that resolves a requested name against a directory listing — it never builds a path from
        /// anything a client sent.
        /// </summary>
        public string LogsDirectory { get; }

        /// <summary>
        /// Per-invocation yt-dlp stdout captures. Written only when <c>Debug</c> is on (development),
        /// and pruned by the maintenance sweep. Never served, as above.
        /// </summary>
        public string YtdlLogsDirectory { get; }

        public Uri ThumbnailsBaseUrl { get; } = new Uri("thumbs", UriKind.Relative);

        public StorageManager(ILogger<VideoStorageService> log,
                              IConfiguration configuration)
        {
            Log = log;
            DataDirectory = configuration["DataDirectory"];
            ThumbnailsDirectory = Path.Combine(DataDirectory, "Thumbnails");
            CookiesDirectory = Path.Combine(DataDirectory, "Cookies");
            BackupDirectory = Path.Combine(DataDirectory, "Backups");
            LogsDirectory = Path.Combine(DataDirectory, "Logs");
            YtdlLogsDirectory = Path.Combine(LogsDirectory, "ytdl");
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

            // Same rule as Cookies: never served. Created here because Startup.Configure calls
            // Initialize immediately before ApplyMigrations, and the pre-migration snapshot needs
            // somewhere to write. DatabaseBackupService re-creates it defensively anyway, so the
            // ordering is a convenience rather than a dependency.
            Directory.CreateDirectory(BackupDirectory);
        }
    }
}
