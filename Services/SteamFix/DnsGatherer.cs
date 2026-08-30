using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;

namespace SteamRouteFixer.Services.SteamFix
{
    public class DnsGatherer
    {
        private readonly HttpClient _httpClient;

        public static readonly (string Ip, string Label)[] PublicResolvers = new[]
        {
            ("8.8.8.8", "Google"),
            ("1.1.1.1", "Cloudflare"),
            ("9.9.9.9", "Quad9"),
            ("208.67.222.222", "OpenDNS")
        };

        public static readonly (string Ip, string Label)[] VnResolvers = new[]
        {
            ("210.245.0.10", "FPT"),
            ("203.113.131.1", "VNPT"),
            ("183.91.160.11", "Viettel")
        };

        public static readonly string[] BogusPrefixes = new[] { "127.", "0.", "10.", "192.168." };

        public DnsGatherer()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "SteamRouteFixer/1.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(5);
        }

        public static bool IsBogusIp(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return true;
            return BogusPrefixes.Any(prefix => ip.StartsWith(prefix));
        }

        public async Task<List<string>> QuerySystemDnsAsync(string host)
        {
            var results = new List<string>();
            try
            {
                var addresses = await Dns.GetHostAddressesAsync(host);
                foreach (var addr in addresses)
                {
                    if (addr.AddressFamily == AddressFamily.InterNetwork)
                    {
                        string ip = addr.ToString();
                        if (!results.Contains(ip)) results.Add(ip);
                    }
                }
            }
            catch { }
            return results;
        }

        public async Task<List<string>> QueryDohAsync(string host)
        {
            var results = new List<string>();

            // 1. Google DoH
            try
            {
                string url = $"https://dns.google/resolve?name={Uri.EscapeDataString(host)}&type=A";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("Accept", "application/dns-json");
                using var resp = await _httpClient.SendAsync(req);
                if (resp.IsSuccessStatusCode)
                {
                    string json = await resp.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("Answer", out var answerArray))
                    {
                        foreach (var item in answerArray.EnumerateArray())
                        {
                            if (item.TryGetProperty("type", out var typeProp) && typeProp.GetInt32() == 1) // Type A
                            {
                                if (item.TryGetProperty("data", out var dataProp))
                                {
                                    string ip = dataProp.GetString() ?? "";
                                    if (!string.IsNullOrEmpty(ip) && !IsBogusIp(ip) && !results.Contains(ip))
                                    {
                                        results.Add(ip);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // 2. Cloudflare DoH fallback
            if (results.Count == 0)
            {
                try
                {
                    string url = $"https://cloudflare-dns.com/dns-query?name={Uri.EscapeDataString(host)}&type=A";
                    using var req = new HttpRequestMessage(HttpMethod.Get, url);
                    req.Headers.Add("Accept", "application/dns-json");
                    using var resp = await _httpClient.SendAsync(req);
                    if (resp.IsSuccessStatusCode)
                    {
                        string json = await resp.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("Answer", out var answerArray))
                        {
                            foreach (var item in answerArray.EnumerateArray())
                            {
                                if (item.TryGetProperty("type", out var typeProp) && typeProp.GetInt32() == 1)
                                {
                                    if (item.TryGetProperty("data", out var dataProp))
                                    {
                                        string ip = dataProp.GetString() ?? "";
                                        if (!string.IsNullOrEmpty(ip) && !IsBogusIp(ip) && !results.Contains(ip))
                                        {
                                            results.Add(ip);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            return results;
        }

        public async Task<List<string>> QueryUdpDnsAsync(string host, string resolverIp, int timeoutMs = 2500)
        {
            var results = new List<string>();
            try
            {
                using var client = new UdpClient();
                client.Client.ReceiveTimeout = timeoutMs;
                client.Client.SendTimeout = timeoutMs;

                byte[] queryPacket = BuildDnsQueryPacket(host);
                var endpoint = new IPEndPoint(IPAddress.Parse(resolverIp), 53);

                await client.SendAsync(queryPacket, queryPacket.Length, endpoint);

                var receiveTask = client.ReceiveAsync();
                var completed = await Task.WhenAny(receiveTask, Task.Delay(timeoutMs));

                if (completed == receiveTask)
                {
                    var response = receiveTask.Result;
                    results = ParseDnsResponse(response.Buffer);
                }
            }
            catch { }
            return results;
        }

        public async Task<DnsGatherResult> GatherAllIpsAsync(string host)
        {
            var result = new DnsGatherResult { Host = host };

            // 1. System IPs
            result.SystemIps = await QuerySystemDnsAsync(host);

            // 2. DoH IPs (Cleanest trusted source)
            var dohTask = QueryDohAsync(host);

            // 3. UDP Public Resolvers in parallel
            var publicTasks = PublicResolvers.Select(r => QueryUdpDnsAsync(host, r.Ip)).ToList();

            await Task.WhenAll(publicTasks.Concat(new[] { dohTask }));

            var trustedList = new List<string>();
            trustedList.AddRange(await dohTask);

            foreach (var task in publicTasks)
            {
                var ips = await task;
                foreach (var ip in ips)
                {
                    if (!IsBogusIp(ip) && !trustedList.Contains(ip))
                    {
                        trustedList.Add(ip);
                    }
                }
            }

            result.TrustedIps = trustedList;

            // Determine if DNS is poisoned
            bool systemHasBogus = result.SystemIps.Count > 0 && result.SystemIps.All(IsBogusIp);
            bool systemNotMatchingTrusted = result.SystemIps.Count > 0 && trustedList.Count > 0 && !result.SystemIps.Any(ip => trustedList.Contains(ip));

            result.IsPoisoned = systemHasBogus || (systemNotMatchingTrusted && result.SystemIps.Any(IsBogusIp));

            return result;
        }

        private static byte[] BuildDnsQueryPacket(string domain)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // Transaction ID
            bw.Write((byte)0x42);
            bw.Write((byte)0x42);
            // Flags: Standard query
            bw.Write((byte)0x01);
            bw.Write((byte)0x00);
            // Questions: 1
            bw.Write((byte)0x00);
            bw.Write((byte)0x01);
            // Answer RRs: 0
            bw.Write((byte)0x00);
            bw.Write((byte)0x00);
            // Authority RRs: 0
            bw.Write((byte)0x00);
            bw.Write((byte)0x00);
            // Additional RRs: 0
            bw.Write((byte)0x00);
            bw.Write((byte)0x00);

            // Query Name
            string[] parts = domain.Trim('.').Split('.');
            foreach (var part in parts)
            {
                byte[] label = System.Text.Encoding.ASCII.GetBytes(part);
                bw.Write((byte)label.Length);
                bw.Write(label);
            }
            bw.Write((byte)0x00); // End of name

            // Type: A (1)
            bw.Write((byte)0x00);
            bw.Write((byte)0x01);
            // Class: IN (1)
            bw.Write((byte)0x00);
            bw.Write((byte)0x01);

            return ms.ToArray();
        }

        private static List<string> ParseDnsResponse(byte[] buffer)
        {
            var ips = new List<string>();
            if (buffer.Length < 12) return ips;

            int ancount = (buffer[6] << 8) | buffer[7];
            int offset = 12;

            // Skip question section
            while (offset < buffer.Length)
            {
                byte len = buffer[offset++];
                if (len == 0) break;
                if ((len & 0xC0) == 0xC0) { offset++; break; }
                offset += len;
            }
            offset += 4; // Skip QType and QClass

            // Parse answers
            for (int i = 0; i < ancount && offset < buffer.Length; i++)
            {
                // Skip Name
                if (offset >= buffer.Length) break;
                if ((buffer[offset] & 0xC0) == 0xC0)
                {
                    offset += 2;
                }
                else
                {
                    while (offset < buffer.Length && buffer[offset] != 0)
                    {
                        offset += buffer[offset] + 1;
                    }
                    offset++;
                }

                if (offset + 10 > buffer.Length) break;

                ushort type = (ushort)((buffer[offset] << 8) | buffer[offset + 1]);
                ushort rdLength = (ushort)((buffer[offset + 8] << 8) | buffer[offset + 9]);
                offset += 10;

                if (type == 1 && rdLength == 4 && offset + 4 <= buffer.Length) // Type A IPv4
                {
                    string ip = $"{buffer[offset]}.{buffer[offset + 1]}.{buffer[offset + 2]}.{buffer[offset + 3]}";
                    if (!IsBogusIp(ip) && !ips.Contains(ip))
                    {
                        ips.Add(ip);
                    }
                }
                offset += rdLength;
            }

            return ips;
        }
    }

    public class DnsGatherResult
    {
        public string Host { get; set; } = string.Empty;
        public List<string> SystemIps { get; set; } = new();
        public List<string> TrustedIps { get; set; } = new();
        public bool IsPoisoned { get; set; }
    }
}
