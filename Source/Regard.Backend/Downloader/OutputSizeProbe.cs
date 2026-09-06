using System;
using System.IO;

namespace Regard.Backend.Downloader
{
    /// <summary>
    /// Sums the on-disk size of the files a single yt-dlp download writes — the liveness signal for the
    /// idle watchdog (<see cref="YoutubeDLWrapper.IdleWatchdog"/>). During the Run phase yt-dlp names
    /// every file for a download "&lt;base&gt;.&lt;something&gt;": fragments "&lt;base&gt;.fNNN.&lt;ext&gt;.part",
    /// the merged "&lt;base&gt;.&lt;ext&gt;", "&lt;base&gt;.info.json", the thumbnail. Matching on a dot
    /// boundary keeps a prefix-related sibling ("&lt;base&gt; 2.*") in the same folder from being counted.
    /// </summary>
    public static class OutputSizeProbe
    {
        /// <summary>True if <paramref name="name"/> is a file produced by the download whose base is
        /// <paramref name="baseName"/> (exact, or on a <c>.</c> boundary).</summary>
        public static bool Belongs(string name, string baseName) =>
            name == baseName || name.StartsWith(baseName + ".", StringComparison.Ordinal);

        /// <summary>
        /// Total bytes of this download's files in <paramref name="dir"/>. Returns <c>-1</c> when the
        /// directory is missing/unreadable ("unknown" — the watchdog then keeps its pure-idle behaviour),
        /// and <c>0</c> when nothing matches yet. A file that vanishes mid-scan (yt-dlp deletes fragments
        /// after a merge) counts as 0 rather than failing the whole sum.
        /// </summary>
        public static long Sum(string dir, string baseName)
        {
            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(baseName))
                return -1;

            string[] files;
            try
            {
                if (!Directory.Exists(dir))
                    return -1;
                files = Directory.GetFiles(dir);
            }
            catch
            {
                return -1;
            }

            long total = 0;
            foreach (var path in files)
            {
                if (!Belongs(Path.GetFileName(path), baseName))
                    continue;
                try
                {
                    total += new FileInfo(path).Length;
                }
                catch
                {
                    // Vanished (merge cleanup) or briefly locked — treat as 0 and keep summing.
                }
            }
            return total;
        }
    }
}
