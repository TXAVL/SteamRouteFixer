using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SteamRouteFixer.Services.Common
{
    public static class ThemeManager
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38; // 1 = None, 2 = Mica, 3 = Acrylic, 4 = MicaAlt

        public static string CurrentTheme { get; private set; } = "WinUI3";

        public static void ApplyTheme(string themeName, Window? targetWindow = null)
        {
            CurrentTheme = themeName;
            string xamlFile = themeName switch
            {
                "SteamDark" => "Styles/ThemeSteamDark.xaml",
                "VSCode" => "Styles/ThemeVSCode.xaml",
                _ => "Styles/ThemeWinUI3.xaml"
            };

            try
            {
                var dictUri = new Uri($"pack://application:,,,/SteamRouteFixer;component/{xamlFile}", UriKind.Absolute);
                var newDict = new ResourceDictionary { Source = dictUri };

                var app = Application.Current;
                if (app != null)
                {
                    // Find and replace theme dictionary or add it
                    ResourceDictionary? existing = null;
                    foreach (var dict in app.Resources.MergedDictionaries)
                    {
                        if (dict.Source != null && dict.Source.OriginalString.Contains("Theme", StringComparison.OrdinalIgnoreCase))
                        {
                            existing = dict;
                            break;
                        }
                    }

                    if (existing != null)
                    {
                        app.Resources.MergedDictionaries.Remove(existing);
                    }
                    app.Resources.MergedDictionaries.Add(newDict);
                }

                if (targetWindow != null)
                {
                    ApplyDwmBackdrop(targetWindow, themeName == "WinUI3");
                }
            }
            catch (Exception ex)
            {
                TxaLogger.Error($"[ThemeManager] Lỗi nạp theme {themeName}", ex);
            }
        }

        public static void ApplyDwmBackdrop(Window window, bool useMica)
        {
            try
            {
                var handle = new WindowInteropHelper(window).EnsureHandle();
                if (handle == IntPtr.Zero) return;

                int darkMode = 1;
                DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

                if (Environment.OSVersion.Version.Build >= 22000) // Windows 11
                {
                    int backdropType = useMica ? 2 : 1; // 2 = Mica, 1 = None
                    DwmSetWindowAttribute(handle, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
                }
            }
            catch { }
        }
    }
}
