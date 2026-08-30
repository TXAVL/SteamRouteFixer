using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SteamRouteFixer.Models
{
    public enum DomainStatus
    {
        Pending,
        Diagnosing,
        Open,           // Reachable, nothing in the way
        DnsPoisoned,    // DNS is poisoned by ISP, clean IP exists
        SniBlocked,     // DPI blocked SNI handshake
        IpBlocked,      // IP blocked / Dead
        Unreachable,    // No address answers
        Fixed           // Pinned and verified successfully
    }

    public class DomainItem : INotifyPropertyChanged
    {
        private string _domain = string.Empty;
        private string _displayName = string.Empty;
        private string _category = string.Empty;
        private DomainStatus _status = DomainStatus.Pending;
        private string _statusText = "Đang chờ";
        private string _statusBadgeColor = "#6E7681";
        private string _systemIp = "Chưa quét";
        private string _bestIp = "Chưa có";
        private int _latencyMs = -1;
        private bool _isPinned = false;
        private string _details = string.Empty;

        public string Domain
        {
            get => _domain;
            set { _domain = value; OnPropertyChanged(); }
        }

        public string DisplayName
        {
            get => _displayName;
            set { _displayName = value; OnPropertyChanged(); }
        }

        public string Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(); }
        }

        public DomainStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                UpdateStatusAppearance();
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

        public string SystemIp
        {
            get => _systemIp;
            set { _systemIp = value; OnPropertyChanged(); }
        }

        public string BestIp
        {
            get => _bestIp;
            set { _bestIp = value; OnPropertyChanged(); }
        }

        public int LatencyMs
        {
            get => _latencyMs;
            set
            {
                _latencyMs = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LatencyDisplay));
            }
        }

        public string LatencyDisplay => _latencyMs >= 0 ? $"{_latencyMs} ms" : "--";

        public bool IsPinned
        {
            get => _isPinned;
            set { _isPinned = value; OnPropertyChanged(); }
        }

        public string Details
        {
            get => _details;
            set { _details = value; OnPropertyChanged(); }
        }

        private void UpdateStatusAppearance()
        {
            switch (_status)
            {
                case DomainStatus.Pending:
                    StatusText = "Đang chờ";
                    StatusBadgeColor = "#6E7681";
                    break;
                case DomainStatus.Diagnosing:
                    StatusText = "Đang kiểm tra...";
                    StatusBadgeColor = "#0078D4";
                    break;
                case DomainStatus.Open:
                    StatusText = "Thông suốt (Open)";
                    StatusBadgeColor = "#107C41";
                    break;
                case DomainStatus.DnsPoisoned:
                    StatusText = "Bị chặn DNS (Poisoned)";
                    StatusBadgeColor = "#D83B01";
                    break;
                case DomainStatus.SniBlocked:
                    StatusText = "Bị chặn SNI (DPI)";
                    StatusBadgeColor = "#E81123";
                    break;
                case DomainStatus.IpBlocked:
                    StatusText = "Bị chặn IP";
                    StatusBadgeColor = "#E81123";
                    break;
                case DomainStatus.Unreachable:
                    StatusText = "Không phản hồi";
                    StatusBadgeColor = "#A80000";
                    break;
                case DomainStatus.Fixed:
                    StatusText = "Đã sửa thành công ✓";
                    StatusBadgeColor = "#00CC6A";
                    break;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
