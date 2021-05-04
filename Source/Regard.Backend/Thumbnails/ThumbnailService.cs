using MimeMapping;
using Regard.Backend.Common.Utils;
using Regard.Backend.Model;
using Regard.Backend.Services;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Regard.Backend.Thumbnails
{
    public class ThumbnailService
    {
        private readonly StorageManager storageManager;

        static readonly Uri VideoDefault = new("img/thumb_default_video.png", UriKind.Relative);

        // TODO
        static readonly Uri SubscriptionDefault = new("img/thumb_default_video.png", UriKind.Relative);

        public ThumbnailService(StorageManager storageManager)
        {
            this.storageManager = storageManager;
        }

        private string GetThumbnailPath(Video video)
        {
            return Path.Combine(storageManager.ThumbnailsDirectory, video.ThumbnailPath);
        }

        private string GetThumbnailPath(Subscription subscription)
        {
            return Path.Combine(storageManager.ThumbnailsDirectory, subscription.ThumbnailPath);
        }

        public Uri GetThumbnail(Subscription subscription)
        {
            if (subscription.ThumbnailPath == null)
                return SubscriptionDefault;

            if (subscription.ThumbnailPath.StartsWith("http"))
                return new Uri(subscription.ThumbnailPath);

            if (File.Exists(GetThumbnailPath(subscription)))
                return storageManager.ThumbnailsBaseUrl.Join(subscription.ThumbnailPath);

            return SubscriptionDefault;
        }

        public Uri GetThumbnail(Video video)
        {
            if (video.ThumbnailPath == null)
                return VideoDefault;

            if (video.ThumbnailPath.StartsWith("http"))
                return new Uri(video.ThumbnailPath);

            if (File.Exists(GetThumbnailPath(video)))
                return storageManager.ThumbnailsBaseUrl.Join(video.ThumbnailPath);

            return VideoDefault;
        }

        private string GeneratePath(Subscription subscription)
        {
            return $"s{subscription.Id}/thumb";
        }

        private string GeneratePath(Video video)
        {
            return $"s{video.SubscriptionId}/{video.Id}";
        }

        public async Task Fetch(Subscription subscription)
        {
            if (subscription.ThumbnailPath.StartsWith("http"))
                subscription.ThumbnailPath = await FetchInternal(subscription.ThumbnailPath, GeneratePath(subscription));
        }

        public async Task Fetch(Video video)
        {
            if (video.ThumbnailPath.StartsWith("http"))
                video.ThumbnailPath = await FetchInternal(video.ThumbnailPath, GeneratePath(video));
        }

        private async Task<string> FetchInternal(string url, string generatedPath)
        {
            // Fetch resource
            var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            // Create output dir, resolve filename
            string ext = MimeUtility.GetExtensions(response.Content.Headers.ContentType.MediaType).FirstOrDefault();
            if (ext == null)
                throw new ArgumentException($"Cannot fetch thumbnail, unknown mime type {response.Content.Headers.ContentType.MediaType}");

            string relPath = generatedPath + "." + ext;
            string absPath = Path.Combine(storageManager.ThumbnailsDirectory, relPath);

            Directory.CreateDirectory(Path.GetDirectoryName(absPath));

            // Download image
            using var stream = await response.Content.ReadAsStreamAsync();
            using var output = File.OpenWrite(absPath);
            await stream.CopyToAsync(output);
            stream.Close();
            output.Close();

            return relPath;
        }
    }
}
