using Regard.Backend.Model;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    public interface IVideoStorageService
    {
        Task<bool> VerifyIsDownloaded(Video video);

        IAsyncEnumerable<string> GetFiles(Video video);

        /// <summary>
        /// Files at an explicit output-path prefix, for when <see cref="Video.DownloadedPath"/> can't be
        /// trusted to point at them — notably a failed download, which leaves a .part behind without ever
        /// setting that field.
        /// </summary>
        IAsyncEnumerable<string> GetFilesAt(string outputPathPrefix);

        /// <summary>Deletes everything at an output-path prefix; returns the number removed.</summary>
        Task<int> DeleteAt(string outputPathPrefix);

        /// <summary>
        /// The sidecar subtitle files sitting next to the video, one per language, in the order the
        /// directory yields them. Only browser-usable formats are returned (see <see cref="SubtitleFile"/>).
        /// </summary>
        Task<IReadOnlyList<SubtitleFile>> GetSubtitleFiles(Video video);

        Task<string> FindVideoFile(Video video);

        Task Delete(Video video);

        Task<long> CalculateSize(Video video);

        Task<string> GetMimeType(Video video);

        Task<Stream> Open(Video video);
    }
}