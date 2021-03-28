using Microsoft.Extensions.Logging;
using MoreLinq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace YoutubeDLWrapper
{
    public class YoutubeDL
    {
        private readonly ILogger logger;

        public string YoutubeDlPath { get; set; }

        public string PythonExePath { get; set; }

        public bool Debug { get; set; }

        public string DebugPath { get; set; }

        public YoutubeDL(ILogger logger, string path, string pythonPath, bool debug, string debugPath)
        {
            this.logger = logger;
            this.YoutubeDlPath = path;
            this.PythonExePath = pythonPath;
            this.Debug = debug;
            this.DebugPath = debugPath;
        }

        private Process BuildProcess(IEnumerable<string> args)
        {
            Process process = new Process();
            process.StartInfo.FileName = PythonExePath;
            process.StartInfo.ArgumentList.Add(YoutubeDlPath);
            args.ForEach(process.StartInfo.ArgumentList.Add);
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            return process;
        }

        private void RunProcess(Process process,
                                Action<string> onOutputCallback,
                                Action<string> onErrorCallback,
                                int timeoutMs,
                                CancellationToken? cancellationToken)
        {
            logger.LogDebug($"Invoking youtube-dl: {process.StartInfo.FileName} {string.Join(" ", process.StartInfo.ArgumentList)}");
            
            string fileOut = null;
            if (Debug)
            {
                Directory.CreateDirectory(DebugPath);
                fileOut = Path.Combine(DebugPath, $"{DateTime.Now:yyyyMMddhhmmsstt}_stdout.txt");
                logger.LogDebug($"Standard output will be written to {fileOut}");
            }

            process.OutputDataReceived += (sender, e) =>
            {
                if (fileOut != null)
                {
                    using var strOut = new StreamWriter(fileOut, true);
                    strOut.Write(e.Data);
                }
                onOutputCallback.Invoke(e.Data);
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                logger.LogDebug($"ERR >> {e.Data}");
                onErrorCallback.Invoke(e.Data);
            };

            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            int timeleft = timeoutMs;
            while (!process.HasExited && timeleft > 0)
            {
                if (cancellationToken.HasValue && cancellationToken.Value.IsCancellationRequested)
                {
                    logger.LogWarning("Invoke cancelled. Killing youtube-dl...");
                    process.Kill();
                    process.WaitForExit();
                    cancellationToken.Value.ThrowIfCancellationRequested();
                }

                process.WaitForExit(Math.Max(timeleft, 100));
                timeleft -= 100;
            }

            if (!process.HasExited)
            {
                logger.LogWarning("Invoke timed out. Killing youtube-dl...");
                process.Kill();
                process.WaitForExit();
            }

            cancellationToken?.ThrowIfCancellationRequested();
        }

        public int Run(IEnumerable<string> args,
                       Action<string> onOutputCallback = null,
                       Action<string> onErrorCallback = null,
                       int timeoutMs = 10000,
                       CancellationToken? cancellationToken = null)
        {
            using Process process = BuildProcess(args);
           
            RunProcess(process, 
                data => onOutputCallback?.Invoke(data), 
                data => onErrorCallback?.Invoke(data), 
                timeoutMs, 
                cancellationToken);
            
            return process.ExitCode;
        }

        public int Run(IEnumerable<string> args,
                       out string stdOutput,
                       out string stdError,
                       int timeoutMs = 10000,
                       CancellationToken? cancellationToken = null)
        {
            using Process process = BuildProcess(args);
            var stdOutBuilder = new StringBuilder();
            var stdErrorBuilder = new StringBuilder();

            RunProcess(process,
                data => stdOutBuilder.AppendLine(data),
                data => stdErrorBuilder.AppendLine(data),
                timeoutMs,
                cancellationToken);

            stdOutput = stdOutBuilder.ToString();
            stdError = stdErrorBuilder.ToString();

            return process.ExitCode;
        }

        public async Task<Version> GetVersion()
        {
            string stdOut = null, stdErr = null;
            int returnCode = await Task.Run(() => Run(new[] { "--version" }, out stdOut, out stdErr));
            if (returnCode != 0)
                throw new Exception("Failed to obtain version! " + stdErr);

            return Version.Parse(stdOut);
        }

        public async Task<UrlInformation> ExtractInformation(string url, bool fetchVideos)
        {
            var args = new List<string>()
            {
                "--ignore-errors",
                "--dump-single-json"
            };
            if (fetchVideos == false)
                args.Add("--flat-playlist");
            args.Add(url);

            string stdOut = null, stdErr = null;
            int returnCode = await Task.Run(() => Run(args, out stdOut, out stdErr, timeoutMs: 1000 * 60 * 10));
            if (returnCode != 0)
                throw new Exception("Information extraction failed! " + stdErr);

            var serializer = JsonSerializer.CreateDefault();
            serializer.MissingMemberHandling = MissingMemberHandling.Ignore;

            return await Task.Run(() =>
            {
                using var stream = new StringReader(stdOut);
                using var jsonStream = new JsonTextReader(stream);
                return serializer.Deserialize<UrlInformation>(jsonStream);
            });
        }
    }
}
