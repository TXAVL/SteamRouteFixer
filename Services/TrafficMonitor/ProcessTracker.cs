using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SteamRouteFixer.Models;

namespace SteamRouteFixer.Services.TrafficMonitor
{
    public class ProcessTracker
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public List<ProcessItem> ScanActiveProcesses()
        {
            var list = new List<ProcessItem>();
            var allProcs = Process.GetProcesses();

            // Add "All Applications" option first
            list.Add(new ProcessItem
            {
                Pid = 0,
                Name = "Tất cả ứng dụng (All Apps)",
                WindowTitle = "Giám sát toàn bộ hệ thống"
            });

            foreach (var p in allProcs)
            {
                try
                {
                    if (p.Id <= 4) continue; // Skip System and Idle

                    string name = p.ProcessName;
                    string title = p.MainWindowTitle;
                    string exePath = string.Empty;

                    try { exePath = p.MainModule?.FileName ?? string.Empty; } catch { }

                    bool isNetworkApp = name.Contains("steam", StringComparison.OrdinalIgnoreCase) ||
                                       name.Contains("chrome", StringComparison.OrdinalIgnoreCase) ||
                                       name.Contains("edge", StringComparison.OrdinalIgnoreCase) ||
                                       name.Contains("firefox", StringComparison.OrdinalIgnoreCase) ||
                                       name.Contains("discord", StringComparison.OrdinalIgnoreCase) ||
                                       name.Contains("epic", StringComparison.OrdinalIgnoreCase) ||
                                       name.Contains("spotify", StringComparison.OrdinalIgnoreCase) ||
                                       name.Contains("game", StringComparison.OrdinalIgnoreCase) ||
                                       !string.IsNullOrWhiteSpace(title);

                    if (isNetworkApp)
                    {
                        var item = new ProcessItem
                        {
                            Pid = p.Id,
                            Name = $"{name}.exe",
                            WindowTitle = title,
                            ExecutablePath = exePath
                        };

                        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                        {
                            try
                            {
                                IntPtr hIcon = ExtractIcon(IntPtr.Zero, exePath, 0);
                                if (hIcon != IntPtr.Zero && hIcon != (IntPtr)1)
                                {
                                    var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                                        hIcon,
                                        System.Windows.Int32Rect.Empty,
                                        BitmapSizeOptions.FromEmptyOptions()
                                    );
                                    bitmapSource.Freeze();
                                    item.Icon = bitmapSource;
                                    DestroyIcon(hIcon);
                                }
                            }
                            catch { }
                        }

                        list.Add(item);
                    }
                }
                catch { }
            }

            return list.OrderBy(p => p.Pid != 0 ? 1 : 0)
                       .ThenByDescending(p => p.Name.Contains("steam", StringComparison.OrdinalIgnoreCase))
                       .ThenByDescending(p => !string.IsNullOrEmpty(p.WindowTitle))
                       .ThenBy(p => p.Name)
                       .ToList();
        }
    }
}
