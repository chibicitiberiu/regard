using Regard.Backend.Configuration;
using Regard.Backend.DB;
using System.Linq;

namespace Regard.Backend.Services
{
    /// <summary>
    /// Single source of truth for "how much has a user downloaded" and "what is their hard quota".
    /// Shared by the admin user list, the per-user settings usage display, the download-block check
    /// in <see cref="Downloader.DownloadVideoJob"/>, and the auto-download limit logic in
    /// <see cref="Downloader.VideoDownloaderService"/>.
    /// </summary>
    public class UserQuotaService
    {
        private readonly DataContext dataContext;
        private readonly IOptionManager optionManager;

        public UserQuotaService(DataContext dataContext, IOptionManager optionManager)
        {
            this.dataContext = dataContext;
            this.optionManager = optionManager;
        }

        /// <summary>
        /// Current usage for a user: number of downloaded videos and total bytes on disk. Bytes,
        /// not MB — <see cref="Model.Video.DownloadedSize"/> is already stored in bytes.
        /// </summary>
        public (int Count, long Bytes) GetUsage(string userId)
        {
            int count = dataContext.Videos.AsQueryable()
                .Where(x => x.Subscription.UserId == userId)
                .Where(x => x.DownloadedPath != null)
                .Count();

            long bytes = dataContext.Videos.AsQueryable()
                .Where(x => x.Subscription.UserId == userId)
                .Where(x => x.DownloadedSize != null)
                .Sum(x => x.DownloadedSize) ?? 0;

            return (count, bytes);
        }

        /// <summary>
        /// The user's effective hard quota (per-user override resolved against the global default).
        /// null = unlimited. Size is converted from the stored MB value to bytes.
        /// </summary>
        public (int? CountQuota, long? SizeQuotaBytes) GetHardQuota(string userId)
        {
            int countQuota = optionManager.GetForUser(Options.User_CountQuota, userId);
            long sizeQuotaMb = optionManager.GetForUser(Options.User_SizeQuota, userId);

            return (
                countQuota >= 0 ? countQuota : (int?)null,
                sizeQuotaMb >= 0 ? sizeQuotaMb * 1024L * 1024L : (long?)null
            );
        }
    }
}
