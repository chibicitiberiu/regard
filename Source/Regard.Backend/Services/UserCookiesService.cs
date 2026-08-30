using Microsoft.Extensions.Logging;
using Regard.Backend.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    /// <summary>
    /// Owns each user's yt-dlp cookies.txt.
    ///
    /// The security shape here is the important part. The option this writes
    /// (<see cref="Options.Server_Ytdl_CookiesFile"/>) holds a filesystem path that is later handed to
    /// yt-dlp as <c>--cookies &lt;path&gt;</c>. yt-dlp both reads that file and writes the jar back to it
    /// when the run finishes, so a user who could choose the path would get an arbitrary file read AND an
    /// arbitrary overwrite — the database included. So:
    ///
    ///   * callers pass cookie **content**, never a path;
    ///   * the path is derived here from the authenticated user's id;
    ///   * the id is validated against a strict allowlist before it touches the filesystem.
    ///
    /// The files live outside any static-file mount (see StorageManager.CookiesDirectory) because they
    /// are effectively session credentials for the user's Google account.
    /// </summary>
    public class UserCookiesService
    {
        /// <summary>Matches an ASP.NET Identity id (a GUID string). Anything else is refused outright.</summary>
        private static bool IsSafeUserId(string userId)
            => !string.IsNullOrEmpty(userId)
               && userId.Length <= 64
               && userId.All(c => char.IsLetterOrDigit(c) || c == '-');

        /// <summary>Cookie jars are text; this is far above any real Netscape cookies.txt.</summary>
        public const int MaxBytes = 1024 * 1024;

        private readonly StorageManager storageManager;
        private readonly ILogger<UserCookiesService> log;

        public UserCookiesService(StorageManager storageManager, ILogger<UserCookiesService> log)
        {
            this.storageManager = storageManager;
            this.log = log;
        }

        /// <summary>The jar path for a user, or null if the id isn't a shape we'll put on disk.</summary>
        public string PathFor(string userId)
        {
            if (!IsSafeUserId(userId))
            {
                log.LogWarning("Refusing to build a cookies path for an unexpected user id shape.");
                return null;
            }

            return Path.Combine(storageManager.CookiesDirectory, userId + ".txt");
        }

        /// <summary>
        /// Whether this user's own jar is actually in use. Takes the stored option value, not just the
        /// file: yt-dlp REWRITES the jar at the end of every run it's given, so an extraction still in
        /// flight when the user hits Remove can recreate the file seconds later. Judging by the file
        /// alone made the UI report "your own cookies are in use" for an orphan nothing would ever read.
        /// </summary>
        public bool HasCookies(string userId, string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
                return false;

            var expected = PathFor(userId);
            return expected != null
                && string.Equals(expected, configuredPath, StringComparison.Ordinal)
                && File.Exists(expected);
        }

        /// <summary>
        /// Applies an upload. <paramref name="content"/> follows the same convention the admin page uses
        /// for the global jar: null leaves it alone, empty removes it, anything else replaces it.
        /// Returns the option value to store (the path, or "" when removed), or null for "no change".
        /// </summary>
        public async Task<string> ApplyAsync(string userId, string content)
        {
            if (content == null)
                return null;

            var path = PathFor(userId);
            if (path == null)
                throw new InvalidOperationException("Could not determine a cookies path for this account.");

            if (content.Length == 0)
            {
                if (File.Exists(path))
                    File.Delete(path);
                // The option is cleared too, and that's what decides whether the jar is used again — an
                // in-flight yt-dlp run can still recreate the file after this delete, and it must not
                // count as "configured" when it does.
                return "";
            }

            if (Encoding.UTF8.GetByteCount(content) > MaxBytes)
                throw new ArgumentException("That cookies file is too large.");

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            // Temp-then-move, so a download reading the jar never sees a half-written file.
            var tmp = path + ".tmp";
            await File.WriteAllTextAsync(tmp, content);
            File.Move(tmp, path, overwrite: true);

            return path;
        }

        /// <summary>Removes a user's jar entirely (account deletion).</summary>
        public void Delete(string userId)
        {
            try
            {
                var path = PathFor(userId);
                if (path != null && File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Could not delete cookies for user");
            }
        }
    }
}
