using Regard.Backend.Common.Providers;
using Regard.Backend.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.ServiceModel.Syndication;
using System.Threading.Tasks;
using System.Xml;

namespace Regard.Backend.Providers.Rss
{
    public class RssSubscriptionProvider : ISubscriptionProvider
    {
        public string Id => "RSS";

        public string Name => "RSS";

        public bool IsInitialized { get; private set; } = false;

        public Type ConfigurationType => null;

        public Task Configure(object config)
        {
            IsInitialized = true;
            return Task.CompletedTask;
        }

        public void Unconfigure()
        {
            IsInitialized = false;
        }

        public async Task<bool> CanHandleSubscriptionUrl(Uri uri)
        {
            return await TryFetchFeed(uri) != null;
        }

        public async Task<Subscription> CreateSubscription(Uri uri)
        {
            var feed = await TryFetchFeed(uri)
                ?? throw new Exception("The URL does not point to a valid RSS or Atom feed.");
            return new Subscription()
            {
                SubscriptionProviderId = Id,
                SubscriptionId = uri.AbsoluteUri,
                OriginalUrl = uri.ToString(),
                Name = feed.Title.Text,
                Description = feed.Description.Text,
                ThumbnailPath = feed.ImageUrl.AbsoluteUri
            };
        }

        public async IAsyncEnumerable<Video> FetchVideos(Subscription subscription)
        {
            var feed = await TryFetchFeed(new Uri(subscription.SubscriptionId))
                ?? throw new Exception("The subscription URL no longer returns a valid RSS or Atom feed.");

            foreach (var link in feed.Items)
            {
                var uri = link.Links.First().Uri;
                uri = await LinkProcessors.Process(uri);

                yield return new Video()
                {
                    OriginalUrl = uri.ToString(),
                    SubscriptionProviderId = link.Id,
                    Name = link.Title.Text,
                    Published = (link.PublishDate == new DateTimeOffset()) ? link.LastUpdatedTime : link.PublishDate,
                    LastUpdated = link.LastUpdatedTime
                };
            }
        }

        /// <summary>
        /// Fetches and parses the feed at <paramref name="uri"/>, returning null (instead of
        /// throwing) when the URL isn't a feed — e.g. a YouTube channel page served as text/html.
        /// This keeps provider probing (CanHandleSubscriptionUrl) from raising a first-chance
        /// XmlException on every non-feed URL.
        /// </summary>
        private static async Task<SyndicationFeed> TryFetchFeed(Uri uri)
        {
            try
            {
                using var httpClient = new HttpClient();
                using var response = await httpClient.GetAsync(uri);
                if (!response.IsSuccessStatusCode)
                    return null;

                // Only attempt XML parsing when the server says it's a feed. HTML pages
                // (YouTube channels, search results, etc.) are rejected without a parse attempt.
                var mediaType = response.Content.Headers.ContentType?.MediaType;
                if (mediaType == null || !IsFeedContentType(mediaType))
                    return null;

                using var xmlReader = XmlReader.Create(await response.Content.ReadAsStreamAsync());
                return SyndicationFeed.Load(xmlReader);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsFeedContentType(string mediaType)
        {
            return mediaType.Contains("xml", StringComparison.OrdinalIgnoreCase)
                || mediaType.Contains("rss", StringComparison.OrdinalIgnoreCase)
                || mediaType.Contains("atom", StringComparison.OrdinalIgnoreCase);
        }
    }
}
