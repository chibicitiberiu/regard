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
    }
}
