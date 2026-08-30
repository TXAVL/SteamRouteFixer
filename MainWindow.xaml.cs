using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SteamRouteFixer.Models;
using SteamRouteFixer.Services.Common;
using SteamRouteFixer.Services.SteamFix;
using SteamRouteFixer.Services.TrafficMonitor;
using SteamRouteFixer.Views;

namespace SteamRouteFixer
{
    public partial class MainWindow : Window
    {
        // Models & Collections
        private readonly ObservableCollection<DomainItem> _allDomains = new();
        private readonly ObservableCollection<DomainItem> _viewDomains = new();
        private readonly ObservableCollection<LogEntry> _logEntries = new();
        private readonly ObservableCollection<NetworkRequestItem> _allRequests = new();
        private readonly ObservableCollection<NetworkRequestItem> _viewRequests = new();

        // Services
        private readonly DnsGatherer _dnsGatherer = new();
        private readonly TlsProber _tlsProber = new();
        private readonly ProcessTracker _processTracker = new();
        private readonly TcpConnectionWatcher _tcpWatcher = new();
        private readonly HttpProxySniffer _httpSniffer = new();

        // Timers & State
        private readonly DispatcherTimer _steamSentinelTimer = new();
        private readonly DispatcherTimer _trafficTimer = new();
        private readonly DispatcherTimer _processRefreshTimer = new();
        private SteamStatusInfo _steamStatus = new();
        private AppConfig _config;

        private bool _isCapturingTraffic = true;
        private int _selectedFilterPid = 0;
        private string _tab1StatusFilter = "All";
        private string _tab2StatusFilter = "All";

        // Blocklist for API / Host endpoints
        private readonly HashSet<string> _blockedApiHosts = new(StringComparer.OrdinalIgnoreCase);

        // Statistics
        private long _totalDownloadedBytes = 0;
        private long _totalUploadedBytes = 0;
        private long _lastDownloadedBytes = 0;
        private DateTime _lastSpeedCheckTime = DateTime.Now;

        public MainWindow()
        {
            InitializeComponent();
            _config = StoragePathManager.LoadConfig();

            GridDomains.ItemsSource = _viewDomains;
            GridRequests.ItemsSource = _viewRequests;
            LogItemsControl.ItemsSource = _logEntries;

            // Connect TxaLogger to UI Log Stream
            TxaLogger.OnLogEmitted += (entry) =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    _logEntries.Add(entry);
                    if (_logEntries.Count > 500) _logEntries.RemoveAt(0);
                    LogScrollViewer.ScrollToEnd();
                });
            };

            TxaLanguageManager.OnLanguageChanged += ApplyLanguageTranslations;
            ApplyLanguageTranslations();

            InitTimers();
            CheckAdminRights();
            InitSteamPresets();

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            Closed += MainWindow_Closed;
        }

        private bool _isExplicitExitConfirmed = false;

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isExplicitExitConfirmed) return;

            string title = TxaLanguageManager.GetString("t_confirm_exit_title", "Xác nhận đóng ứng dụng");
            string msg = TxaLanguageManager.GetString("t_confirm_exit_msg", "Bạn có chắc chắn muốn thoát khỏi Steam Route Fixer?");

            var result = MessageBox.Show(this, msg, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            _isExplicitExitConfirmed = true;
        }

        private void ApplyLanguageTranslations()
        {
            Dispatcher.Invoke(() =>
            {
                // Window Title
                Title = TxaLanguageManager.GetString("t_app_title", "Steam Route Fixer & Traffic Inspector");

                // Top Menus
                if (MenuFile != null) MenuFile.Header = TxaLanguageManager.GetString("t_menu_file", "_File");
                if (MenuSteamDir != null) MenuSteamDir.Header = TxaLanguageManager.GetString("t_menu_steam_dir", "🎮 Mở thư mục cài đặt Steam");
                if (MenuHostsFile != null) MenuHostsFile.Header = TxaLanguageManager.GetString("t_menu_hosts_file", "📝 Mở file hosts Windows (Notepad)");
                if (MenuExportLogs != null) MenuExportLogs.Header = TxaLanguageManager.GetString("t_menu_export_logs", "💾 Xuất nhật ký Logs ra file...");
                if (MenuExit != null) MenuExit.Header = TxaLanguageManager.GetString("t_menu_exit", "❌ Thoát");

                if (MenuEdit != null) MenuEdit.Header = TxaLanguageManager.GetString("t_menu_edit", "_Edit");
                if (MenuCopyDomains != null) MenuCopyDomains.Header = TxaLanguageManager.GetString("t_menu_copy_domains", "📋 Copy danh sách Domain");
                if (MenuCopyLogs != null) MenuCopyLogs.Header = TxaLanguageManager.GetString("t_menu_copy_logs", "📋 Copy toàn bộ Console Logs");
                if (MenuClearLog != null) MenuClearLog.Header = TxaLanguageManager.GetString("t_menu_clear_log", "🧹 Xóa sạch Console Log");
                if (MenuRefreshAll != null) MenuRefreshAll.Header = TxaLanguageManager.GetString("t_menu_refresh", "🔄 Làm mới dữ liệu");

                if (MenuView != null) MenuView.Header = TxaLanguageManager.GetString("t_menu_view", "_View");
                if (MenuTools != null) MenuTools.Header = TxaLanguageManager.GetString("t_menu_tools", "_Tools");
                if (MenuAutoFix != null) MenuAutoFix.Header = TxaLanguageManager.GetString("t_menu_auto_fix", "⚡ 1-Click Auto Fix Steam");
                if (MenuDiagnose != null) MenuDiagnose.Header = TxaLanguageManager.GetString("t_menu_diagnose", "🔍 Chẩn đoán kết nối ngay");
                if (MenuRevertHosts != null) MenuRevertHosts.Header = TxaLanguageManager.GetString("t_menu_revert", "🧹 Khôi phục file Hosts gốc");
                if (MenuFlushDns != null) MenuFlushDns.Header = TxaLanguageManager.GetString("t_menu_flush_dns", "🔄 Flush DNS Cache Windows");
                if (MenuOpenAppData != null) MenuOpenAppData.Header = TxaLanguageManager.GetString("t_menu_appdata", "📂 Mở thư mục dữ liệu AppData (%LocalAppData%)");
                if (MenuSettings != null) MenuSettings.Header = TxaLanguageManager.GetString("t_menu_settings", "⚙️ Cài đặt hệ thống...");

                if (MenuHelp != null) MenuHelp.Header = TxaLanguageManager.GetString("t_menu_help", "_Help");
                if (MenuCheckUpdate != null) MenuCheckUpdate.Header = TxaLanguageManager.GetString("t_menu_check_update", "🔄 Kiểm tra cập nhật (Check Update...)");
                if (MenuAbout != null) MenuAbout.Header = TxaLanguageManager.GetString("t_menu_about", "ℹ️ Giới thiệu (About SteamRouteFixer...)");

                // Header Top
                if (BtnSettingsTop != null) BtnSettingsTop.Content = TxaLanguageManager.GetString("t_btn_settings", "⚙️ Cài Đặt");

                // Tabs
                if (TabSteamItem != null) TabSteamItem.Header = TxaLanguageManager.GetString("t_tab_steam", "🎮 STEAM ROUTE FIXER");
                if (TabTrafficItem != null) TabTrafficItem.Header = TxaLanguageManager.GetString("t_tab_traffic", "🌐 HTTP/HTTPS TRAFFIC & PROCESS INSPECTOR");

                // Tab 1 Action buttons
                if (BtnAutoFix != null) BtnAutoFix.Content = TxaLanguageManager.GetString("t_btn_autofix", "⚡ 1-Click Auto Fix Steam");
                if (BtnDiagnose != null) BtnDiagnose.Content = TxaLanguageManager.GetString("t_btn_diagnose", "🔍 Kiểm Tra (Diagnose)");
                if (BtnRevertHosts != null) BtnRevertHosts.Content = TxaLanguageManager.GetString("t_btn_revert_hosts", "🧹 Khôi Phục Hosts");
                if (BtnFlushDns != null) BtnFlushDns.Content = TxaLanguageManager.GetString("t_btn_flush_dns", "🔄 Flush DNS");
                if (BtnLaunchSteam != null) BtnLaunchSteam.Content = TxaLanguageManager.GetString("t_btn_open_steam", "🚀 Mở Steam");
                if (BtnCloseSteam != null) BtnCloseSteam.Content = TxaLanguageManager.GetString("t_btn_close_steam", "🛑 Đóng Steam Ngay");
                if (BtnNoticeCloseSteam != null) BtnNoticeCloseSteam.Content = TxaLanguageManager.GetString("t_btn_close_steam", "🛑 Đóng Steam Ngay");

                // Tab 1 Sentinel & Warning
                if (TxtSentinelHeader != null) TxtSentinelHeader.Text = TxaLanguageManager.GetString("t_sentinel_title", "BẢO VỆ STEAM SENTINEL: ");
                if (TxtNoticeSteamRunningHeader != null) TxtNoticeSteamRunningHeader.Text = TxaLanguageManager.GetString("t_steam_running_title", "PHÁT HIỆN TIẾN TRÌNH STEAM ĐANG CHẠY - CÁC TÁC VỤ SỬA ROUTE ĐÃ ĐƯỢC TẠM KHÓA");
                if (TxtNoticeSteamRunningDesc != null) TxtNoticeSteamRunningDesc.Text = TxaLanguageManager.GetString("t_steam_running_desc", "Vui lòng nhấn 'Đóng Steam Ngay' để giải phóng Socket và ghi file hosts an toàn trước khi thao tác.");

                // Tab 1 Filters
                if (TxtFilterStatusLabel1 != null) TxtFilterStatusLabel1.Text = TxaLanguageManager.GetString("t_filter_status", "Lọc trạng thái: ");
                if (TxtFilterAll1 != null) TxtFilterAll1.Text = TxaLanguageManager.GetString("t_filter_all", "Tất cả");
                if (TxtFilterOpen1 != null) TxtFilterOpen1.Text = TxaLanguageManager.GetString("t_filter_open", "Open");
                if (TxtFilterPoisoned1 != null) TxtFilterPoisoned1.Text = TxaLanguageManager.GetString("t_filter_poisoned", "Poisoned");
                if (TxtFilterBlocked1 != null) TxtFilterBlocked1.Text = TxaLanguageManager.GetString("t_filter_blocked", "Blocked");

                // Tab 1 Progress & Grid Columns
                if (TxtProgressStatus != null && (MainProgressBar == null || MainProgressBar.Value == 0)) TxtProgressStatus.Text = TxaLanguageManager.GetString("t_progress_ready", "Sẵn sàng kiểm tra và sửa lỗi kết nối Steam.");
                if (TxtEtaCountdown != null && (MainProgressBar == null || MainProgressBar.Value == 0)) TxtEtaCountdown.Text = TxaLanguageManager.GetString("t_eta_ready", "ETA: Sẵn sàng");
                if (ColPurpose != null) ColPurpose.Header = TxaLanguageManager.GetString("t_col_purpose", "Mục Đích");
                if (ColDomain != null) ColDomain.Header = TxaLanguageManager.GetString("t_col_domain", "Tên Miền (Domain)");
                if (ColSystemIp != null) ColSystemIp.Header = TxaLanguageManager.GetString("t_col_dns_ip", "IP Máy / ISP");
                if (ColBestIp != null) ColBestIp.Header = TxaLanguageManager.GetString("t_col_clean_ip", "IP Sạch Nhanh Nhất");
                if (ColPing != null) ColPing.Header = TxaLanguageManager.GetString("t_col_latency", "Ping");
                if (ColStatus != null) ColStatus.Header = TxaLanguageManager.GetString("t_col_status", "Trạng Thái");

                // Tab 1 Log
                if (TxtLogConsoleHeader != null) TxtLogConsoleHeader.Text = TxaLanguageManager.GetString("t_log_console", "📜 NHẬT KÝ HOẠT ĐỘNG (REAL-TIME LOGS)");
                if (BtnCopyLogsTab1 != null) BtnCopyLogsTab1.Content = TxaLanguageManager.GetString("t_btn_copy_logs", "📋 Copy Logs");
                if (BtnClearLogTab1 != null) BtnClearLogTab1.Content = TxaLanguageManager.GetString("t_btn_clear_log_short", "🧹 Clear");

                // Tab 2 Toolbar & Stats
                if (TxtFilterAppLabel != null) TxtFilterAppLabel.Text = TxaLanguageManager.GetString("t_filter_app", "📱 LỌC THEO APP: ");
                if (BtnRefreshProcesses != null) BtnRefreshProcesses.Content = TxaLanguageManager.GetString("t_btn_scan_app", "🔄 Quét App");
                if (BtnClearRequests != null) BtnClearRequests.Content = TxaLanguageManager.GetString("t_btn_clear_table", "🧹 Xóa Bảng");
                if (BtnToggleCapture != null) BtnToggleCapture.Content = _isCapturingTraffic ? TxaLanguageManager.GetString("t_btn_pause", "⏸ Tạm Dừng") : TxaLanguageManager.GetString("t_btn_resume", "▶ Tiếp Tục");

                if (TxtStatRequestsLabel != null) TxtStatRequestsLabel.Text = TxaLanguageManager.GetString("t_stat_requests", "📊 Tổng Requests");
                if (TxtStatDownloadLabel != null) TxtStatDownloadLabel.Text = TxaLanguageManager.GetString("t_stat_download", "📥 Đã Tải Về (Download)");
                if (TxtStatUploadLabel != null) TxtStatUploadLabel.Text = TxaLanguageManager.GetString("t_stat_upload", "📤 Đã Tải Lên (Upload)");
                if (TxtStatSpeedLabel != null) TxtStatSpeedLabel.Text = TxaLanguageManager.GetString("t_stat_speed", "⚡ Tốc Độ Mạng Hiện Tại");

                // Tab 2 Filters
                if (TxtFilterStatusLabel2 != null) TxtFilterStatusLabel2.Text = TxaLanguageManager.GetString("t_filter_status", "Lọc trạng thái: ");
                if (TxtFilterAll2 != null) TxtFilterAll2.Text = TxaLanguageManager.GetString("t_filter_all", "Tất cả");
                if (TxtTipTab2Header != null) TxtTipTab2Header.Text = TxaLanguageManager.GetString("t_tip_tab2", "💡 Nhấp đúp vào dòng Request để mở Modal & Copy Response");

                // Tab 2 Grid Columns
                if (ColReqTime != null) ColReqTime.Header = TxaLanguageManager.GetString("t_col_time", "Thời Gian");
                if (ColReqApp != null) ColReqApp.Header = TxaLanguageManager.GetString("t_col_app", "Ứng Dụng");
                if (ColReqProto != null) ColReqProto.Header = TxaLanguageManager.GetString("t_col_proto", "Giao Thức");
                if (ColReqHost != null) ColReqHost.Header = TxaLanguageManager.GetString("t_col_host", "Host / Endpoint URL");
                if (ColReqSize != null) ColReqSize.Header = TxaLanguageManager.GetString("t_col_size", "Dung Lượng (↓/↑)");
                if (ColReqLatency != null) ColReqLatency.Header = TxaLanguageManager.GetString("t_col_latency", "Độ Trễ");
                if (ColReqStatus != null) ColReqStatus.Header = TxaLanguageManager.GetString("t_col_status", "Trạng Thái");
                if (ColReqBlockAction != null) ColReqBlockAction.Header = TxaLanguageManager.GetString("t_col_block_action", "🛡️ Chặn API / Tắt Bật");
                if (TxtSearchWatermark != null) TxtSearchWatermark.Text = TxaLanguageManager.GetString("t_search_placeholder", "🔍 Tìm kiếm URL, API, Domain, IP...");

                // Bottom Tip
                if (TxtBottomTip != null)
                {
                    TxtBottomTip.Text = (MainTabControl?.SelectedIndex == 0)
                        ? TxaLanguageManager.GetString("t_tip_tab1", "💡 Mẹo: Nhấn '1-Click Auto Fix' để tự động ghim IP sạch & sửa lỗi Steam không cần VPN")
                        : TxaLanguageManager.GetString("t_tip_tab2", "💡 Mẹo: Nhấp đúp vào bất kỳ dòng Request nào để mở Modal chi tiết và Copy Response");
                }

                CheckAdminRights();
                RefreshSteamSentinel();
            });
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // CRITICAL: Stop bubbling events from inner ComboBoxes or DataGrids from switching tabs
            if (!ReferenceEquals(e.OriginalSource, MainTabControl))
            {
                return;
            }

            if (TxtBottomTip != null)
            {
                if (MainTabControl.SelectedIndex == 0)
                {
                    TxtBottomTip.Text = TxaLanguageManager.GetString("t_tip_tab1", "💡 Mẹo: Nhấn '1-Click Auto Fix' để tự động ghim IP sạch & sửa lỗi Steam không cần VPN");
                }
                else
                {
                    TxtBottomTip.Text = TxaLanguageManager.GetString("t_tip_tab2", "💡 Mẹo: Nhấp đúp vào bất kỳ dòng Request nào để mở Modal chi tiết và Copy Response");
                }
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Explicitly ensure Tab 1 (Steam Route Fixer) is selected on start
            MainTabControl.SelectedIndex = 0;

            ThemeManager.ApplyDwmBackdrop(this, _config.Theme == "WinUI3");
            RefreshSteamSentinel();
            RefreshProcessesList();
            TxaLogger.Info($"Giao diện Steam Route Fixer v1.0.0 đã sẵn sàng trên .NET 10.");
            TxaLogger.Info($"Thư mục làm việc: {StoragePathManager.AppDataRoot}");
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            _steamSentinelTimer.Stop();
            _trafficTimer.Stop();
            _processRefreshTimer.Stop();
            _httpSniffer.Stop();
        }

        private void CheckAdminRights()
        {
            bool isAdmin = false;
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { }

            if (isAdmin)
            {
                TxtAdminStatus.Text = TxaLanguageManager.GetString("t_admin_ok", "🛡️ Administrator");
                BadgeAdmin.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 22, 56, 32));
            }
            else
            {
                TxtAdminStatus.Text = TxaLanguageManager.GetString("t_admin_needed", "⚠️ Cần Quyền Admin");
                BadgeAdmin.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 90, 40, 20));
            }
        }

        private void InitSteamPresets()
        {
            _allDomains.Clear();

            var presets = new (string Category, string Name, string Domain)[]
            {
                ("Cửa Hàng (Store)", "Steam Store Official", "store.steampowered.com"),
                ("Cộng Đồng (Community)", "Steam Community & Market", "steamcommunity.com"),
                ("Cổng Thông Tin", "Steam Main Portal", "steampowered.com"),
                ("Đăng Nhập (Auth)", "Steam Login & Auth", "login.steampowered.com"),
                ("Thanh Toán", "Steam Checkout Service", "checkout.steampowered.com"),
                ("Hỗ Trợ (Support)", "Steam Help & Support", "help.steampowered.com"),
                ("API Web", "Steam Web API", "api.steampowered.com"),
                ("Tải Game (CDN)", "Akamai CDN Steam", "steamcdn-a.akamaihd.net"),
                ("Hình Ảnh (CDN)", "Cloudflare Steam Static", "clan.cloudflare.steamstatic.com"),
                ("Static Assets", "Steam Static Assets CDN", "cdn.cloudflare.steamstatic.com")
            };

            var pinned = HostsManager.ReadPinnedBlock();

            foreach (var p in presets)
            {
                var item = new DomainItem
                {
                    Category = p.Category,
                    DisplayName = p.Name,
                    Domain = p.Domain,
                    Status = DomainStatus.Pending
                };

                if (pinned.TryGetValue(p.Domain, out var pinnedIp))
                {
                    item.IsPinned = true;
                    item.BestIp = pinnedIp;
                    item.StatusText = "Đã ghim trong Hosts";
                    item.StatusBadgeColor = "#0078D4";
                }

                _allDomains.Add(item);
            }

            ApplyTab1Filter();
        }

        private void InitTimers()
        {
            // Steam Sentinel Watcher (every 2.5 seconds)
            _steamSentinelTimer.Interval = TimeSpan.FromSeconds(2.5);
            _steamSentinelTimer.Tick += (s, e) => RefreshSteamSentinel();
            _steamSentinelTimer.Start();

            // Traffic Monitor loop (every 1 second)
            _trafficTimer.Interval = TimeSpan.FromSeconds(1.0);
            _trafficTimer.Tick += (s, e) => PollTraffic();
            _trafficTimer.Start();

            // Process auto-refresher (every 3 seconds)
            _processRefreshTimer.Interval = TimeSpan.FromSeconds(3.0);
            _processRefreshTimer.Tick += (s, e) => RefreshProcessesList(silent: true);
            _processRefreshTimer.Start();

            // Local HTTP sniffer listener
            _httpSniffer.OnRequestCaptured += (req) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (_isCapturingTraffic)
                    {
                        AddCapturedRequest(req);
                    }
                });
            };
            if (_config.EnableHttpProxySniffer)
            {
                _httpSniffer.Start(_config.SnifferPort);
            }
        }

        #region Steam Sentinel & Tab 1 Logic

        private void RefreshSteamSentinel()
        {
            _steamStatus = SteamDetector.DetectSteam(_config.CustomSteamPath);

            TxtSteamStatusSummary.Text = _steamStatus.StatusSummary;
            TxtSteamStatusSummary.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_steamStatus.StatusColor)
            );

            TxtSteamPathDisplay.Text = string.IsNullOrEmpty(_steamStatus.ExecutablePath)
                ? "Chưa phát hiện đường dẫn cài đặt Steam."
                : $"Đường dẫn Steam: {_steamStatus.ExecutablePath}";

            BtnCloseSteam.Visibility = _steamStatus.IsRunning ? Visibility.Visible : Visibility.Collapsed;
            BtnLaunchSteam.Visibility = (!_steamStatus.IsRunning && _steamStatus.IsInstalled) ? Visibility.Visible : Visibility.Collapsed;

            // Highlight border if steam is running
            SteamSentinelBorder.BorderBrush = _steamStatus.IsRunning
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(216, 59, 1))
                : (System.Windows.Media.Brush)FindResource("CardBorderBrush");

            // Dim action buttons and display notice banner if steam is running
            bool isRunning = _steamStatus.IsRunning;
            SteamRunningNoticeBanner.Visibility = isRunning ? Visibility.Visible : Visibility.Collapsed;
            StackActionButtons.Opacity = isRunning ? 0.45 : 1.0;
            BtnAutoFix.IsEnabled = !isRunning;
            BtnDiagnose.IsEnabled = !isRunning;
            BtnRevertHosts.IsEnabled = !isRunning;
            BtnFlushDns.IsEnabled = !isRunning;
        }

        private void BtnCloseSteam_Click(object sender, RoutedEventArgs e)
        {
            TxaLogger.Warn("Đang đóng toàn bộ tiến trình Steam (steam.exe, steamwebhelper.exe)...");
            if (SteamDetector.CloseSteamProcesses())
            {
                TxaLogger.Success("Đã đóng Steam thành công. Hệ thống đã giải phóng socket và cache kết nối!");
                RefreshSteamSentinel();
            }
            else
            {
                TxaLogger.Error("Không thể đóng tiến trình Steam. Vui lòng đóng Steam thủ công hoặc chạy với quyền Admin.");
            }
        }

        private void BtnLaunchSteam_Click(object sender, RoutedEventArgs e)
        {
            TxaLogger.Info("Đang mở lại ứng dụng Steam...");
            if (SteamDetector.LaunchSteam(_steamStatus.ExecutablePath))
            {
                TxaLogger.Success("Đã mở Steam thành công!");
                RefreshSteamSentinel();
            }
        }

        private async void BtnDiagnose_Click(object sender, RoutedEventArgs e)
        {
            await RunDiagnosisWorkflowAsync(applyFix: false);
        }

        private async void BtnAutoFix_Click(object sender, RoutedEventArgs e)
        {
            if (_steamStatus.IsRunning)
            {
                if (MessageBox.Show("Steam đang chạy! Để áp dụng cấu hình DNS sạch hiệu quả nhất, khuyến nghị đóng Steam trước.\n\nBạn có muốn tự động đóng Steam ngay bây giờ không?", "Cảnh báo Steam đang chạy", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    SteamDetector.CloseSteamProcesses();
                    RefreshSteamSentinel();
                }
            }

            await RunDiagnosisWorkflowAsync(applyFix: true);
        }

        private async Task RunDiagnosisWorkflowAsync(bool applyFix)
        {
            BtnAutoFix.IsEnabled = false;
            BtnDiagnose.IsEnabled = false;
            BtnRevertHosts.IsEnabled = false;

            int total = _allDomains.Count;
            var swTotal = Stopwatch.StartNew();

            TxaLogger.Info($"=== BẮT ĐẦU {(applyFix ? "TỰ ĐỘNG SỬA LỖI STEAM" : "CHẨN ĐOÁN KẾT NỐI")} ({total} DOMAINS) ===");
            MainProgressBar.Value = 0;

            var pinsToApply = new Dictionary<string, string>();
            var domainTimes = new List<double>();

            for (int i = 0; i < total; i++)
            {
                var domainItem = _allDomains[i];
                domainItem.Status = DomainStatus.Diagnosing;

                int currentProgress = (int)(((double)i / total) * 100);
                MainProgressBar.Value = currentProgress;
                TxtProgressPercent.Text = $"{currentProgress}%";
                TxtProgressStatus.Text = $"Đang chẩn đoán: {domainItem.Domain} ({i + 1}/{total})...";

                // Estimate ETA
                if (domainTimes.Count > 0)
                {
                    double avgSeconds = domainTimes.Average();
                    double remainingSeconds = avgSeconds * (total - i);
                    TxtEtaCountdown.Text = $"ETA: {FormatHelper.FormatEta(remainingSeconds)}";
                }

                var swStep = Stopwatch.StartNew();
                TxaLogger.Diag($"[1/3] Thu thập DNS và đo TLS SNI cho {domainItem.Domain}...");

                var report = await _tlsProber.DiagnoseDomainAsync(domainItem.Domain, _dnsGatherer);
                swStep.Stop();
                domainTimes.Add(swStep.Elapsed.TotalSeconds);

                domainItem.Status = report.Verdict;
                domainItem.LatencyMs = report.BestLatencyMs;
                domainItem.SystemIp = report.SystemIps.Count > 0 ? string.Join(", ", report.SystemIps) : "Không tìm thấy";
                domainItem.BestIp = report.BestIp ?? "Không có IP sạch";

                if (report.Verdict == DomainStatus.DnsPoisoned)
                {
                    TxaLogger.Warn($"⚠️ {domainItem.Domain}: Phát hiện DNS bị nhà mạng VN chặn! IP sạch tốt nhất: {report.BestIp} ({report.BestLatencyMs}ms)");
                    if (report.BestIp != null)
                    {
                        pinsToApply[domainItem.Domain] = report.BestIp;
                    }
                }
                else if (report.Verdict == DomainStatus.Open)
                {
                    TxaLogger.Success($"✓ {domainItem.Domain}: Kết nối thông suốt bình thường ({report.BestLatencyMs}ms).");
                }
                else
                {
                    TxaLogger.Error($"✕ {domainItem.Domain}: {report.SummaryMessage}");
                }
            }

            MainProgressBar.Value = 100;
            TxtProgressPercent.Text = "100%";
            TxtEtaCountdown.Text = "ETA: Hoàn tất";

            if (applyFix)
            {
                TxtProgressStatus.Text = "Đang sao lưu file Hosts và ghim các IP sạch...";
                TxaLogger.Info($"[2/3] Đang sao lưu file hosts vào {StoragePathManager.BackupsDirectory}...");

                if (pinsToApply.Count > 0)
                {
                    if (HostsManager.PinMultiple(pinsToApply))
                    {
                        TxaLogger.Success($"✓ Đã ghim thành công {pinsToApply.Count} tên miền vào file Hosts Windows an toàn.");
                    }
                    else
                    {
                        TxaLogger.Error("Không thể ghi file Hosts. Hãy đảm bảo bạn chạy phần mềm với quyền Administrator.");
                    }
                }
                else
                {
                    TxaLogger.Info("Mọi tên miền đều đã thông suốt hoặc không cần ghim.");
                }

                TxaLogger.Info("[3/3] Đang làm mới bộ nhớ đệm DNS hệ thống (Flush DNS)...");
                if (DnsFlusher.FlushDnsCache())
                {
                    TxaLogger.Success("✓ Đã Flush DNS Windows thành công!");
                }

                // Update UI status to Fixed for pinned domains
                foreach (var d in _allDomains)
                {
                    if (pinsToApply.ContainsKey(d.Domain))
                    {
                        d.Status = DomainStatus.Fixed;
                        d.IsPinned = true;
                    }
                }

                TxtProgressStatus.Text = $"✅ Hoàn tất! Đã sửa xong {pinsToApply.Count} domain Steam. Bạn có thể mở Steam ngay mà không cần VPN/1.1.1.1!";
                TxaLogger.Success($"=== HOÀN TẤT AUTO FIX STEAM TRONG {swTotal.Elapsed.TotalSeconds:0.0}s ===");

                MessageBox.Show($"Đã sửa lỗi kết nối Steam thành công ({pinsToApply.Count} tên miền được tối ưu IP sạch)!\n\nBây giờ bạn có thể mở Steam và đăng nhập / mua game bình thường mà không cần bật VPN hay 1.1.1.1.", "Sửa lỗi hoàn tất", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                TxtProgressStatus.Text = $"Chẩn đoán hoàn tất trong {swTotal.Elapsed.TotalSeconds:0.0}s. Tìm thấy {pinsToApply.Count} domain bị chặn DNS.";
                TxaLogger.Info($"=== CHẨN ĐOÁN HOÀN TẤT ===");
            }

            BtnAutoFix.IsEnabled = true;
            BtnDiagnose.IsEnabled = true;
            BtnRevertHosts.IsEnabled = true;
            ApplyTab1Filter();
        }

        private void BtnRevertHosts_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Bạn có chắc chắn muốn xóa toàn bộ các IP Steam đã ghim và khôi phục file hosts về trạng thái gốc?", "Xác nhận khôi phục", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                TxaLogger.Warn("Đang khôi phục file hosts gốc...");
                if (HostsManager.ClearAllPinned())
                {
                    DnsFlusher.FlushDnsCache();
                    TxaLogger.Success("✓ Đã xóa sạch cấu hình ghim Steam trong hosts và Flush DNS thành công!");

                    foreach (var d in _allDomains)
                    {
                        d.IsPinned = false;
                        d.Status = DomainStatus.Pending;
                    }
                    ApplyTab1Filter();
                    MessageBox.Show("Đã khôi phục file hosts gốc thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    TxaLogger.Error("Không thể sửa file hosts. Vui lòng kiểm tra quyền Administrator.");
                }
            }
        }

        private void BtnFlushDns_Click(object sender, RoutedEventArgs e)
        {
            TxaLogger.Info("Đang thực hiện làm mới DNS cache (Flush DNS)...");
            if (DnsFlusher.FlushDnsCache())
            {
                TxaLogger.Success("✓ Đã Flush DNS Windows thành công!");
                MessageBox.Show("Đã làm mới DNS Cache thành công!", "Flush DNS", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ApplyTab1Filter()
        {
            _viewDomains.Clear();
            foreach (var item in _allDomains)
            {
                bool match = _tab1StatusFilter switch
                {
                    "Open" => item.Status == DomainStatus.Open || item.Status == DomainStatus.Fixed,
                    "Poisoned" => item.Status == DomainStatus.DnsPoisoned,
                    "Blocked" => item.Status == DomainStatus.SniBlocked || item.Status == DomainStatus.IpBlocked || item.Status == DomainStatus.Unreachable,
                    _ => true
                };

                if (match) _viewDomains.Add(item);
            }
        }

        private void FilterTab1_All_Click(object sender, RoutedEventArgs e) { _tab1StatusFilter = "All"; ApplyTab1Filter(); }
        private void FilterTab1_Open_Click(object sender, RoutedEventArgs e) { _tab1StatusFilter = "Open"; ApplyTab1Filter(); }
        private void FilterTab1_Poisoned_Click(object sender, RoutedEventArgs e) { _tab1StatusFilter = "Poisoned"; ApplyTab1Filter(); }
        private void FilterTab1_Blocked_Click(object sender, RoutedEventArgs e) { _tab1StatusFilter = "Blocked"; ApplyTab1Filter(); }

        #endregion

        #region Tab 2: HTTP Traffic & Process Inspector Logic

        private void RefreshProcessesList(bool silent = false)
        {
            var activePids = _allRequests.Select(r => r.Pid).Where(p => p > 0).ToHashSet();
            var procs = _processTracker.ScanActiveNetworkProcesses(activePids);

            int prevPid = _selectedFilterPid;
            CmbProcesses.ItemsSource = procs;

            var selected = procs.FirstOrDefault(p => p.Pid == prevPid) ?? procs.FirstOrDefault();
            if (selected != null)
            {
                CmbProcesses.SelectedItem = selected;
            }

            if (!silent)
            {
                TxaLogger.Info($"Đã quét và phát hiện {procs.Count - 1} ứng dụng có hoạt động mạng kết nối.");
            }
        }

        private void BtnRefreshProcesses_Click(object sender, RoutedEventArgs e)
        {
            RefreshProcessesList();
        }

        private void CmbProcesses_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // CRITICAL: Stop bubbling so TabControl does not switch tabs!
            e.Handled = true;

            if (CmbProcesses.SelectedItem is ProcessItem proc)
            {
                _selectedFilterPid = proc.Pid;
                ApplyTab2Filter();
            }
        }

        private bool _isPollingTraffic = false;

        private async void PollTraffic()
        {
            if (!_isCapturingTraffic || _isPollingTraffic) return;
            _isPollingTraffic = true;

            try
            {
                var newConnections = await _tcpWatcher.GetNewActiveConnectionsAsync(_selectedFilterPid);
                if (newConnections.Count > 0 && _isCapturingTraffic)
                {
                    foreach (var item in newConnections)
                    {
                        AddCapturedRequest(item);
                    }
                }

                // Calculate live network speed
                double elapsedSec = (DateTime.Now - _lastSpeedCheckTime).TotalSeconds;
                if (elapsedSec >= 1.0)
                {
                    long bytesDiff = _totalDownloadedBytes - _lastDownloadedBytes;
                    double speed = Math.Max(0, bytesDiff / elapsedSec);
                    TxtStatSpeed.Text = FormatHelper.FormatSpeed(speed);

                    _lastDownloadedBytes = _totalDownloadedBytes;
                    _lastSpeedCheckTime = DateTime.Now;
                }
            }
            catch { }
            finally
            {
                _isPollingTraffic = false;
            }
        }

        private void AddCapturedRequest(NetworkRequestItem item)
        {
            if (!string.IsNullOrEmpty(item.Host) && _blockedApiHosts.Contains(item.Host))
            {
                item.IsBlocked = true;
            }
            else if (!string.IsNullOrEmpty(item.RemoteIp) && _blockedApiHosts.Contains(item.RemoteIp))
            {
                item.IsBlocked = true;
            }

            _allRequests.Insert(0, item);
            if (_allRequests.Count > 300) _allRequests.RemoveAt(_allRequests.Count - 1);

            _totalDownloadedBytes += item.ResponseBytes;
            _totalUploadedBytes += item.RequestBytes;

            // Update stats text with dynamic format
            TxtStatRequests.Text = $"{_allRequests.Count} reqs";
            TxtStatDownload.Text = FormatHelper.FormatBytes(_totalDownloadedBytes);
            TxtStatUpload.Text = FormatHelper.FormatBytes(_totalUploadedBytes);

            ApplyTab2FilterSingle(item);
        }

        private void ApplyTab2Filter()
        {
            _viewRequests.Clear();
            string keyword = TxtSearchRequest.Text.Trim().ToLowerInvariant();

            foreach (var req in _allRequests)
            {
                if (MatchesTab2Filter(req, keyword))
                {
                    _viewRequests.Add(req);
                }
            }
        }

        private void ApplyTab2FilterSingle(NetworkRequestItem item)
        {
            string keyword = TxtSearchRequest.Text.Trim().ToLowerInvariant();
            if (MatchesTab2Filter(item, keyword))
            {
                _viewRequests.Insert(0, item);
                if (_viewRequests.Count > 500) _viewRequests.RemoveAt(_viewRequests.Count - 1);
            }
        }

        private bool MatchesTab2Filter(NetworkRequestItem req, string keyword)
        {
            if (_selectedFilterPid != 0 && req.Pid != _selectedFilterPid) return false;

            bool statusMatch = _tab2StatusFilter switch
            {
                "2xx" => req.StatusCategory == HttpStatusCategory.Success2xx,
                "3xx" => req.StatusCategory == HttpStatusCategory.Redirect3xx,
                "4xx" => req.StatusCategory == HttpStatusCategory.ClientError4xx,
                "5xx" => req.StatusCategory == HttpStatusCategory.ServerError5xx,
                "Active" => req.StatusCategory == HttpStatusCategory.ActivePending,
                _ => true
            };
            if (!statusMatch) return false;

            if (!string.IsNullOrEmpty(keyword))
            {
                return req.Host.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                       req.Url.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                       req.RemoteIp.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                       req.ProcessName.Contains(keyword, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        private void TxtSearchRequest_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtSearchWatermark != null)
            {
                TxtSearchWatermark.Visibility = string.IsNullOrEmpty(TxtSearchRequest.Text) ? Visibility.Visible : Visibility.Collapsed;
            }
            ApplyTab2Filter();
        }

        private void BtnToggleBlockApi_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is NetworkRequestItem req)
            {
                string targetHost = !string.IsNullOrEmpty(req.Host) ? req.Host : req.RemoteIp;
                if (string.IsNullOrEmpty(targetHost)) return;

                bool isCurrentlyBlocked = _blockedApiHosts.Contains(targetHost);
                bool willBeBlocked = !isCurrentlyBlocked;

                if (willBeBlocked)
                {
                    _blockedApiHosts.Add(targetHost);
                    _httpSniffer.BlockedHosts.Add(targetHost);
                    TxaLogger.Error($"🛡️ [CHẶN API] Đã KÍCH HOẠT chặn toàn bộ các Request kết nối tới: {targetHost}");
                }
                else
                {
                    _blockedApiHosts.Remove(targetHost);
                    _httpSniffer.BlockedHosts.Remove(targetHost);
                    TxaLogger.Success($"🔓 [BỎ CHẶN] Đã HỦY chặn các Request kết nối tới: {targetHost}");
                }

                // Update all items in active memory with matching host
                foreach (var item in _allRequests)
                {
                    if (string.Equals(item.Host, targetHost, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(item.RemoteIp, targetHost, StringComparison.OrdinalIgnoreCase))
                    {
                        item.IsBlocked = willBeBlocked;
                    }
                }
            }
        }

        private void FilterTab2_All_Click(object sender, RoutedEventArgs e) { _tab2StatusFilter = "All"; ApplyTab2Filter(); }
        private void FilterTab2_2xx_Click(object sender, RoutedEventArgs e) { _tab2StatusFilter = "2xx"; ApplyTab2Filter(); }
        private void FilterTab2_3xx_Click(object sender, RoutedEventArgs e) { _tab2StatusFilter = "3xx"; ApplyTab2Filter(); }
        private void FilterTab2_4xx_Click(object sender, RoutedEventArgs e) { _tab2StatusFilter = "4xx"; ApplyTab2Filter(); }
        private void FilterTab2_5xx_Click(object sender, RoutedEventArgs e) { _tab2StatusFilter = "5xx"; ApplyTab2Filter(); }
        private void FilterTab2_Active_Click(object sender, RoutedEventArgs e) { _tab2StatusFilter = "Active"; ApplyTab2Filter(); }

        private void BtnToggleCapture_Click(object sender, RoutedEventArgs e)
        {
            _isCapturingTraffic = !_isCapturingTraffic;
            BtnToggleCapture.Content = _isCapturingTraffic ? "⏸ Tạm Dừng" : "▶ Tiếp Tục";
            TxaLogger.Info(_isCapturingTraffic ? "Đã tiếp tục bắt lưu lượng traffic." : "Đã tạm dừng bắt lưu lượng traffic.");
        }

        private void BtnClearRequests_Click(object sender, RoutedEventArgs e)
        {
            _allRequests.Clear();
            _viewRequests.Clear();
            _totalDownloadedBytes = 0;
            _totalUploadedBytes = 0;
            TxtStatRequests.Text = "0 reqs";
            TxtStatDownload.Text = "0 B";
            TxtStatUpload.Text = "0 B";
            TxtStatSpeed.Text = "0 B/s";
        }

        private void GridRequests_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (GridRequests.SelectedItem is NetworkRequestItem selected)
            {
                var modal = new RequestDetailModal(selected) { Owner = this };
                modal.ShowDialog();
            }
        }

        #endregion

        #region Menu Bar Handlers

        private void MenuOpenSteamDir_Click(object sender, RoutedEventArgs e)
        {
            string dir = _steamStatus.InstallPath;
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                dir = @"C:\Program Files (x86)\Steam";
            }

            if (Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{dir}\"",
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show("Chưa tìm thấy thư mục cài đặt Steam trên máy tính.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void MenuOpenHostsFile_Click(object sender, RoutedEventArgs e)
        {
            string hostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = $"\"{hostsPath}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể mở file hosts: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MenuExportLogs_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Xuất nhật ký Logs",
                Filter = "Text Log File (*.txt)|*.txt|All Files (*.*)|*.*",
                FileName = $"SteamRoute_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (sfd.ShowDialog() == true)
            {
                var sb = new StringBuilder();
                foreach (var l in _logEntries)
                {
                    sb.AppendLine($"[{l.TimeString}] {l.LevelPrefix} {l.Message}");
                }
                File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show("Đã xuất file log thành công!", "Xuất Logs", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void MenuOpenAppData_Click(object sender, RoutedEventArgs e)
        {
            StoragePathManager.EnsureDirectories();
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{StoragePathManager.AppDataRoot}\"",
                UseShellExecute = true
            });
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MenuCopyDomains_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            foreach (var d in _allDomains)
            {
                sb.AppendLine($"{d.Domain,-35} | {d.StatusText,-20} | Best IP: {d.BestIp,-15} | Ping: {d.LatencyDisplay}");
            }
            Clipboard.SetText(sb.ToString());
            MessageBox.Show("Đã copy danh sách tên miền vào Clipboard!", "Copy", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuCopyLogs_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            foreach (var l in _logEntries)
            {
                sb.AppendLine($"[{l.TimeString}] {l.LevelPrefix} {l.Message}");
            }
            Clipboard.SetText(sb.ToString());
            MessageBox.Show("Đã copy toàn bộ Logs vào Clipboard!", "Copy Logs", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuClearLog_Click(object sender, RoutedEventArgs e)
        {
            _logEntries.Clear();
        }

        private void MenuRefreshAll_Click(object sender, RoutedEventArgs e)
        {
            RefreshSteamSentinel();
            RefreshProcessesList();
            InitSteamPresets();
            TxaLogger.Info("Đã làm mới toàn bộ dữ liệu.");
        }

        private void MenuThemeWinUI3_Click(object sender, RoutedEventArgs e)
        {
            _config.Theme = "WinUI3";
            StoragePathManager.SaveConfig(_config);
            ThemeManager.ApplyTheme("WinUI3", this);
        }

        private void MenuThemeSteamDark_Click(object sender, RoutedEventArgs e)
        {
            _config.Theme = "SteamDark";
            StoragePathManager.SaveConfig(_config);
            ThemeManager.ApplyTheme("SteamDark", this);
        }

        private void MenuThemeVSCode_Click(object sender, RoutedEventArgs e)
        {
            _config.Theme = "VSCode";
            StoragePathManager.SaveConfig(_config);
            ThemeManager.ApplyTheme("VSCode", this);
        }

        private void MenuSettings_Click(object sender, RoutedEventArgs e)
        {
            var modal = new SettingsModal { Owner = this };
            if (modal.ShowDialog() == true)
            {
                _config = StoragePathManager.LoadConfig();
                RefreshSteamSentinel();
            }
        }

        private void MenuCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            var modal = new UpdateModal { Owner = this };
            modal.ShowDialog();
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            var modal = new AboutModal { Owner = this };
            modal.ShowDialog();
        }

        private void BtnGithubTop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/TXAVL/SteamRouteFixer",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        #endregion
    }
}
