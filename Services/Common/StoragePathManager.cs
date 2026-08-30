using System.IO;
using System.Text.Json;
using SteamRouteFixer.Models;

namespace SteamRouteFixer.Services.Common
{
    public static class StoragePathManager
    {
        private static readonly string RootAppData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SteamRouteFixer"
        );

        public static string AppDataRoot => RootAppData;
        public static string LogsDirectory => Path.Combine(RootAppData, "logs");
        public static string BackupsDirectory => Path.Combine(RootAppData, "backups");
        public static string SetupDirectory => Path.Combine(RootAppData, "setup");
        public static string DownloadsDirectory => Path.Combine(RootAppData, "downloads");
        public static string ConfigFilePath => Path.Combine(RootAppData, "txaconfig.json");
        public static string LegacyConfigFilePath => Path.Combine(RootAppData, "config.json");

        public static void EnsureDirectories()
        {
            try
            {
                if (!Directory.Exists(RootAppData)) Directory.CreateDirectory(RootAppData);
                if (!Directory.Exists(LogsDirectory)) Directory.CreateDirectory(LogsDirectory);
                if (!Directory.Exists(BackupsDirectory)) Directory.CreateDirectory(BackupsDirectory);
                if (!Directory.Exists(SetupDirectory)) Directory.CreateDirectory(SetupDirectory);
                if (!Directory.Exists(DownloadsDirectory)) Directory.CreateDirectory(DownloadsDirectory);
            }
            catch { }
        }

        public static AppConfig LoadConfig()
        {
            EnsureDirectories();
            try
            {
                string targetPath = File.Exists(ConfigFilePath) ? ConfigFilePath : LegacyConfigFilePath;
                if (File.Exists(targetPath))
                {
                    string json = File.ReadAllText(targetPath);
                    var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                    if (cfg != null) return cfg;
                }
            }
            catch { }

            var defaultConfig = new AppConfig();
            SaveConfig(defaultConfig);
            return defaultConfig;
        }

        public static void SaveConfig(AppConfig config)
        {
            EnsureDirectories();
            try
            {
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFilePath, json);
            }
            catch { }
        }

        public static void AppendLogFile(string logText)
        {
            try
            {
                EnsureDirectories();
                string logFile = Path.Combine(LogsDirectory, $"steamroute_{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {logText}{Environment.NewLine}");
            }
            catch { }
        }
    }
}
