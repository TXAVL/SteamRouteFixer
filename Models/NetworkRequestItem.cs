using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SteamRouteFixer.Models
{
    public enum HttpStatusCategory
    {
        Success2xx,
        Redirect3xx,
        ClientError4xx,
        ServerError5xx,
        ActivePending,
        Unknown
    }

    public class NetworkRequestItem : INotifyPropertyChanged
    {
        private long _id;
        private DateTime _timestamp = DateTime.Now;
        private string _processName = "System";
        private int _pid;
        private string _protocol = "HTTPS";
        private string _method = "GET";
        private string _url = string.Empty;
        private string _host = string.Empty;
        private string _path = "/";
        private string _remoteIp = string.Empty;
        private int _remotePort = 443;
        private int _statusCode = 200;
        private string _statusText = "200 OK";
        private string _statusBadgeColor = "#107C41";
        private HttpStatusCategory _statusCategory = HttpStatusCategory.Success2xx;
        private long _requestBytes = 0;
        private long _responseBytes = 0;
        private string _formattedSize = "0 B";
        private int _durationMs = 0;
        private string _requestHeaders = string.Empty;
        private string _requestBody = string.Empty;
        private string _responseHeaders = string.Empty;
        private string _responseBody = string.Empty;
        private string _contentType = "application/json";

        public long Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public DateTime Timestamp
        {
            get => _timestamp;
            set { _timestamp = value; OnPropertyChanged(); OnPropertyChanged(nameof(TimeString)); }
        }

        public string TimeString
        {
            get => _timestamp.ToString("HH:mm:ss.fff");
            set { }
        }

        public string ProcessName
        {
            get => _processName;
            set { _processName = value; OnPropertyChanged(); }
        }

        public int Pid
        {
            get => _pid;
            set { _pid = value; OnPropertyChanged(); }
        }

        public string Protocol
        {
            get => _protocol;
            set { _protocol = value; OnPropertyChanged(); }
        }

        public string Method
        {
            get => _method;
            set { _method = value; OnPropertyChanged(); }
        }

        public string Url
        {
            get => _url;
            set { _url = value; OnPropertyChanged(); }
        }

        public string Host
        {
            get => _host;
            set { _host = value; OnPropertyChanged(); }
        }

        public string Path
        {
            get => _path;
            set { _path = value; OnPropertyChanged(); OnPropertyChanged(nameof(CompactDisplayUrl)); }
        }

        public string CompactDisplayUrl
        {
            get
            {
                if (string.IsNullOrEmpty(_url)) return $"{_host}{_path}";
                if (_url.Length > 45)
                {
                    return _url.Substring(0, 42) + "...";
                }
                return _url;
            }
            set { }
        }

        public string RemoteIp
        {
            get => _remoteIp;
            set { _remoteIp = value; OnPropertyChanged(); OnPropertyChanged(nameof(RemoteEndpoint)); }
        }

        public int RemotePort
        {
            get => _remotePort;
            set { _remotePort = value; OnPropertyChanged(); OnPropertyChanged(nameof(RemoteEndpoint)); }
        }

        public string RemoteEndpoint
        {
            get => $"{_remoteIp}:{_remotePort}";
            set { }
        }

        public int StatusCode
        {
            get => _statusCode;
            set
            {
                _statusCode = value;
                UpdateCategoryAndBadge();
                OnPropertyChanged();
            }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public string StatusBadgeColor
        {
            get => _statusBadgeColor;
            set { _statusBadgeColor = value; OnPropertyChanged(); }
        }

        public HttpStatusCategory StatusCategory
        {
            get => _statusCategory;
            set { _statusCategory = value; OnPropertyChanged(); }
        }

        public long RequestBytes
        {
            get => _requestBytes;
            set { _requestBytes = value; OnPropertyChanged(); }
        }

        public long ResponseBytes
        {
            get => _responseBytes;
            set { _responseBytes = value; OnPropertyChanged(); }
        }

        public string FormattedSize
        {
            get => _formattedSize;
            set { _formattedSize = value; OnPropertyChanged(); }
        }

        public int DurationMs
        {
            get => _durationMs;
            set { _durationMs = value; OnPropertyChanged(); OnPropertyChanged(nameof(DurationDisplay)); }
        }

        public string DurationDisplay
        {
            get => $"{_durationMs} ms";
            set { }
        }

        public string RequestHeaders
        {
            get => _requestHeaders;
            set { _requestHeaders = value; OnPropertyChanged(); }
        }

        public string RequestBody
        {
            get => _requestBody;
            set { _requestBody = value; OnPropertyChanged(); }
        }

        public string ResponseHeaders
        {
            get => _responseHeaders;
            set { _responseHeaders = value; OnPropertyChanged(); }
        }

        public string ResponseBody
        {
            get => _responseBody;
            set { _responseBody = value; OnPropertyChanged(); }
        }

        public string ContentType
        {
            get => _contentType;
            set { _contentType = value; OnPropertyChanged(); }
        }

        private void UpdateCategoryAndBadge()
        {
            if (_statusCode >= 200 && _statusCode < 300)
            {
                _statusCategory = HttpStatusCategory.Success2xx;
                _statusBadgeColor = "#107C41";
                _statusText = $"{_statusCode} OK";
            }
            else if (_statusCode >= 300 && _statusCode < 400)
            {
                _statusCategory = HttpStatusCategory.Redirect3xx;
                _statusBadgeColor = "#0078D4";
                _statusText = $"{_statusCode} Redirect";
            }
            else if (_statusCode >= 400 && _statusCode < 500)
            {
                _statusCategory = HttpStatusCategory.ClientError4xx;
                _statusBadgeColor = "#D83B01";
                _statusText = $"{_statusCode} Client Err";
            }
            else if (_statusCode >= 500)
            {
                _statusCategory = HttpStatusCategory.ServerError5xx;
                _statusBadgeColor = "#E81123";
                _statusText = $"{_statusCode} Server Err";
            }
            else
            {
                _statusCategory = HttpStatusCategory.ActivePending;
                _statusBadgeColor = "#8A8886";
                _statusText = "Active...";
            }
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusBadgeColor));
            OnPropertyChanged(nameof(StatusCategory));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
