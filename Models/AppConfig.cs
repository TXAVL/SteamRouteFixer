namespace SteamRouteFixer.Models
{
    public class AppConfig
    {
        public string Theme { get; set; } = "WinUI3"; // "WinUI3", "SteamDark", "VSCode"
        public string CustomSteamPath { get; set; } = string.Empty;
        public string UpdateCheckUrl { get; set; } = "https://api.github.com/repos/txa-fix-steam/SteamRouteFixer/releases/latest";
        public string PrimaryNet10DownloadUrl { get; set; } = "https://www.mediafire.com/file/jj8pfqovpm30fw2/dotnet+10.exe/file";
        public string FallbackNet10DownloadUrl { get; set; } = "https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe";
        public bool AutoFlushDnsOnFix { get; set; } = true;
        public bool AutoBackupHosts { get; set; } = true;
        public int SnifferPort { get; set; } = 8888;
        public bool EnableHttpProxySniffer { get; set; } = false;
        public double UiScale { get; set; } = 1.0;
    }

    public class AppUpdateInfo
    {
        public string Version { get; set; } = "1.0.0";
        public string ReleaseDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");
        public string Changelog { get; set; } = "Phiên bản khởi tạo v1.0.0 với bộ sửa lỗi Steam và giám sát traffic.";
        public string DownloadUrl { get; set; } = string.Empty;
        public string FallbackUrl { get; set; } = string.Empty;
        public bool HasUpdate { get; set; } = false;
    }

    public enum LogLevel
    {
        Info,
        Success,
        Warning,
        Error,
        Diag
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Message { get; set; } = string.Empty;
        public LogLevel Level { get; set; } = LogLevel.Info;

        public string TimeString
        {
            get => Timestamp.ToString("HH:mm:ss.fff");
            set { }
        }

        public string LevelPrefix
        {
            get => Level switch
            {
                LogLevel.Info => "[INFO]",
                LogLevel.Success => "[SUCCESS]",
                LogLevel.Warning => "[WARN]",
                LogLevel.Error => "[ERROR]",
                LogLevel.Diag => "[DIAG]",
                _ => "[LOG]"
            };
            set { }
        }

        public string LevelColor
        {
            get => Level switch
            {
                LogLevel.Info => "#0098FF",
                LogLevel.Success => "#00E676",
                LogLevel.Warning => "#FFB300",
                LogLevel.Error => "#FF3D00",
                LogLevel.Diag => "#E040FB",
                _ => "#CCCCCC"
            };
            set { }
        }
    }
}
