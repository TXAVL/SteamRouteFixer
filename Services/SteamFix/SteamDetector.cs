using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using SteamRouteFixer.Models;

namespace SteamRouteFixer.Services.SteamFix
{
    public static class SteamDetector
    {
        private static readonly string[] StandardPaths = new[]
        {
            @"C:\Program Files (x86)\Steam\steam.exe",
            @"C:\Program Files\Steam\steam.exe",
            @"D:\Steam\steam.exe",
            @"D:\Program Files (x86)\Steam\steam.exe",
            @"E:\Steam\steam.exe",
            @"E:\Program Files (x86)\Steam\steam.exe"
        };

        public static string NormalizeWindowsPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            path = path.Replace('/', '\\').Trim();

            try
            {
                if (File.Exists(path))
                {
                    return new FileInfo(path).FullName;
                }
                if (Directory.Exists(path))
                {
                    return new DirectoryInfo(path).FullName;
                }
            }
            catch { }

            // Ensure drive letter is uppercase
            if (path.Length >= 2 && path[1] == ':')
            {
                path = char.ToUpperInvariant(path[0]) + path.Substring(1);
            }

            return path;
        }

        public static SteamStatusInfo DetectSteam(string? customPath = null)
        {
            var info = new SteamStatusInfo();

            // 1. Check custom path if provided
            if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
            {
                info.IsInstalled = true;
                info.ExecutablePath = NormalizeWindowsPath(customPath);
                info.InstallPath = NormalizeWindowsPath(Path.GetDirectoryName(customPath) ?? string.Empty);
            }

            // 2. Check running process MainModule
            if (!info.IsInstalled)
            {
                try
                {
                    var steamProcesses = Process.GetProcessesByName("steam");
                    if (steamProcesses.Length > 0)
                    {
                        var path = steamProcesses[0].MainModule?.FileName;
                        if (!string.IsNullOrEmpty(path) && File.Exists(path))
                        {
                            info.IsInstalled = true;
                            info.ExecutablePath = NormalizeWindowsPath(path);
                            info.InstallPath = NormalizeWindowsPath(Path.GetDirectoryName(path) ?? string.Empty);
                        }
                    }
                }
                catch { }
            }

            // 3. Check Windows Registry (HKCU)
            if (!info.IsInstalled)
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                    if (key != null)
                    {
                        var steamPath = key.GetValue("SteamPath") as string;
                        var steamExe = key.GetValue("SteamExe") as string;

                        if (!string.IsNullOrEmpty(steamPath))
                        {
                            steamPath = steamPath.Replace('/', '\\');
                            string exe = !string.IsNullOrEmpty(steamExe) ? steamExe.Replace('/', '\\') : Path.Combine(steamPath, "steam.exe");
                            if (File.Exists(exe))
                            {
                                info.IsInstalled = true;
                                info.ExecutablePath = NormalizeWindowsPath(exe);
                                info.InstallPath = NormalizeWindowsPath(steamPath);
                            }
                        }
                    }
                }
                catch { }
            }

            // 4. Check Windows Registry (HKLM)
            if (!info.IsInstalled)
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")
                                 ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");
                    if (key != null)
                    {
                        var installPath = key.GetValue("InstallPath") as string;
                        if (!string.IsNullOrEmpty(installPath))
                        {
                            string exe = Path.Combine(installPath, "steam.exe");
                            if (File.Exists(exe))
                            {
                                info.IsInstalled = true;
                                info.ExecutablePath = NormalizeWindowsPath(exe);
                                info.InstallPath = NormalizeWindowsPath(installPath);
                            }
                        }
                    }
                }
                catch { }
            }

            // 5. Check standard drive locations
            if (!info.IsInstalled)
            {
                foreach (var std in StandardPaths)
                {
                    if (File.Exists(std))
                    {
                        info.IsInstalled = true;
                        info.ExecutablePath = NormalizeWindowsPath(std);
                        info.InstallPath = NormalizeWindowsPath(Path.GetDirectoryName(std) ?? string.Empty);
                        break;
                    }
                }
            }

            // 6. Check running state
            UpdateRunningStatus(info);

            return info;
        }

        public static void UpdateRunningStatus(SteamStatusInfo info)
        {
            try
            {
                var steamProcs = Process.GetProcessesByName("steam");
                var helperProcs = Process.GetProcessesByName("steamwebhelper");
                int total = steamProcs.Length + helperProcs.Length;

                info.RunningProcessCount = total;
                info.IsRunning = total > 0;
            }
            catch
            {
                info.IsRunning = false;
                info.RunningProcessCount = 0;
            }
        }

        public static bool CloseSteamProcesses()
        {
            try
            {
                var names = new[] { "steam", "steamwebhelper", "steamservice" };
                foreach (var name in names)
                {
                    var procs = Process.GetProcessesByName(name);
                    foreach (var p in procs)
                    {
                        try
                        {
                            p.Kill();
                            p.WaitForExit(1000);
                        }
                        catch { }
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool LaunchSteam(string? exePath = null)
        {
            try
            {
                string path = exePath ?? DetectSteam().ExecutablePath;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    });
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
