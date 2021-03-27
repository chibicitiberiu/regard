using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Regard.Backend.Providers.Rss
{
    public class RedditLinkProcessor : ILinkProcessor
    {
        private async Task<Uri> GetOriginalUrl(string linkId)
        {
            var encLinkId = HttpUtility.UrlEncode(linkId);

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("UserAgent", "Regard");
            var resp = await client.GetAsync($"https://www.reddit.com/api/info/.json?id=t3_{encLinkId}");
            if (!resp.IsSuccessStatusCode)
                return null;

            using var textReader = new StreamReader(await resp.Content.ReadAsStreamAsync());
            using var jsonReader = new JsonTextReader(textReader);
            var json = await JObject.LoadAsync(jsonReader);
            
            var children = json["data"]?["children"];
            if (children != null && children.HasValues)
            {
                var url = children.First?["data"]?["url"]?.ToObject<string>();
                return (url != null) ? new Uri(url) : null;
            }

            return null;
        }

        public async Task<Uri> ProcessLink(Uri link)
        {
            string linkId = null;

            if (link.Host.EndsWith("reddit.com") 
                && link.Segments.Length >= 5
                && link.Segments[3] == "comments/")
            {
                linkId = link.Segments[4];
            }
            if (link.Host.EndsWith("redd.it")
                && link.Segments.Length >= 2)
            {
                linkId = link.Segments[1];
            }

            if (linkId != null)
            {
                var originalUrl = await GetOriginalUrl(linkId.Trim('/'));
                if (originalUrl != null)
                    return originalUrl;
            }

            return link;
        }
    }
}
