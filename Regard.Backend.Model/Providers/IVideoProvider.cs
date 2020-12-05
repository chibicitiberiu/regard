using Regard.Backend.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Threading.Tasks;

namespace Regard.Backend.Providers
{
    public interface IVideoProvider
    {
        string ProviderId { get; }

        bool IsInitialized { get; }

        Type ConfigurationType { get; }

        void Configure(object config);

        void Unconfigure();

        Task<bool> CanHandleUrl(Uri uri);

        IAsyncEnumerable<Video> FetchVideos(IEnumerable<Uri> urls);

        Task UpdateMetadata(IEnumerable<Video> videos, bool updateStatistics);
    }
}
