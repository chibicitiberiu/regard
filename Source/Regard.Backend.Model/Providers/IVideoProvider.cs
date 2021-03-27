using Regard.Backend.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Threading.Tasks;

namespace Regard.Backend.Common.Providers
{
    public interface IVideoProvider : IProvider
    {
        Task<bool> CanHandleVideo(Video video);

        Task UpdateMetadata(IEnumerable<Video> videos, bool updateMetadata, bool updateStatistics);
    }
}
