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
            ApplyLanguageTranslations();
            TxaLanguageManager.OnLanguageChanged += ApplyLanguageTranslations;
            Closed += (s, e) => TxaLanguageManager.OnLanguageChanged -= ApplyLanguageTranslations;
            Loaded += (s, e) => BtnCheckUpdate_Click(this, new RoutedEventArgs());
            TxtCurrentVersion.Text = $"Phiên bản hiện tại: v{UpdateChecker.CurrentVersion}";
        }

        private void ApplyLanguageTranslations()
        {
            Title = TxaLanguageManager.GetString("t_update_title", "Kiểm tra Cập nhật - Steam Route Fixer");
            if (TxtUpdateMainTitle != null) TxtUpdateMainTitle.Text = TxaLanguageManager.GetString("t_update_header", "Cập Nhật Phần Mềm");
            if (TxtUpdateMainSub != null) TxtUpdateMainSub.Text = TxaLanguageManager.GetString("t_update_sub", "Kiểm tra phiên bản mới từ GitHub TXAVL/SteamRouteFixer");
            if (TxtReleaseInfoTitle != null) TxtReleaseInfoTitle.Text = TxaLanguageManager.GetString("t_changelog_header", "📋 Thông tin bản phát hành:");
            if (BtnCheckUpdate != null) BtnCheckUpdate.Content = TxaLanguageManager.GetString("t_btn_check_again", "🔄 Kiểm Tra Ngay");
            if (BtnDownloadUpdate != null) BtnDownloadUpdate.Content = TxaLanguageManager.GetString("t_btn_download_update", "⬇ Tải Bản Cập Nhật Mới");
            if (BtnOpenGithub != null) BtnOpenGithub.Content = "🌐 GitHub Releases";
            if (BtnCloseUpdate != null) BtnCloseUpdate.Content = TxaLanguageManager.GetString("t_btn_close", "Đóng");
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

                    TxtChangelog.Text = $"=== BẢN CẬP NHẬT MỚI: v{_latestUpdate.Version} ===\r\nNgày phát hành: {_latestUpdate.ReleaseDate}\r\n\r\nNội dung thay đổi:\r\n{_latestUpdate.Changelog}";

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

                    string changelogContent = !string.IsNullOrWhiteSpace(_latestUpdate.Changelog)
                        ? _latestUpdate.Changelog
                        : "Bản phát hành chính thức ổn định từ TXA Studio.";

                    TxtChangelog.Text = $"=== NHẬT KÝ BẢN PHÁT HÀNH v{_latestUpdate.Version} (MỚI NHẤT) ===\r\nNgày phát hành: {_latestUpdate.ReleaseDate}\r\n\r\n{changelogContent}";
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

                if (TxaMessageBox.Show(this, "Đã tải xong bản cập nhật mới. Bạn có muốn khởi chạy ngay?", "Cập nhật hoàn tất", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
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
