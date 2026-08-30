using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace SteamRouteFixer.Models
{
    public class ProcessItem : INotifyPropertyChanged
    {
        private int _pid;
        private string _name = string.Empty;
        private string _windowTitle = string.Empty;
        private string _executablePath = string.Empty;
        private ImageSource? _icon;
        private int _connectionCount = 0;
        private int _requestCount = 0;
        private long _totalBytes = 0;
        private string _formattedSize = "0 B";

        public int Pid
        {
            get => _pid;
            set { _pid = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        public string WindowTitle
        {
            get => _windowTitle;
            set { _windowTitle = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        public string DisplayName => string.IsNullOrWhiteSpace(_windowTitle) ? $"{_name} (PID: {_pid})" : $"{_name} - {_windowTitle} (PID: {_pid})";

        public string ExecutablePath
        {
            get => _executablePath;
            set { _executablePath = value; OnPropertyChanged(); }
        }

        public ImageSource? Icon
        {
            get => _icon;
            set { _icon = value; OnPropertyChanged(); }
        }

        public int ConnectionCount
        {
            get => _connectionCount;
            set { _connectionCount = value; OnPropertyChanged(); }
        }

        public int RequestCount
        {
            get => _requestCount;
            set { _requestCount = value; OnPropertyChanged(); }
        }

        public long TotalBytes
        {
            get => _totalBytes;
            set
            {
                _totalBytes = value;
                OnPropertyChanged();
            }
        }

        public string FormattedSize
        {
            get => _formattedSize;
            set { _formattedSize = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
