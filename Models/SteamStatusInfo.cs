using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SteamRouteFixer.Models
{
    public class SteamStatusInfo : INotifyPropertyChanged
    {
        private bool _isInstalled;
        private string _installPath = string.Empty;
        private string _executablePath = string.Empty;
        private bool _isRunning;
        private int _runningProcessCount;
        private string _statusSummary = "Đang kiểm tra...";
        private string _statusColor = "#6E7681";

        public bool IsInstalled
        {
            get => _isInstalled;
            set { _isInstalled = value; OnPropertyChanged(); }
        }

        public string InstallPath
        {
            get => _installPath;
            set { _installPath = value; OnPropertyChanged(); }
        }

        public string ExecutablePath
        {
            get => _executablePath;
            set { _executablePath = value; OnPropertyChanged(); }
        }

        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                _isRunning = value;
                UpdateSummary();
                OnPropertyChanged();
            }
        }

        public int RunningProcessCount
        {
            get => _runningProcessCount;
            set { _runningProcessCount = value; OnPropertyChanged(); }
        }

        public string StatusSummary
        {
            get => _statusSummary;
            set { _statusSummary = value; OnPropertyChanged(); }
        }

        public string StatusColor
        {
            get => _statusColor;
            set { _statusColor = value; OnPropertyChanged(); }
        }

        public bool CanOperateTab1 => _isInstalled && !_isRunning;

        private void UpdateSummary()
        {
            if (!_isInstalled)
            {
                StatusSummary = "Chưa phát hiện Steam trên máy (Có thể chọn thư mục thủ công)";
                StatusColor = "#D83B01";
            }
            else if (_isRunning)
            {
                StatusSummary = $"⚠️ Steam đang chạy ({_runningProcessCount} tiến trình). Vui lòng đóng Steam để áp dụng cấu hình sạch hiệu quả!";
                StatusColor = "#D83B01";
            }
            else
            {
                StatusSummary = $"✅ Steam đã cài đặt ({_installPath}) và ĐÃ ĐÓNG HOÀN TOÀN. Sẵn sàng sửa lỗi!";
                StatusColor = "#107C41";
            }
            OnPropertyChanged(nameof(CanOperateTab1));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
