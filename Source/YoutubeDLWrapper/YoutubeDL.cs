using Microsoft.Extensions.Logging;
using MoreLinq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
                                int idleTimeoutMs,
                                CancellationToken? cancellationToken)
        {
            string fullCmdLine = $"{process.StartInfo.FileName} {string.Join(" ", process.StartInfo.ArgumentList)}";
            logger.LogDebug($"Invoking youtube-dl: {fullCmdLine}");
            
            string fileOut = null;
            if (Debug)
            {
                int r = new Random().Next(1000);
                Directory.CreateDirectory(DebugPath);
                fileOut = Path.Combine(DebugPath, $"{DateTime.Now:yyyyMMddhhmmsstt}_{r}_stdout.txt");
                logger.LogDebug($"Standard output will be written to {fileOut}");
                File.AppendAllText(fileOut, $"> {fullCmdLine}{Environment.NewLine}");
            }

            process.Start();

            // Per-invocation timestamp of the last line seen on either pipe. This YoutubeDL instance
            // is shared across concurrent invocations (reader lock), so it must be a local, not a field.
            var lastOutput = new StrongBox<long>(DateTime.UtcNow.Ticks);
            var thread = new Thread(() => OutputProcessingThread(process, fileOut, onOutputCallback, onErrorCallback, lastOutput));
            thread.Start();

            int timeleft = timeoutMs;
            while (!process.HasExited && timeleft > 0)
            {
                if (cancellationToken.HasValue && cancellationToken.Value.IsCancellationRequested)
                {
                    logger.LogWarning("Invoke cancelled. Killing youtube-dl...");
                    process.Kill();
                    process.WaitForExit();
                    thread.Join();
                    cancellationToken.Value.ThrowIfCancellationRequested();
                }

                // Idle-hang watchdog: a stalled download can keep the pipe open while emitting nothing
                // (YouTube throttling a high itag has frozen .part files for 15+ min). If nothing is
                // written for idleTimeoutMs, kill it and surface a TimeoutException — distinct from the
                // cancellation path, so the job's normal retry re-runs it instead of giving up.
                if (idleTimeoutMs > 0)
                {
                    long idleMs = (DateTime.UtcNow.Ticks - Interlocked.Read(ref lastOutput.Value)) / TimeSpan.TicksPerMillisecond;
                    if (idleMs > idleTimeoutMs)
                    {
                        logger.LogWarning($"youtube-dl produced no output for {idleMs} ms (idle timeout {idleTimeoutMs} ms). Killing as stalled...");
                        process.Kill();
                        process.WaitForExit();
                        thread.Join();
                        throw new TimeoutException($"youtube-dl stalled: no output for {idleMs} ms.");
                    }
                }

                process.WaitForExit(Math.Min(timeleft, 100));
                timeleft -= 100;
            }

            if (!process.HasExited)
            {
                logger.LogWarning("Invoke timed out. Killing youtube-dl...");
                process.Kill();
                process.WaitForExit();
            }

            thread.Join();
            cancellationToken?.ThrowIfCancellationRequested();
        }

        private void OutputProcessingThread(Process process,
                                            string outputFileOut,
                                            Action<string> onOutputCallback,
                                            Action<string> onErrorCallback,
                                            StrongBox<long> lastOutput)
        {
            var stdOut = process.StandardOutput;
            var stdErr = process.StandardError;

            var readOut = stdOut.ReadLineAsync();
            var readErr = stdErr.ReadLineAsync();
            bool endOut = false, endErr = false;

            do
            {
                // Read stdout
                if (readOut.IsCompleted && !endOut)
                {
                    Interlocked.Exchange(ref lastOutput.Value, DateTime.UtcNow.Ticks);
                    if (outputFileOut != null)
                    {
                        using var strOut = new StreamWriter(outputFileOut, true);
                        strOut.WriteLine(readOut.Result);
                    }
                    onOutputCallback.Invoke(readOut.Result);

                    if (!stdOut.EndOfStream)
                        readOut = stdOut.ReadLineAsync();
                    else endOut = true;
                }

                // Read stderr
                if (readErr.IsCompleted && !endErr)
                {
                    Interlocked.Exchange(ref lastOutput.Value, DateTime.UtcNow.Ticks);
                    onErrorCallback.Invoke(readErr.Result);

                    if (!stdErr.EndOfStream)
                        readErr = stdErr.ReadLineAsync();
                    else endErr = true;
                }

                Task.WaitAny(readOut, readErr);

            } while (!endOut || !endErr);
        }

        public int Run(IEnumerable<string> args,
                       Action<string> onOutputCallback = null,
                       Action<string> onErrorCallback = null,
                       int timeoutMs = 10000,
                       CancellationToken? cancellationToken = null,
                       int idleTimeoutMs = 0)
        {
            using Process process = BuildProcess(args);

            RunProcess(process,
                data => onOutputCallback?.Invoke(data),
                data => onErrorCallback?.Invoke(data),
                timeoutMs,
                idleTimeoutMs,
                cancellationToken);

            return process.ExitCode;
        }

        public int Run(IEnumerable<string> args,
                       out string stdOutput,
                       out string stdError,
                       int timeoutMs = 10000,
                       CancellationToken? cancellationToken = null,
                       int idleTimeoutMs = 0)
        {
            using Process process = BuildProcess(args);
            var stdOutBuilder = new StringWriter();
            var stdErrorBuilder = new StringWriter();

            RunProcess(process,
                data => stdOutBuilder.WriteLine(data),
                data => stdErrorBuilder.WriteLine(data),
                timeoutMs,
                idleTimeoutMs,
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

        /// <summary>
        /// Extracts metadata for a URL, retrying on failure. <paramref name="timeoutMs"/> should be
        /// short for interactive callers (the Add-subscription flow, where a user is waiting) and long
        /// for background work (sync). A failed attempt is retried up to <paramref name="retries"/> times.
        /// </summary>
        public async Task<UrlInformation> ExtractInformation(string url, bool fetchVideos,
            int timeoutMs = 1000 * 60 * 10, int idleTimeoutMs = 0, int retries = 0,
            IEnumerable<string> extraArgs = null)
        {
            Exception lastError = null;
            for (int attempt = 0; attempt <= retries; attempt++)
            {
                try
                {
                    return await ExtractInformationOnce(url, fetchVideos, timeoutMs, idleTimeoutMs, extraArgs);
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    logger.LogWarning($"Information extraction for '{url}' failed (attempt {attempt + 1}/{retries + 1}): {ex.Message}");
                }
            }
            throw lastError;
        }

        private async Task<UrlInformation> ExtractInformationOnce(string url, bool fetchVideos, int timeoutMs, int idleTimeoutMs,
            IEnumerable<string> extraArgs = null)
        {
            var args = new List<string>()
            {
                "--ignore-errors",
                "--dump-single-json"
            };
            // Per-call anti-bot args (cookies, sleeps). A fresh local list, never shared instance state.
            if (extraArgs != null)
                args.AddRange(extraArgs);
            if (fetchVideos == false)
                args.Add("--flat-playlist");
            args.Add(url);

            string stdOut = null, stdErr = null;
            int returnCode = await Task.Run(() => Run(args, out stdOut, out stdErr, timeoutMs: timeoutMs, idleTimeoutMs: idleTimeoutMs));

            // With --ignore-errors, yt-dlp exits non-zero whenever SOME entries fail to extract
            // (members-only, private, geo-blocked, or -- lately -- videos needing a JS runtime),
            // yet it still emits a valid single-JSON document for every entry it could extract.
            // So a non-zero exit alone isn't fatal: only treat it as failure when there's no
            // usable JSON to parse. Otherwise proceed with the partial result and just log it.
            if (string.IsNullOrWhiteSpace(stdOut))
                throw new Exception("Information extraction failed! " + stdErr);

            if (returnCode != 0)
                logger.LogWarning($"yt-dlp reported errors extracting '{url}' (exit {returnCode}); using the entries it did return. Details: {stdErr}");

            var serializer = JsonSerializer.CreateDefault();
            serializer.MissingMemberHandling = MissingMemberHandling.Ignore;

            var info = await Task.Run(() =>
            {
                using var stream = new StringReader(stdOut);
                using var jsonStream = new JsonTextReader(stream);
                return serializer.Deserialize<UrlInformation>(jsonStream);
            });

            // yt-dlp prints the literal "null" when nothing at all could be extracted.
            if (info == null)
                throw new Exception("Information extraction failed! " + stdErr);

            return info;
        }
    }
}
