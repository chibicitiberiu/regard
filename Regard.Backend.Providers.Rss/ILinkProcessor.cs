using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Regard.Backend.Providers.Rss
{
    public interface ILinkProcessor
    {
        Task<Uri> ProcessLink(Uri link);
    }
}
