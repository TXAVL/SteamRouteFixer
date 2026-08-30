using System.Diagnostics;
using System.Windows;
using SteamRouteFixer.Models;
using SteamRouteFixer.Services.Common;

namespace SteamRouteFixer.Views
{
    public partial class UpdateModal : Window
    {
        private readonly UpdateChecker _checker;
        private readonly AppConfig _config;
        private AppUpdateInfo? _latestUpdate;

        public UpdateModal()
        {
            InitializeComponent();
            _checker = new UpdateChecker();
            _config = StoragePathManager.LoadConfig();
            TxtCurrentVersion.Text = $"Phiên bản hiện tại: v{UpdateChecker.CurrentVersion}";
        }

        private async void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            BtnCheckUpdate.IsEnabled = false;
            TxtUpdateStatus.Text = "Đang kết nối đến GitHub Releases API...";
            TxtUpdateStatus.Foreground = (System.Windows.Media.Brush)FindResource("PrimaryAccentBrush");

            try
            {
                _latestUpdate = await _checker.CheckForUpdateAsync(_config.UpdateCheckUrl);

                if (_latestUpdate.HasUpdate)
                {
                    TxtUpdateStatus.Text = $"🚀 Phát hiện phiên bản mới: v{_latestUpdate.Version} (Phát hành: {_latestUpdate.ReleaseDate})!";
                    TxtUpdateStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 200, 50));

                    TxtChangelog.Text = $"=== BẢN CẬP NHẬT V{_latestUpdate.Version} ===\r\nNgày phát hành: {_latestUpdate.ReleaseDate}\r\n\r\nNội dung thay đổi:\r\n{_latestUpdate.Changelog}";

                    if (!string.IsNullOrEmpty(_latestUpdate.DownloadUrl))
                    {
                        BtnDownloadUpdate.Visibility = Visibility.Visible;
                    }
                }
                else if (_latestUpdate.IsError)
                {
                    TxtUpdateStatus.Text = $"ℹ️ {_latestUpdate.ErrorMessage}";
                    TxtUpdateStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 180, 50));
                    TxtChangelog.Text = $"Thông báo từ máy chủ:\r\n{_latestUpdate.Changelog}\r\n\r\nRepo chính thức: https://github.com/TXAVL/SteamRouteFixer";
                }
                else
                {
                    TxtUpdateStatus.Text = $"✅ Bạn đang sử dụng phiên bản v{UpdateChecker.CurrentVersion} mới nhất từ TXA Studio!";
                    TxtUpdateStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(38, 224, 127));
                    TxtChangelog.Text = $"Phiên bản hiện tại: v{UpdateChecker.CurrentVersion}\r\nTrạng thái: Mới nhất trên GitHub Release.\r\nKhông có bản cập nhật nào cần tải về.";
                }
            }
            catch (Exception ex)
            {
                TxtUpdateStatus.Text = $"Lỗi kết nối: {ex.Message}";
                TxtChangelog.Text = $"Chi tiết lỗi:\r\n{ex}";
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
            TxtUpdateStatus.Text = "Đang tải bản cập nhật về máy...";

            var progress = new Progress<int>(percent =>
            {
                DownloadProgressBar.Value = percent;
                TxtUpdateStatus.Text = $"Đang tải bản cập nhật: {percent}%";
            });

            try
            {
                string downloadedFile = await _checker.DownloadUpdateAsync(_latestUpdate.DownloadUrl, progress);
                TxtUpdateStatus.Text = "✓ Đã tải xong! Đang mở bộ cài...";

                if (MessageBox.Show("Đã tải xong bản cập nhật mới. Bạn có muốn khởi chạy ngay?", "Cập nhật hoàn tất", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
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

        private void BtnOpenGithub_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/TXAVL/SteamRouteFixer/releases",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
