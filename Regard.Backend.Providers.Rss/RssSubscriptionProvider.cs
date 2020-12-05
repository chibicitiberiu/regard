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
        public string ProviderId => "RSS";

        public string Name => "RSS";

        public bool IsInitialized { get; private set; } = false;

        public Type ConfigurationType => null;

        public void Configure(object config)
        {
            IsInitialized = true;
        }

        public void Unconfigure()
        {
            IsInitialized = false;
        }

        public async Task<bool> CanHandleUrl(Uri uri)
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
                SubscriptionProviderId = ProviderId,
                SubscriptionId = uri.AbsoluteUri,
                Name = feed.Title.Text,
                Description = feed.Description.Text,
                ThumbnailPath = feed.ImageUrl.AbsoluteUri
            };
        }

        public async IAsyncEnumerable<Uri> FetchVideos(Subscription subscription)
        {
            var feed = await FetchFeed(new Uri(subscription.SubscriptionId));

            foreach (var link in feed.Links)
                yield return link.Uri;
        }

        async Task<SyndicationFeed> FetchFeed(Uri uri)
        {
            var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(uri);
            var xmlReader = XmlReader.Create(await response.Content.ReadAsStreamAsync());
            return SyndicationFeed.Load(xmlReader);
        }
    }
}
