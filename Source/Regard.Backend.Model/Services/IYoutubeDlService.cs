using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using YoutubeDLWrapper;

namespace Regard.Backend.Common.Services
{
    public interface IYoutubeDlService
    {
        Version CurrentVersion { get; }

        Task Initialize();

        Task DownloadLatest();

        Task UsingYoutubeDL(Func<YoutubeDL, Task> action);

        Task<T> UsingYoutubeDL<T>(Func<YoutubeDL, Task<T>> action);

        /// <summary>
        /// Server-wide yt-dlp anti-bot args (cookies, sleeps) to pass as <c>extraArgs</c> to a yt-dlp call,
        /// so providers (which can't read options) still get cookie'd/paced extraction. A fresh list each
        /// call — never shared instance state.
        /// </summary>
        IReadOnlyList<string> GetAntibotArgs();

        /// <summary>
        /// Impersonation targets the current yt-dlp can actually use (client names, lowercased, e.g.
        /// "chrome"), probed once per yt-dlp version via --list-impersonate-targets. Empty when curl_cffi
        /// is missing. Passing --impersonate with a target that isn't in here makes yt-dlp abort before it
        /// does any work, so every caller must check first.
        /// </summary>
        IReadOnlyList<string> ImpersonateTargets { get; }

        /// <summary>Short per-host pace before a metadata extraction (download throttling). No-op when disabled.</summary>
        Task PaceExtractionAsync(string host);
    }
}
