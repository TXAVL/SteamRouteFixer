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

            RefreshBackupsList();
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
