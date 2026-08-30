using System.Diagnostics;
using System.Windows;
using SteamRouteFixer.Models;
using SteamRouteFixer.Services.Common;

namespace SteamRouteFixer.Views
{
    public partial class AboutUpdateModal : Window
    {
        private readonly UpdateChecker _checker;
        private readonly AppConfig _config;
        private AppUpdateInfo? _latestUpdate;

        public AboutUpdateModal()
        {
            InitializeComponent();
            _checker = new UpdateChecker();
            _config = StoragePathManager.LoadConfig();
        }

        private async void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            BtnCheckUpdate.IsEnabled = false;
            TxtUpdateStatus.Text = "Đang kết nối đến máy chủ kiểm tra phiên bản...";

            try
            {
                _latestUpdate = await _checker.CheckForUpdateAsync(_config.UpdateCheckUrl);

                if (_latestUpdate.HasUpdate)
                {
                    TxtUpdateStatus.Text = $"🚀 Phát hiện phiên bản mới: v{_latestUpdate.Version} (Phát hành: {_latestUpdate.ReleaseDate})!";
                    TxtUpdateStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 200, 50));

                    TxtChangelog.Text = $"Chi tiết bản cập nhật v{_latestUpdate.Version}:\r\n{_latestUpdate.Changelog}";

                    if (!string.IsNullOrEmpty(_latestUpdate.DownloadUrl))
                    {
                        BtnDownloadUpdate.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    TxtUpdateStatus.Text = $"✅ Bạn đang sử dụng phiên bản mới nhất v1.0 từ TXA Studio!";
                    TxtUpdateStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(38, 224, 127));
                }
            }
            catch (Exception ex)
            {
                TxtUpdateStatus.Text = $"Lỗi khi kiểm tra: {ex.Message}";
            }
            finally
            {
                BtnCheckUpdate.IsEnabled = true;
            }
        }

        private async void BtnDownloadUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_latestUpdate == null || string.IsNullOrEmpty(_latestUpdate.DownloadUrl)) return;

            BtnDownloadUpdate.IsEnabled = false;
            DownloadProgressBar.Visibility = Visibility.Visible;
            TxtUpdateStatus.Text = "Đang tải bản cập nhật về %LocalAppData%\\SteamRouteFixer\\setup...";

            var progress = new Progress<int>(percent =>
            {
                DownloadProgressBar.Value = percent;
                TxtUpdateStatus.Text = $"Đang tải bản cập nhật: {percent}%";
            });

            try
            {
                string downloadedFile = await _checker.DownloadUpdateAsync(_latestUpdate.DownloadUrl, progress);
                TxtUpdateStatus.Text = "✓ Đã tải xong! Đang chuẩn bị mở bộ cài...";

                if (MessageBox.Show("Đã tải xong bản cập nhật mới. Bạn có muốn mở ngay bây giờ?", "Cập nhật hoàn tất", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = downloadedFile,
                        UseShellExecute = true
                    });
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                TxtUpdateStatus.Text = $"Lỗi khi tải: {ex.Message}";
                BtnDownloadUpdate.IsEnabled = true;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
