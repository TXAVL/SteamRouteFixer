using System.IO;
using System.Text;
using SteamRouteFixer.Models;

namespace SteamRouteFixer.Services.Common
{
    public static class TxaLogger
    {
        private static readonly object _lockObj = new();
        private static string _currentLogFile = string.Empty;

        public static event Action<LogEntry>? OnLogEmitted;

        public static void Initialize()
        {
            try
            {
                StoragePathManager.EnsureDirectories();
                string timestamp = DateTime.Now.ToString("yyyyMMdd");
                _currentLogFile = Path.Combine(StoragePathManager.LogsDirectory, $"txa_stream_{timestamp}.log");

                Info($"[TxaLogger] Khởi tạo hệ thống ghi log thành công. File: {_currentLogFile}");
            }
            catch { }
        }

        public static void Info(string message) => Log(LogLevel.Info, message);
        public static void Success(string message) => Log(LogLevel.Success, message);
        public static void Warn(string message) => Log(LogLevel.Warning, message);
        public static void Error(string message, Exception? ex = null)
        {
            string fullMsg = ex != null ? $"{message} | Chi tiết Exception: {ex.GetType().Name}: {ex.Message}\r\nStack: {ex.StackTrace}" : message;
            Log(LogLevel.Error, fullMsg);
        }
        public static void Diag(string message) => Log(LogLevel.Diag, message);

        public static void Log(LogLevel level, string message)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message
            };

            // 1. Fire event for UI
            try
            {
                OnLogEmitted?.Invoke(entry);
            }
            catch { }

            // 2. Write to File
            try
            {
                lock (_lockObj)
                {
                    if (string.IsNullOrEmpty(_currentLogFile))
                    {
                        StoragePathManager.EnsureDirectories();
                        _currentLogFile = Path.Combine(StoragePathManager.LogsDirectory, $"txa_stream_{DateTime.Now:yyyyMMdd}.log");
                    }

                    string logLine = $"[{entry.TimeString}] [{entry.LevelPrefix}] {entry.Message}{Environment.NewLine}";
                    File.AppendAllText(_currentLogFile, logLine, Encoding.UTF8);
                }
            }
            catch { }
        }
    }
}
