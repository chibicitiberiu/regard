using MimeMapping;
using Regard.Backend.Common.Utils;
using Regard.Backend.Model;
using Regard.Backend.Services;
using System;
using System.Collections.Generic;
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

        // Allowed extensions for a user-uploaded icon. Raster only — an SVG served same-origin from
        // /thumbs could carry <script> (stored XSS), so it's deliberately excluded.
        static readonly HashSet<string> AllowedIconExtensions =
            new(StringComparer.OrdinalIgnoreCase) { "png", "jpg", "jpeg", "gif", "webp" };

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

        /// <summary>
        /// Returns the absolute path of the locally-cached thumbnail file, or null if the
        /// thumbnail hasn't been fetched yet (still a remote URL) or isn't on disk.
        /// </summary>
        public string TryGetLocalFile(Subscription subscription)
        {
            if (subscription.ThumbnailPath == null || subscription.ThumbnailPath.StartsWith("http"))
                return null;
            var path = GetThumbnailPath(subscription);
            return File.Exists(path) ? path : null;
        }

        /// <inheritdoc cref="TryGetLocalFile(Subscription)"/>
        public string TryGetLocalFile(Video video)
        {
            if (video.ThumbnailPath == null || video.ThumbnailPath.StartsWith("http"))
                return null;
            var path = GetThumbnailPath(video);
            return File.Exists(path) ? path : null;
        }

        public Uri GetThumbnail(Subscription subscription)
        {
            // While the thumbnail is still a remote URL (not yet cached by FetchThumbnailsJob), serve the
            // local placeholder rather than the raw yt3.googleusercontent.com URL — the browser blocks
            // that cross-origin <img> (ERR_BLOCKED_BY_ORB) on first paint.
            if (subscription.ThumbnailPath == null || subscription.ThumbnailPath.StartsWith("http"))
                return SubscriptionDefault;

            // Cache-buster: a replaced icon reuses the same s{id}/thumb.ext filename, so append the file's
            // mtime as ?v= so the browser refetches. Only here (the file exists) — never on the placeholder.
            var fi = new FileInfo(GetThumbnailPath(subscription));
            if (fi.Exists)
                return new Uri(storageManager.ThumbnailsBaseUrl.Join(subscription.ThumbnailPath)
                    + "?v=" + fi.LastWriteTimeUtc.Ticks, UriKind.Relative);

            return SubscriptionDefault;
        }

        public Uri GetThumbnail(Video video)
        {
            if (video.ThumbnailPath == null || video.ThumbnailPath.StartsWith("http"))
                return VideoDefault;

            if (File.Exists(GetThumbnailPath(video)))
                return storageManager.ThumbnailsBaseUrl.Join(video.ThumbnailPath);

            return VideoDefault;
        }

        private string GeneratePath(Subscription subscription)
        {
            return $"s{subscription.Id}/thumb";
        }

        /// <summary>
        /// Stores a user-uploaded custom icon for a subscription and returns the relative path to save on
        /// <see cref="Subscription.ThumbnailPath"/>. Validates the extension against a raster allowlist
        /// (rejecting SVG etc.), removes any prior <c>thumb.*</c> so an extension change leaves no orphan,
        /// and writes atomically. Throws <see cref="ArgumentException"/> for a disallowed/empty extension.
        /// </summary>
        public string SetCustom(Subscription subscription, byte[] bytes, string fileName)
        {
            string ext = Path.GetExtension(fileName ?? string.Empty).TrimStart('.').ToLowerInvariant();
            if (!AllowedIconExtensions.Contains(ext))
                throw new ArgumentException($"Unsupported image type '{ext}'. Allowed: png, jpg, gif, webp.");

            string relPath = GeneratePath(subscription) + "." + ext;
            string absPath = Path.Combine(storageManager.ThumbnailsDirectory, relPath);
            string dir = Path.GetDirectoryName(absPath);
            Directory.CreateDirectory(dir);

            // Drop any previous thumb.* (e.g. a .png being replaced by a .jpg) to avoid a stale orphan.
            foreach (var old in Directory.GetFiles(dir, "thumb.*"))
                File.Delete(old);

            string tmp = absPath + ".tmp";
            File.WriteAllBytes(tmp, bytes);
            File.Move(tmp, absPath, overwrite: true);

            return relPath;
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
