using MoreLinq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace YoutubeDLWrapper
{
    public class YoutubeDL
    {
        public string YoutubeDlPath { get; set; }

        public string PythonExePath { get; set; }

        public YoutubeDL(string path, string pythonPath)
        {
            this.YoutubeDlPath = path;
            this.PythonExePath = pythonPath;
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

        private void RunProcess(Process process, int timeoutMs, CancellationToken? cancellationToken)
        {
            process.Start();

            int timeleft = timeoutMs;
            while (!process.HasExited && timeleft > 0)
            {
                cancellationToken?.ThrowIfCancellationRequested();
                process.WaitForExit(Math.Max(timeleft, 100));
                timeleft -= 100;
            }

            if (!process.HasExited)
            {
                cancellationToken?.ThrowIfCancellationRequested();
                process.Kill();
                process.WaitForExit();
            }
        }

        public int Run(IEnumerable<string> args,
                       Action<string> onOutputCallback = null,
                       Action<string> onErrorCallback = null,
                       int timeoutMs = 10000,
                       CancellationToken? cancellationToken = null)
        {
            using Process process = BuildProcess(args);
            if (onOutputCallback != null)
                process.OutputDataReceived += (sender, e) => onOutputCallback.Invoke(e.Data);
            if (onErrorCallback != null)
                process.ErrorDataReceived += (sender, e) => onErrorCallback.Invoke(e.Data);
            
            RunProcess(process, timeoutMs, cancellationToken);

            return process.ExitCode;
        }

        public int Run(IEnumerable<string> args,
                       out string stdOutput,
                       out string stdError,
                       int timeoutMs = 10000,
                       CancellationToken? cancellationToken = null)
        {
            using Process process = BuildProcess(args);
            RunProcess(process, timeoutMs, cancellationToken);

            stdOutput = process.StandardOutput.ReadToEnd();
            stdError = process.StandardError.ReadToEnd();

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
    }
}
