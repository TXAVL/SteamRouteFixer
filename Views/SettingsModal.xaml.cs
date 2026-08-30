using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using SteamRouteFixer.Models;
using SteamRouteFixer.Services.Common;
using SteamRouteFixer.Services.SteamFix;

namespace SteamRouteFixer.Views
{
    public partial class SettingsModal : Window
    {
        private readonly AppConfig _config;

        private bool _isUpdatingLanguageDropdown = false;

        public SettingsModal()
        {
            InitializeComponent();
            _config = StoragePathManager.LoadConfig();
            ApplyLanguageTranslations();
            TxaLanguageManager.OnLanguageChanged += ApplyLanguageTranslations;
            Closed += (s, e) => TxaLanguageManager.OnLanguageChanged -= ApplyLanguageTranslations;
            LoadSettings();
        }

        private void ApplyLanguageTranslations()
        {
            Title = TxaLanguageManager.GetString("t_settings_title", "Cài Đặt Hệ Thống & Giao Diện");
            if (TxtSettingsMainTitle != null) TxtSettingsMainTitle.Text = TxaLanguageManager.GetString("t_settings_header", "⚙️ THIẾT LẬP CẤU HÌNH ỨNG DỤNG");

            if (TxtThemeHeader != null) TxtThemeHeader.Text = TxaLanguageManager.GetString("t_theme_header", "🎨 CHỦ ĐỀ GIAO DIỆN (THEME)");
            if (TxtThemeDesc != null) TxtThemeDesc.Text = TxaLanguageManager.GetString("t_theme_desc", "Tùy biến phong cách hiển thị WinUI 3 Fluent, Steam Dark Gaming hoặc VS Code Studio Dark:");
            if (RbThemeWinUI3 != null) RbThemeWinUI3.Content = TxaLanguageManager.GetString("t_theme_winui3", "WinUI 3 (Windows 11 Fluent Mica)");
            if (RbThemeSteam != null) RbThemeSteam.Content = TxaLanguageManager.GetString("t_theme_steam", "Steam Dark (Cyberpunk Glow)");
            if (RbThemeVSCode != null) RbThemeVSCode.Content = TxaLanguageManager.GetString("t_theme_vscode", "VS Code Studio Dark");

            if (TxtLangHeader != null) TxtLangHeader.Text = TxaLanguageManager.GetString("t_lang_header", "🌐 NGÔN NGỮ ỨNG DỤNG (TXA LANGUAGE)");
            if (TxtLangDesc != null) TxtLangDesc.Text = TxaLanguageManager.GetString("t_lang_desc", "Quét tự động các gói ngôn ngữ (.txal) trong %LocalAppData%\\SteamRouteFixer\\languages\\:");
            if (BtnImportTxa != null) BtnImportTxa.Content = TxaLanguageManager.GetString("t_btn_import_txa", "📂 Nạp File .txal");
            if (BtnOpenLangFolder != null) BtnOpenLangFolder.Content = TxaLanguageManager.GetString("t_btn_open_lang_dir", "📁 Mở Thư Mục Lang");
            if (BtnTranslateApp != null) BtnTranslateApp.Content = TxaLanguageManager.GetString("t_btn_translate_app", "🌐 Tự Tạo Bản Dịch Mới (Translate App...)");

            if (TxtSteamPathHeader != null) TxtSteamPathHeader.Text = TxaLanguageManager.GetString("t_steam_path_header", "🎮 ĐƯỜNG DẪN STEAM TÙY CHỌN");
            if (TxtSteamPathDesc != null) TxtSteamPathDesc.Text = TxaLanguageManager.GetString("t_steam_path_desc", "Nếu bạn cài Steam ở ổ đĩa khác và công cụ chưa tự nhận diện, hãy chọn file steam.exe:");
            if (BtnBrowseSteam != null) BtnBrowseSteam.Content = TxaLanguageManager.GetString("t_btn_browse", "📁 Chọn file steam.exe");

            if (TxtHostsBackupHeader != null) TxtHostsBackupHeader.Text = TxaLanguageManager.GetString("t_hosts_backup_header", "🛡️ SAO LƯU & KHÔI PHỤC FILE HOSTS");
            if (TxtHostsBackupDesc != null) TxtHostsBackupDesc.Text = TxaLanguageManager.GetString("t_hosts_backup_desc", "Danh sách các bản sao lưu tự động trong %LocalAppData%\\SteamRouteFixer\\backups\\:");
            if (BtnRestoreBackup != null) BtnRestoreBackup.Content = TxaLanguageManager.GetString("t_btn_restore_backup", "🔄 Khôi Phục Bản Chọn");
            if (BtnOpenAppData != null) BtnOpenAppData.Content = TxaLanguageManager.GetString("t_menu_appdata", "📂 Mở Thư Mục AppData");

            if (BtnSave != null) BtnSave.Content = TxaLanguageManager.GetString("t_btn_save", "💾 Lưu Cài Đặt");
            if (BtnCancel != null) BtnCancel.Content = TxaLanguageManager.GetString("t_btn_close", "Đóng");
        }

        private void LoadSettings()
        {
            if (_config.Theme == "SteamDark") RbThemeSteam.IsChecked = true;
            else if (_config.Theme == "VSCode") RbThemeVSCode.IsChecked = true;
            else RbThemeWinUI3.IsChecked = true;

            TxtSteamPath.Text = _config.CustomSteamPath;

            LoadLanguagesList();
            RefreshBackupsList();
        }

        private void LoadLanguagesList()
        {
            _isUpdatingLanguageDropdown = true;
            try
            {
                TxaLanguageManager.ScanAvailableLanguages();
                CmbLanguage.Items.Clear();

                int selectedIndex = 0;
                for (int i = 0; i < TxaLanguageManager.AvailableLanguages.Count; i++)
                {
                    var lang = TxaLanguageManager.AvailableLanguages[i];
                    CmbLanguage.Items.Add($"{lang.lang_name} ({lang.lang_code})");

                    if (lang.lang_code.Equals(TxaLanguageManager.CurrentLanguage.lang_code, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedIndex = i;
                    }
                }

                if (CmbLanguage.Items.Count > 0)
                {
                    CmbLanguage.SelectedIndex = selectedIndex;
                }
            }
            finally
            {
                _isUpdatingLanguageDropdown = false;
            }
        }

        private void CmbLanguage_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isUpdatingLanguageDropdown) return;

            if (CmbLanguage.SelectedIndex >= 0 && CmbLanguage.SelectedIndex < TxaLanguageManager.AvailableLanguages.Count)
            {
                var selected = TxaLanguageManager.AvailableLanguages[CmbLanguage.SelectedIndex];
                _config.LanguageCode = selected.lang_code;
                TxaLanguageManager.ApplyLanguageByCode(selected.lang_code, saveToConfig: true);
            }
        }

        private void BtnImportTxa_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = TxaLanguageManager.GetString("t_dialog_import_txa_title", "Chọn gói ngôn ngữ TXA Language (*.txal, *.txa)"),
                Filter = "TXA Language Package (*.txal;*.txa)|*.txal;*.txa|All files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                if (TxaLanguageManager.ImportAndApplyLanguageFile(dlg.FileName, showSuccessModal: true))
                {
                    _config.LanguageCode = TxaLanguageManager.CurrentLanguage.lang_code;
                    LoadLanguagesList();
                }
            }
        }

        private void BtnOpenLangFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!Directory.Exists(TxaLanguageManager.LanguagesDirectory))
                {
                    Directory.CreateDirectory(TxaLanguageManager.LanguagesDirectory);
                }
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{TxaLanguageManager.LanguagesDirectory}\"",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void BtnTranslateApp_Click(object sender, RoutedEventArgs e)
        {
            var ownerWin = this.Owner ?? Application.Current.MainWindow;
            this.Close();

            var modal = new TranslationEditorModal
            {
                Owner = ownerWin,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            modal.ShowDialog();

            // Re-open SettingsModal after TranslationEditorModal finishes
            var newSettings = new SettingsModal
            {
                Owner = ownerWin,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            newSettings.ShowDialog();
        }

        private void RefreshBackupsList()
        {
            LstBackups.Items.Clear();
            var backups = HostsManager.GetBackupFiles();
            foreach (var b in backups)
            {
                LstBackups.Items.Add(Path.GetFileName(b));
            }
        }

        private void RbTheme_Checked(object sender, RoutedEventArgs e)
        {
            string theme = "WinUI3";
            if (RbThemeSteam.IsChecked == true) theme = "SteamDark";
            else if (RbThemeVSCode.IsChecked == true) theme = "VSCode";

            ThemeManager.ApplyTheme(theme, Application.Current.MainWindow);
        }

        private void BtnBrowseSteam_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = TxaLanguageManager.GetString("t_dialog_browse_steam_title", "Chọn file steam.exe"),
                Filter = "Steam Executable (steam.exe)|steam.exe|All files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                TxtSteamPath.Text = dlg.FileName;
            }
        }

        private void BtnRestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            if (LstBackups.SelectedItem is string selectedFile)
            {
                string fullPath = Path.Combine(StoragePathManager.BackupsDirectory, selectedFile);
                string confirmFmt = TxaLanguageManager.GetString("t_backup_confirm_restore_fmt", "Bạn có chắc chắn muốn khôi phục file hosts từ bản sao lưu:\n{0}?");
                string confirmTitle = TxaLanguageManager.GetString("t_backup_restore_title", "Xác nhận khôi phục");

                if (TxaMessageBox.Show(this, string.Format(confirmFmt, selectedFile), confirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    if (HostsManager.RestoreBackup(fullPath))
                    {
                        DnsFlusher.FlushDnsCache();
                        string succMsg = TxaLanguageManager.GetString("t_backup_restore_success", "Khôi phục file hosts thành công và đã Flush DNS!");
                        string succTitle = TxaLanguageManager.GetString("t_dialog_success", "Thành công");
                        TxaMessageBox.Show(this, succMsg, succTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                        RefreshBackupsList();
                    }
                    else
                    {
                        string errMsg = TxaLanguageManager.GetString("t_backup_restore_error", "Không thể khôi phục file hosts. Vui lòng chạy phần mềm với quyền Administrator.");
                        string errTitle = TxaLanguageManager.GetString("t_dialog_error", "Lỗi");
                        TxaMessageBox.Show(this, errMsg, errTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                string warnMsg = TxaLanguageManager.GetString("t_backup_select_warning", "Vui lòng chọn 1 bản sao lưu trong danh sách!");
                string warnTitle = TxaLanguageManager.GetString("t_dialog_notice", "Thông báo");
                TxaMessageBox.Show(this, warnMsg, warnTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnOpenAppData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StoragePathManager.EnsureDirectories();
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{StoragePathManager.AppDataRoot}\"",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (RbThemeSteam.IsChecked == true) _config.Theme = "SteamDark";
            else if (RbThemeVSCode.IsChecked == true) _config.Theme = "VSCode";
            else _config.Theme = "WinUI3";

            _config.CustomSteamPath = TxtSteamPath.Text.Trim();
            StoragePathManager.SaveConfig(_config);

            DialogResult = true;
            Close();
        }
    }
}
