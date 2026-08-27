using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace YoutubeDLWrapper
{
    public static class PythonFinder
    {
        [SupportedOSPlatform("windows")]
        private static string FindInRegistry(RegistryKey rootKey)
        {
            var pythonKey = rootKey.OpenSubKey(@"SOFTWARE\Python\PythonCore");
            if (pythonKey == null)
                return null;

            foreach (var versionKey in pythonKey.GetSubKeyNames().OrderByDescending(x => Version.Parse(x)))
            {
                var installPathKey = pythonKey.OpenSubKey(versionKey + @"\InstallPath");
                var exePath = installPathKey?.GetValue("WindowedExecutablePath");
                if (exePath != null)
                    return (string)exePath;

                exePath = installPathKey?.GetValue("ExecutablePath");
                if (exePath != null)
                    return (string)exePath;
            }

            return null;
        }

        private static string RunTestScript(string pythonExe)
        {
            using var process = new Process();
            process.StartInfo.FileName = pythonExe;
            process.StartInfo.Arguments = "-c \"import sys; print(sys.executable)\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;

            try
            {
                process.Start();
                process.WaitForExit(1000);

                if (process.ExitCode == 0)
                    return process.StandardOutput.ReadToEnd().Trim();
            }
            catch (Exception)
            {
            }

            return null;
        }

        public static string FindPython3(bool skipRegistry = false)
        {
            // Attempt to find in registry
            if (!skipRegistry && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var registryExe = FindInRegistry(Registry.CurrentUser) ?? FindInRegistry(Registry.LocalMachine);
                if (registryExe != null)
                    return registryExe;
            }

            // Find in PATH... easiest way is to simply try to run it.
            // pythonw is a Windows-only GUI launcher (no console window); attempting it on
            // Linux/macOS just throws a caught Win32Exception (noisy under a debugger), so
            // only try it on Windows.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return RunTestScript("pythonw")
                    ?? RunTestScript("python3")
                    ?? RunTestScript("python");

            return RunTestScript("python3")
                ?? RunTestScript("python");
        }
    }
}
