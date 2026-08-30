using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SteamRouteFixer.Services.SteamFix
{
    public static class DnsFlusher
    {
        [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
        private static extern int DnsFlushResolverCache();

        public static bool FlushDnsCache()
        {
            bool success = false;
            try
            {
                int result = DnsFlushResolverCache();
                if (result == 1)
                {
                    success = true;
                }
            }
            catch { }

            // Also invoke ipconfig /flushdns
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "ipconfig.exe",
                    Arguments = "/flushdns",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    proc.WaitForExit(3000);
                    if (proc.ExitCode == 0) success = true;
                }
            }
            catch { }

            return success;
        }
    }
}
