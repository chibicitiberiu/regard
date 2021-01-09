using Regard.Backend.Model;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    public interface IVideoStorageService
    {
        Task<bool> VerifyIsDownloaded(Video video);

        IAsyncEnumerable<string> GetFiles(Video video);

        Task<string> FindVideoFile(Video video);

        Task Delete(Video video);

        Task<long> CalculateSize(Video video);
    }
}