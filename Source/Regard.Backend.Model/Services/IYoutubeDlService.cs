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
    }
}
