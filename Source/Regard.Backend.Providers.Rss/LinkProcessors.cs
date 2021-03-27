using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Regard.Backend.Providers.Rss
{
    public static class LinkProcessors
    {
        public static readonly ILinkProcessor[] Processors =
        {
            new RedditLinkProcessor()
        };

        public static async Task<Uri> Process(Uri uri)
        {
            foreach (var processor in Processors)
                uri = await processor.ProcessLink(uri);

            return uri;
        }
    }
}
