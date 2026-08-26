using Microsoft.Extensions.Logging;
using Regard.Backend.Model;
using Regard.Backend.Thumbnails;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Regard.Backend.Metadata
{
    /// <summary>
    /// Writes Kodi/Jellyfin-compatible metadata (NFO sidecars + poster image) so a Jellyfin
    /// "Shows" library displays each subscription as a Show and each video as an Episode with
    /// title, plot, air date, episode number, and artwork.
    ///
    /// Numbering: Season = video publish year, Episode = <see cref="Video.PlaylistIndex"/>
    /// (a globally-unique, stable-per-video counter). These are the single source of truth for
    /// both the on-disk filename (via <see cref="EpisodeCode"/>) and the episode NFO, so they
    /// never drift. All writes are best-effort: failures are logged and swallowed, never
    /// propagated into the download/sync flow.
    /// </summary>
    public class MetadataService
    {
        private readonly ILogger<MetadataService> log;
        private readonly ThumbnailService thumbnailService;

        public MetadataService(ILogger<MetadataService> log, ThumbnailService thumbnailService)
        {
            this.log = log;
            this.thumbnailService = thumbnailService;
        }

        /// <summary>Season number for a video (its publish year, falling back to discovery year).</summary>
        public int Season(Video video)
        {
            var date = (video.Published == default) ? video.Discovered : video.Published;
            return (date == default) ? 1 : date.Year;
        }

        /// <summary>Episode number for a video (its globally-unique playlist index).</summary>
        public int Episode(Video video) => video.PlaylistIndex;

        /// <summary>The "SxxExx" code embedded into the download filename so Jellyfin resolves episodes.</summary>
        public string EpisodeCode(Video video) => $"S{Season(video)}E{Episode(video)}";

        /// <summary>
        /// Writes the per-episode NFO next to the downloaded file (<paramref name="basePathNoExt"/> + ".nfo").
        /// </summary>
        public async Task WriteEpisodeNfo(Video video, Subscription subscription, string basePathNoExt)
        {
            try
            {
                var aired = ((video.Published == default) ? video.Discovered : video.Published).ToString("yyyy-MM-dd");

                var doc = new XDocument(
                    new XDeclaration("1.0", "utf-8", null),
                    new XElement("episodedetails",
                        new XElement("title", video.Name ?? string.Empty),
                        new XElement("season", Season(video)),
                        new XElement("episode", Episode(video)),
                        new XElement("plot", video.Description ?? string.Empty),
                        new XElement("aired", aired),
                        new XElement("studio", video.UploaderName ?? "YouTube"),
                        new XElement("uniqueid",
                            new XAttribute("type", "youtube"),
                            new XAttribute("default", "true"),
                            video.VideoId ?? string.Empty),
                        new XElement("thumb", Path.GetFileName(basePathNoExt) + "-thumb.jpg")));

                await SaveAsync(doc, basePathNoExt + ".nfo");
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Failed to write episode NFO for video {0}", video);
            }
        }

        /// <summary>
        /// Writes/refreshes the show-level tvshow.nfo and copies the channel poster into
        /// <paramref name="showDir"/>. Idempotent — safe to call on every sync.
        /// </summary>
        public async Task WriteShowMetadata(Subscription subscription, string showDir)
        {
            try
            {
                var doc = new XDocument(
                    new XDeclaration("1.0", "utf-8", null),
                    new XElement("tvshow",
                        new XElement("title", subscription.Name ?? string.Empty),
                        new XElement("plot", subscription.Description ?? string.Empty),
                        new XElement("studio", "YouTube"),
                        new XElement("uniqueid",
                            new XAttribute("type", "youtube"),
                            new XAttribute("default", "true"),
                            subscription.SubscriptionId ?? string.Empty)));

                await SaveAsync(doc, Path.Combine(showDir, "tvshow.nfo"));
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Failed to write tvshow.nfo for subscription {0}", subscription);
            }

            try
            {
                var src = thumbnailService.TryGetLocalFile(subscription);
                if (src != null)
                {
                    var ext = Path.GetExtension(src);
                    if (string.IsNullOrEmpty(ext))
                        ext = ".jpg";
                    File.Copy(src, Path.Combine(showDir, "poster" + ext), overwrite: true);
                }
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Failed to write poster for subscription {0}", subscription);
            }
        }

        private static async Task SaveAsync(XDocument doc, string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            await using var stream = File.Create(path);
            await doc.SaveAsync(stream, SaveOptions.None, CancellationToken.None);
        }
    }
}
