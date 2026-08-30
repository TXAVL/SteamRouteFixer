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

        public SettingsModal()
        {
            InitializeComponent();
            _config = StoragePathManager.LoadConfig();
            LoadSettings();
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
            TxaLanguageManager.ScanAvailableLanguages();
            CmbLanguage.Items.Clear();

            int selectedIndex = 0;
            for (int i = 0; i < TxaLanguageManager.AvailableLanguages.Count; i++)
            {
                var lang = TxaLanguageManager.AvailableLanguages[i];
                CmbLanguage.Items.Add($"{lang.lang_name} ({lang.lang_code})");

                if (lang.lang_code.Equals(_config.LanguageCode, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                }
            }

            if (CmbLanguage.Items.Count > 0)
            {
                CmbLanguage.SelectedIndex = selectedIndex;
            }
        }

        private void CmbLanguage_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
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
                Title = "Chọn gói ngôn ngữ TXA Language (*.txal, *.txa)",
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
            var modal = new TranslationEditorModal
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (modal.ShowDialog() == true)
            {
                LoadLanguagesList();
            }
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
                Title = "Chọn file steam.exe",
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
                if (MessageBox.Show($"Bạn có chắc chắn muốn khôi phục file hosts từ bản sao lưu:\n{selectedFile}?", "Xác nhận khôi phục", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    if (HostsManager.RestoreBackup(fullPath))
                    {
                        DnsFlusher.FlushDnsCache();
                        MessageBox.Show("Khôi phục file hosts thành công và đã Flush DNS!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                        RefreshBackupsList();
                    }
                    else
                    {
                        MessageBox.Show("Không thể khôi phục file hosts. Vui lòng chạy phần mềm với quyền Administrator.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Vui lòng chọn 1 bản sao lưu trong danh sách!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
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
