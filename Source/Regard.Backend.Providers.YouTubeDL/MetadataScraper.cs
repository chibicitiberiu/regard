using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Regard.Backend.Providers.YouTubeDL
{
    public static class MetadataScraper
    {
        public class ScrapeInfo
        {
            public string ThumbnailUrl { get; set; }
            public string ChannelTitle { get; set; }
        }

        public static IEnumerable<KeyValuePair<string, string>> ScrapeMetadata(Uri uri)
        {
            var web = new HtmlWeb
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:87.0) Gecko/20100101 Firefox/87.0"
            };

            var doc = web.Load(uri);

            foreach (var metaNode in doc.DocumentNode.SelectNodes("//meta"))
            {
                string name = metaNode.Attributes["name"]?.Value
                        ?? metaNode.Attributes["property"]?.Value
                        ?? metaNode.Attributes["itemprop"]?.Value;

                if (name != null)
                {
                    string value = metaNode.Attributes["content"]?.Value;
                    yield return new KeyValuePair<string, string>(name, value);
                }
            }

            foreach (var linkNode in doc.DocumentNode.SelectNodes("//link[@itemprop]"))
            {
                string itemprop = linkNode.Attributes["itemprop"]?.Value;
                string href = linkNode.Attributes["href"]?.Value;

                if (itemprop != null)
                    yield return new KeyValuePair<string, string>("link:" + itemprop, href);
            }
        }
    }
}
