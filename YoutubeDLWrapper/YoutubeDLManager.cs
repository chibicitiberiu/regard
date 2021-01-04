using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace YoutubeDLWrapper
{
    public class YoutubeDLManager
    {
        public string StorePath { get; set; }

        public string LatestUrl { get; set; } = "https://youtube-dl.org/downloads/latest/youtube-dl";

        public string PythonPath { get; private set; }

        public Dictionary<Version, YoutubeDL> Versions { get; } = new Dictionary<Version, YoutubeDL>();

        private async Task<bool> IsYoutubeDl(string path)
        {
            // check if filename starts with youtube-dl (we store them as youtube-dl-<version>)
            bool filenameOk = Path.GetFileName(path).ToLower().StartsWith("youtube-dl");
            
            // check if file contains python shebang
            var firstLine = await Task.Run(() => File.ReadLines(path).FirstOrDefault() ?? "");
            bool isPythonExe = firstLine.StartsWith("#!") && firstLine.Contains("python");

            // file is probably ok :)
            return filenameOk && isPythonExe;
        }

        public async Task Initialize()
        {
            if (StorePath == null)
                throw new ArgumentNullException("StorePath not set");

            PythonPath = PythonFinder.FindPython3();

            var files = await Task.Run(() => Directory.GetFiles(StorePath));
            foreach (var file in files)
            {
                if (await IsYoutubeDl(file))
                {
                    var ytdl = new YoutubeDL(file, PythonPath);
                    try
                    {
                        var version = await ytdl.GetVersion();
                        if (Versions.ContainsKey(version))
                        {
                            await Task.Run(() => File.Delete(file));
                        }
                        else
                        {
                            Versions.Add(version, ytdl);
                        }
                    }
                    catch (Exception ex)
                    {
                        // TODO... log
                        Console.Write(ex.ToString());
                    }
                }
            }
        }

        private async Task<string> DownloadLatest()
        {
            string targetFile = Path.Combine(StorePath, "youtube-dl-tmp");
            using var client = new HttpClient();
            using var downloadStream = await client.GetStreamAsync(LatestUrl);
            using var fileOut = File.OpenWrite(targetFile);
            await downloadStream.CopyToAsync(fileOut);
            return targetFile;
        }

        public async Task<bool> DownloadLatestVersion()
        {
            var tmpFile = await DownloadLatest();
            var ytdl = new YoutubeDL(tmpFile, PythonPath);
            var version = await ytdl.GetVersion();

            // We already have this version?
            if (Versions.ContainsKey(version))
            {
                // Discard downloaded version
                await Task.Run(() => File.Delete(tmpFile));
                return false;
            }
            
            // Rename to contain version number
            string newName = Path.Combine(
                Path.GetDirectoryName(tmpFile),
                Path.GetFileName(tmpFile).Replace("tmp", version.ToString()));

            await Task.Run(() => File.Move(tmpFile, newName));
            ytdl.YoutubeDlPath = newName;

            Versions.Add(version, ytdl);
            return true;
        }

        public async Task CleanupOldVersions(int keepCount = 1)
        {
            var toDelete = Versions.OrderByDescending(x => x.Key).Skip(keepCount).ToArray();

            foreach (var version in toDelete)
            {
                await Task.Run(() => File.Delete(version.Value.YoutubeDlPath));
                Versions.Remove(version.Key);
            }
        }
    }
}
