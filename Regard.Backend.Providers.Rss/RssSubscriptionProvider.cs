using Regard.Backend.Common.Providers;
using Regard.Backend.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.ServiceModel.Syndication;
using System.Threading.Tasks;
using System.Xml;

namespace Regard.Backend.Providers
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
            try
            {
                await FetchFeed(uri);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<Subscription> CreateSubscription(Uri uri)
        {
            var feed = await FetchFeed(uri);
            return new Subscription()
            {
                SubscriptionProviderId = Id,
                SubscriptionId = uri.AbsoluteUri,
                Name = feed.Title.Text,
                Description = feed.Description.Text,
                ThumbnailPath = feed.ImageUrl.AbsoluteUri
            };
        }

        public async IAsyncEnumerable<Video> FetchVideos(Subscription subscription)
        {
            var feed = await FetchFeed(new Uri(subscription.SubscriptionId));

            foreach (var link in feed.Items)
                yield return new Video()
                {
                    OriginalUrl = link.Links.First().Uri.ToString(),
                    SubscriptionProviderId = link.Id,
                    Name = link.Title.Text,
                    Published = (link.PublishDate == new DateTimeOffset()) ? link.LastUpdatedTime : link.PublishDate,
                    LastUpdated = link.LastUpdatedTime
                };
        }

        private async Task<SyndicationFeed> FetchFeed(Uri uri)
        {
            var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(uri);
            var xmlReader = XmlReader.Create(await response.Content.ReadAsStreamAsync());
            return SyndicationFeed.Load(xmlReader);
        }
    }
}
