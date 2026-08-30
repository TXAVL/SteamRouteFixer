using System.IO;
using System.Net;
using System.Text;
using SteamRouteFixer.Models;
using SteamRouteFixer.Services.Common;

namespace SteamRouteFixer.Services.TrafficMonitor
{
    public class HttpProxySniffer
    {
        private HttpListener? _listener;
        private bool _isRunning;
        private long _idCounter = 1000;

        public event Action<NetworkRequestItem>? OnRequestCaptured;

        public bool IsRunning => _isRunning;

        public void Start(int port = 8888)
        {
            if (_isRunning) return;

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{port}/");
                _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                _listener.Start();
                _isRunning = true;

                Task.Run(ListenLoop);
            }
            catch { }
        }

        public void Stop()
        {
            _isRunning = false;
            try
            {
                _listener?.Stop();
                _listener?.Close();
            }
            catch { }
            _listener = null;
        }

        private async Task ListenLoop()
        {
            while (_isRunning && _listener != null && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => ProcessContextAsync(context));
                }
                catch
                {
                    if (!_isRunning) break;
                }
            }
        }

        private async Task ProcessContextAsync(HttpListenerContext context)
        {
            var req = context.Request;
            var resp = context.Response;

            string method = req.HttpMethod;
            string url = req.Url?.ToString() ?? "http://localhost/";
            string host = req.Url?.Host ?? "localhost";
            string path = req.Url?.PathAndQuery ?? "/";

            // Read Request Body
            string reqBody = "";
            long reqBytes = 0;
            if (req.HasEntityBody)
            {
                using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
                reqBody = await reader.ReadToEndAsync();
                reqBytes = req.ContentLength64 > 0 ? req.ContentLength64 : Encoding.UTF8.GetByteCount(reqBody);
            }

            var sbReqHeaders = new StringBuilder();
            if (req.Headers.AllKeys != null)
            {
                foreach (string? key in req.Headers.AllKeys)
                {
                    if (key != null)
                    {
                        sbReqHeaders.AppendLine($"{key}: {req.Headers[key]}");
                    }
                }
            }

            // Mock Proxy Forward or Handle local request
            string respContent = "{\"status\":\"ok\",\"proxy\":\"SteamRouteFixer-Sniffer\",\"timestamp\":\"" + DateTime.UtcNow.ToString("o") + "\"}";
            byte[] respBuffer = Encoding.UTF8.GetBytes(respContent);

            resp.StatusCode = 200;
            resp.ContentType = "application/json; charset=utf-8";
            resp.ContentLength64 = respBuffer.Length;

            var sbRespHeaders = new StringBuilder();
            sbRespHeaders.AppendLine("HTTP/1.1 200 OK");
            sbRespHeaders.AppendLine("Content-Type: application/json; charset=utf-8");
            sbRespHeaders.AppendLine($"Content-Length: {respBuffer.Length}");
            sbRespHeaders.AppendLine("Server: SteamRouteFixer-Sniffer/1.0");

            await resp.OutputStream.WriteAsync(respBuffer, 0, respBuffer.Length);
            resp.OutputStream.Close();

            var item = new NetworkRequestItem
            {
                Id = Interlocked.Increment(ref _idCounter),
                Timestamp = DateTime.Now,
                Pid = Environment.ProcessId,
                ProcessName = "LocalProxy.exe",
                Protocol = "HTTP/1.1",
                Method = method,
                Host = host,
                Path = path,
                Url = url,
                RemoteIp = "127.0.0.1",
                RemotePort = req.Url?.Port ?? 80,
                StatusCode = 200,
                RequestBytes = reqBytes,
                ResponseBytes = respBuffer.Length,
                FormattedSize = FormatHelper.FormatBytes(respBuffer.Length),
                DurationMs = 5,
                ContentType = "application/json",
                RequestHeaders = sbReqHeaders.ToString(),
                RequestBody = reqBody,
                ResponseHeaders = sbRespHeaders.ToString(),
                ResponseBody = respContent
            };

            OnRequestCaptured?.Invoke(item);
        }
    }
}
