namespace SteamRouteFixer.Services.Common
{
    public static class FormatHelper
    {
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
}
