namespace SteamRouteFixer.Services.Common
{
    public static class TxaFormat
    {
        /// <summary>
        /// Định dạng thời gian chính xác tới mili-giây chuẩn TXA: HH:mm:ss.fff
        /// </summary>
        public static string FormatTime(DateTime dt)
        {
            return dt.ToString("HH:mm:ss.fff");
        }

        /// <summary>
        /// Định dạng ngày tháng năm chuẩn TXA: yyyy-MM-dd
        /// </summary>
        public static string FormatDate(DateTime dt)
        {
            return dt.ToString("yyyy-MM-dd");
        }

        /// <summary>
        /// Định dạng đầy đủ Ngày + Giờ chính xác tới mili-giây: yyyy-MM-dd HH:mm:ss.fff
        /// </summary>
        public static string FormatDateTime(DateTime dt)
        {
            return dt.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }

        /// <summary>
        /// Định dạng dung lượng dữ liệu tự động (B, KB, MB, GB, TB)
        /// </summary>
        public static string FormatBytes(long bytes)
        {
            if (bytes < 0) return "0 B";
            if (bytes < 1024) return $"{bytes} B";

            double kb = bytes / 1024.0;
            if (kb < 1024.0) return $"{kb:0.0} KB";

            double mb = kb / 1024.0;
            if (mb < 1024.0) return $"{mb:0.00} MB";

            double gb = mb / 1024.0;
            if (gb < 1024.0) return $"{gb:0.00} GB";

            double tb = gb / 1024.0;
            return $"{tb:0.00} TB";
        }

        /// <summary>
        /// Định dạng tốc độ mạng truyền tải thời gian thực (B/s, KB/s, MB/s, GB/s)
        /// </summary>
        public static string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond < 0) return "0 B/s";
            if (bytesPerSecond < 1024) return $"{bytesPerSecond:0} B/s";

            double kb = bytesPerSecond / 1024.0;
            if (kb < 1024.0) return $"{kb:0.0} KB/s";

            double mb = kb / 1024.0;
            if (mb < 1024.0) return $"{mb:0.00} MB/s";

            double gb = mb / 1024.0;
            return $"{gb:0.00} GB/s";
        }

        /// <summary>
        /// Định dạng độ trễ kết nối (Latency/Ping): ms
        /// </summary>
        public static string FormatLatency(long durationMs)
        {
            if (durationMs < 0) return "0 ms";
            return $"{durationMs} ms";
        }

        /// <summary>
        /// Định dạng thời gian ước tính hoàn thành (ETA)
        /// </summary>
        public static string FormatEta(double secondsRemaining)
        {
            if (secondsRemaining <= 0) return "0s";
            if (secondsRemaining < 1.0) return $"~{(int)(secondsRemaining * 1000)}ms";
            if (secondsRemaining < 60.0) return $"~{secondsRemaining:0.0}s";

            var time = TimeSpan.FromSeconds(secondsRemaining);
            if (time.TotalHours < 1)
            {
                return $"~{time.Minutes}m {time.Seconds}s";
            }
            return $"~{(int)time.TotalHours}h {time.Minutes}m";
        }
    }

    // Alias wrapper for backwards compatibility
    public static class FormatHelper
    {
        public static string FormatBytes(long bytes) => TxaFormat.FormatBytes(bytes);
        public static string FormatSpeed(double bytesPerSecond) => TxaFormat.FormatSpeed(bytesPerSecond);
        public static string FormatEta(double secondsRemaining) => TxaFormat.FormatEta(secondsRemaining);
        public static string FormatTime(DateTime dt) => TxaFormat.FormatTime(dt);
    }
}
