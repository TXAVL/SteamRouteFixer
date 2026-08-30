using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using SteamRouteFixer.Models;

namespace SteamRouteFixer.Services.SteamFix
{
    public class TlsProbeResult
    {
        public string Ip { get; set; } = string.Empty;
        public string Sni { get; set; } = string.Empty;
        public bool TcpConnected { get; set; }
        public bool TlsSuccess { get; set; }
        public int LatencyMs { get; set; } = -1;
        public string? ErrorMessage { get; set; }
        public bool IsReset { get; set; }
    }

    public class DiagnosisReport
    {
        public string Host { get; set; } = string.Empty;
        public DomainStatus Verdict { get; set; }
        public string? BestIp { get; set; }
        public int BestLatencyMs { get; set; } = -1;
        public List<TlsProbeResult> Probes { get; set; } = new();
        public List<string> SystemIps { get; set; } = new();
        public List<string> CandidateIps { get; set; } = new();
        public bool Poisoned { get; set; }
        public string SummaryMessage { get; set; } = string.Empty;
    }

    public class TlsProber
    {
        private const int DefaultPort = 443;
        private const string ControlSni = "example.com";

        public async Task<TlsProbeResult> ProbeIpAsync(string ip, string sni, int timeoutMs = 4000)
        {
            var result = new TlsProbeResult { Ip = ip, Sni = sni };
            var sw = Stopwatch.StartNew();

            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.NoDelay = true;

            try
            {
                var connectTask = socket.ConnectAsync(IPAddress.Parse(ip), DefaultPort);
                var completedTask = await Task.WhenAny(connectTask, Task.Delay(timeoutMs));

                if (completedTask != connectTask)
                {
                    result.ErrorMessage = "TCP Timeout";
                    return result;
                }

                result.TcpConnected = true;

                using var networkStream = new NetworkStream(socket, false);
                using var sslStream = new SslStream(
                    networkStream,
                    false,
                    (sender, certificate, chain, sslPolicyErrors) => true // Ignore certificate validation for reachability test
                );

                var sslOptions = new SslClientAuthenticationOptions
                {
                    TargetHost = sni,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck
                };

                var sslTask = sslStream.AuthenticateAsClientAsync(sslOptions, CancellationToken.None);
                var sslCompleted = await Task.WhenAny(sslTask, Task.Delay(timeoutMs));

                if (sslCompleted == sslTask)
                {
                    await sslTask; // Ensure no exception
                    sw.Stop();
                    result.TlsSuccess = true;
                    result.LatencyMs = (int)sw.ElapsedMilliseconds;
                }
                else
                {
                    result.ErrorMessage = "TLS Handshake Timeout";
                }
            }
            catch (SocketException ex)
            {
                result.ErrorMessage = ex.Message;
                result.IsReset = ex.SocketErrorCode == SocketError.ConnectionReset || ex.SocketErrorCode == SocketError.ConnectionRefused;
            }
            catch (AuthenticationException ex)
            {
                result.ErrorMessage = ex.Message;
                result.IsReset = ex.Message.Contains("reset", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        public async Task<DiagnosisReport> DiagnoseDomainAsync(string host, DnsGatherer gatherer, int timeoutMs = 4000)
        {
            var report = new DiagnosisReport { Host = host };

            // 1. Gather all IPs
            var dnsResult = await gatherer.GatherAllIpsAsync(host);
            report.SystemIps = dnsResult.SystemIps;
            report.Poisoned = dnsResult.IsPoisoned;

            var candidates = dnsResult.TrustedIps.Count > 0
                ? dnsResult.TrustedIps
                : dnsResult.SystemIps.Where(ip => !DnsGatherer.IsBogusIp(ip)).ToList();

            report.CandidateIps = candidates;

            if (candidates.Count == 0)
            {
                report.Verdict = DomainStatus.Unreachable;
                report.SummaryMessage = "Không tìm thấy địa chỉ IP hợp lệ nào.";
                return report;
            }

            // 2. Probe candidates in parallel
            var probeTasks = candidates.Select(ip => ProbeIpAsync(ip, host, timeoutMs)).ToList();
            var probeResults = (await Task.WhenAll(probeTasks)).ToList();

            // Sort: working TLS first, then by lowest latency
            probeResults.Sort((a, b) =>
            {
                if (a.TlsSuccess && !b.TlsSuccess) return -1;
                if (!a.TlsSuccess && b.TlsSuccess) return 1;
                return a.LatencyMs.CompareTo(b.LatencyMs);
            });

            report.Probes = probeResults;
            var working = probeResults.Where(p => p.TlsSuccess).ToList();

            if (working.Count > 0)
            {
                var best = working[0];
                report.BestIp = best.Ip;
                report.BestLatencyMs = best.LatencyMs;

                if (report.Poisoned)
                {
                    report.Verdict = DomainStatus.DnsPoisoned;
                    report.SummaryMessage = $"DNS bị nhà mạng chặn. Tuyến IP sạch {best.Ip} hoạt động tốt ({best.LatencyMs}ms).";
                }
                else
                {
                    report.Verdict = DomainStatus.Open;
                    report.SummaryMessage = $"Thông suốt - Không bị chặn ({best.LatencyMs}ms).";
                }
            }
            else
            {
                // Test control SNI on candidate[0]
                var controlProbe = await ProbeIpAsync(candidates[0], ControlSni, timeoutMs);
                if (controlProbe.TlsSuccess)
                {
                    report.Verdict = DomainStatus.SniBlocked;
                    report.SummaryMessage = "Nhà mạng chặn bắt gói tin theo tên miền (SNI DPI Block).";
                }
                else if (probeResults.Any(p => p.TcpConnected))
                {
                    report.Verdict = DomainStatus.IpBlocked;
                    report.SummaryMessage = "Địa chỉ IP máy chủ bị chặn hoặc không phản hồi.";
                }
                else
                {
                    report.Verdict = DomainStatus.Unreachable;
                    report.SummaryMessage = "Không có địa chỉ nào phản hồi.";
                }
            }

            return report;
        }
    }
}
