using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;

namespace SteamRouteFixer.Services.Common
{
    public class TxaLanguagePackage
    {
        public string lang_name { get; set; } = "Tiếng Việt";
        public string lang_code { get; set; } = "vi-VN";
        public string author { get; set; } = "TXAVL";
        public Dictionary<string, string> txa_key { get; set; } = new();
    }

    public static class TxaLanguageManager
    {
        private static readonly byte[] TxaMagic = Encoding.UTF8.GetBytes("TXALANG1");
        // Dedicated 256-bit AES encryption key for TXA proprietary language packages
        private static readonly byte[] AesKey = new byte[]
        {
            0x54, 0x58, 0x41, 0x5F, 0x53, 0x54, 0x55, 0x44,
            0x49, 0x4F, 0x5F, 0x53, 0x45, 0x43, 0x55, 0x52,
            0x45, 0x5F, 0x4C, 0x41, 0x4E, 0x47, 0x5F, 0x4B,
            0x45, 0x59, 0x5F, 0x32, 0x30, 0x32, 0x36, 0x21
        }; // "TXA_STUDIO_SECURE_LANG_KEY_2026!"

        private static readonly byte[] AesIv = new byte[]
        {
            0x53, 0x74, 0x65, 0x61, 0x6D, 0x52, 0x6F, 0x75,
            0x74, 0x65, 0x46, 0x69, 0x78, 0x65, 0x72, 0x21
        }; // "SteamRouteFixer!"

        public static string LanguagesDirectory => Path.Combine(StoragePathManager.AppDataRoot, "languages");

        public static TxaLanguagePackage CurrentLanguage { get; private set; } = new();
        public static List<TxaLanguagePackage> AvailableLanguages { get; private set; } = new();

        public static event Action? OnLanguageChanged;

        public static void Initialize(string? startupTxaFile = null)
        {
            try
            {
                if (!Directory.Exists(LanguagesDirectory))
                {
                    Directory.CreateDirectory(LanguagesDirectory);
                }

                // 1. Generate default built-in language files if missing
                GenerateDefaultPackages();

                // 2. Scan available languages
                ScanAvailableLanguages();

                // 3. Handle startup .txa file double-click
                if (!string.IsNullOrWhiteSpace(startupTxaFile) && File.Exists(startupTxaFile))
                {
                    ImportAndApplyLanguageFile(startupTxaFile, showSuccessModal: true);
                    return;
                }

                // 4. Load configured language or detect Windows OS default
                var cfg = StoragePathManager.LoadConfig();
                string targetCode = cfg.LanguageCode;

                if (string.IsNullOrWhiteSpace(targetCode))
                {
                    string sysLang = CultureInfo.CurrentUICulture.Name;
                    targetCode = sysLang.StartsWith("vi", StringComparison.OrdinalIgnoreCase) ? "vi-VN" : "en-US";
                }

                ApplyLanguageByCode(targetCode, saveToConfig: false);

                // 5. Register .txa file association in Windows Registry
                RegisterFileAssociation();
            }
            catch (Exception ex)
            {
                TxaLogger.Error($"Lỗi khởi tạo TxaLanguage: {ex.Message}");
            }
        }

        public static string GetString(string key, string fallback = "")
        {
            if (CurrentLanguage.txa_key.TryGetValue(key, out var val))
            {
                return val;
            }
            return !string.IsNullOrEmpty(fallback) ? fallback : key;
        }

        public static void ScanAvailableLanguages()
        {
            AvailableLanguages.Clear();
            if (!Directory.Exists(LanguagesDirectory)) return;

            // Scan both .txal and legacy .txa files
            var files = Directory.GetFiles(LanguagesDirectory, "*.*")
                .Where(f => f.EndsWith(".txal", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".txa", StringComparison.OrdinalIgnoreCase));

            foreach (var file in files)
            {
                var pkg = DecryptLanguageFile(file);
                if (pkg != null && !AvailableLanguages.Any(l => l.lang_code == pkg.lang_code))
                {
                    AvailableLanguages.Add(pkg);
                }
            }
        }

        public static bool ApplyLanguageByCode(string langCode, bool saveToConfig = true)
        {
            var target = AvailableLanguages.FirstOrDefault(l => l.lang_code.Equals(langCode, StringComparison.OrdinalIgnoreCase));
            if (target == null)
            {
                target = AvailableLanguages.FirstOrDefault(l => l.lang_code == "vi-VN") ?? AvailableLanguages.FirstOrDefault();
            }

            if (target != null)
            {
                CurrentLanguage = target;

                if (saveToConfig)
                {
                    var cfg = StoragePathManager.LoadConfig();
                    cfg.LanguageCode = target.lang_code;
                    StoragePathManager.SaveConfig(cfg);
                }

                OnLanguageChanged?.Invoke();
                return true;
            }
            return false;
        }

        public static bool ImportAndApplyLanguageFile(string filePath, bool showSuccessModal = false)
        {
            try
            {
                var pkg = DecryptLanguageFile(filePath);
                if (pkg == null || string.IsNullOrWhiteSpace(pkg.lang_code) || pkg.txa_key.Count == 0)
                {
                    MessageBox.Show(
                        "File ngôn ngữ .txa không hợp lệ hoặc đã bị chỉnh sửa/hư hại.",
                        "Lỗi Gói Ngôn Ngữ TXA",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                    return false;
                }

                // Copy to local languages directory with .txal extension
                string destFile = Path.Combine(LanguagesDirectory, $"{pkg.lang_code}.txal");
                File.Copy(filePath, destFile, true);

                ScanAvailableLanguages();
                ApplyLanguageByCode(pkg.lang_code, saveToConfig: true);

                if (showSuccessModal)
                {
                    string title = GetString("t_lang_switched_title", "Đổi Ngôn Ngữ Thành Công");
                    string msg = string.Format(
                        GetString("t_lang_switched_msg", "Đã nạp và áp dụng thành công gói ngôn ngữ: {0} ({1})"),
                        pkg.lang_name,
                        pkg.lang_code
                    );

                    MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Information);
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi nạp file .txal: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public static byte[] EncryptLanguagePackage(TxaLanguagePackage pkg)
        {
            string json = JsonSerializer.Serialize(pkg, new JsonSerializerOptions { WriteIndented = false });
            byte[] plainBytes = Encoding.UTF8.GetBytes(json);

            using var aes = Aes.Create();
            aes.Key = AesKey;
            aes.IV = AesIv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            byte[] encryptedData = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            // Output format: [TXALANG1 Magic (8 bytes)] + [Encrypted Payload]
            byte[] output = new byte[TxaMagic.Length + encryptedData.Length];
            Buffer.BlockCopy(TxaMagic, 0, output, 0, TxaMagic.Length);
            Buffer.BlockCopy(encryptedData, 0, output, TxaMagic.Length, encryptedData.Length);
            return output;
        }

        public static TxaLanguagePackage? DecryptLanguageFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return null;

                byte[] fileBytes = File.ReadAllBytes(filePath);
                if (fileBytes.Length <= TxaMagic.Length) return null;

                // Validate Magic Header
                for (int i = 0; i < TxaMagic.Length; i++)
                {
                    if (fileBytes[i] != TxaMagic[i]) return null;
                }

                int cipherLen = fileBytes.Length - TxaMagic.Length;
                byte[] cipherBytes = new byte[cipherLen];
                Buffer.BlockCopy(fileBytes, TxaMagic.Length, cipherBytes, 0, cipherLen);

                using var aes = Aes.Create();
                aes.Key = AesKey;
                aes.IV = AesIv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var decryptor = aes.CreateDecryptor();
                byte[] decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

                string json = Encoding.UTF8.GetString(decryptedBytes);
                return JsonSerializer.Deserialize<TxaLanguagePackage>(json);
            }
            catch
            {
                // Return null on decryption/format failure
                return null;
            }
        }

        private static void GenerateDefaultPackages()
        {
            string viPath = Path.Combine(LanguagesDirectory, "vi-VN.txal");
            if (!File.Exists(viPath))
            {
                var vi = CreateVietnamesePackage();
                File.WriteAllBytes(viPath, EncryptLanguagePackage(vi));
            }

            string enPath = Path.Combine(LanguagesDirectory, "en-US.txal");
            if (!File.Exists(enPath))
            {
                var en = CreateEnglishPackage();
                File.WriteAllBytes(enPath, EncryptLanguagePackage(en));
            }
        }

        private static TxaLanguagePackage CreateVietnamesePackage()
        {
            return new TxaLanguagePackage
            {
                lang_name = "Tiếng Việt",
                lang_code = "vi-VN",
                author = "TXAVL",
                txa_key = new Dictionary<string, string>
                {
                    { "t_app_title", "Steam Route Fixer & Traffic Inspector" },
                    { "t_tab_steam", "🎮 STEAM ROUTE FIXER" },
                    { "t_tab_traffic", "🌐 HTTP/HTTPS TRAFFIC & PROCESS INSPECTOR" },
                    { "t_menu_file", "_Tệp" },
                    { "t_menu_steam_dir", "🎮 Mở thư mục cài đặt Steam" },
                    { "t_menu_hosts_file", "📝 Mở file hosts Windows (Notepad)" },
                    { "t_menu_export_logs", "💾 Xuất nhật ký Logs ra file..." },
                    { "t_menu_exit", "❌ Thoát" },
                    { "t_menu_edit", "_Chỉnh sửa" },
                    { "t_menu_copy_domains", "📋 Copy danh sách Domain" },
                    { "t_menu_copy_logs", "📋 Copy toàn bộ Console Logs" },
                    { "t_menu_clear_log", "🧹 Xóa sạch Console Log" },
                    { "t_menu_refresh", "🔄 Làm mới dữ liệu" },
                    { "t_menu_view", "_Giao diện" },
                    { "t_menu_tools", "_Công cụ" },
                    { "t_menu_auto_fix", "⚡ 1-Click Auto Fix Steam" },
                    { "t_menu_diagnose", "🔍 Chẩn đoán kết nối ngay" },
                    { "t_menu_revert", "🧹 Khôi phục file Hosts gốc" },
                    { "t_menu_flush_dns", "🔄 Flush DNS Cache Windows" },
                    { "t_menu_appdata", "📂 Mở thư mục dữ liệu AppData (%LocalAppData%)" },
                    { "t_menu_settings", "⚙️ Cài đặt hệ thống..." },
                    { "t_menu_help", "_Trợ giúp" },
                    { "t_menu_check_update", "🔄 Kiểm tra cập nhật (Check Update...)" },
                    { "t_menu_about", "ℹ️ Giới thiệu (About SteamRouteFixer...)" },
                    { "t_btn_autofix", "⚡ 1-Click Auto Fix Steam" },
                    { "t_btn_diagnose", "🔍 Kiểm Tra (Diagnose)" },
                    { "t_btn_revert_hosts", "🧹 Khôi Phục Hosts" },
                    { "t_btn_flush_dns", "🔄 Flush DNS" },
                    { "t_btn_open_steam", "🚀 Mở Steam" },
                    { "t_filter_status", "Lọc trạng thái: " },
                    { "t_filter_all", "Tất cả" },
                    { "t_filter_open", "Open" },
                    { "t_filter_poisoned", "Poisoned" },
                    { "t_filter_blocked", "Blocked" },
                    { "t_sentinel_title", "BẢO VỆ STEAM SENTINEL:" },
                    { "t_sentinel_installed", "Steam đã cài đặt" },
                    { "t_sentinel_running", "Đang chạy" },
                    { "t_sentinel_stopped", "Đang tắt" },
                    { "t_progress_ready", "Sẵn sàng kiểm tra và sửa lỗi kết nối Steam." },
                    { "t_eta_ready", "ETA: Sẵn sàng" },
                    { "t_col_domain", "Tên Miền (Steam Hostname)" },
                    { "t_col_dns_ip", "IP Mạng Hiện Tại" },
                    { "t_col_clean_ip", "IP Sạch (Tối Ưu)" },
                    { "t_col_latency", "Độ Trễ" },
                    { "t_col_status", "Trạng Thái" },
                    { "t_col_action", "Hành Động" },
                    { "t_log_console", "NHẬT KÝ HOẠT ĐỘNG THỜI GIAN THỰC (LIVE CONSOLE):" },
                    { "t_filter_app", "📱 LỌC THEO APP: " },
                    { "t_btn_scan_app", "🔄 Quét App" },
                    { "t_stat_requests", "📊 Tổng Requests" },
                    { "t_stat_download", "📥 Đã Tải Về (Download)" },
                    { "t_stat_upload", "📤 Đã Tải Lên (Upload)" },
                    { "t_stat_speed", "⚡ Tốc Độ Mạng Hiện Tại" },
                    { "t_btn_pause", "⏸ Tạm Dừng" },
                    { "t_btn_resume", "▶ Tiếp Tục" },
                    { "t_btn_clear_table", "🧹 Xóa Bảng" },
                    { "t_tip_tab2", "💡 Nhấp đúp vào dòng Request để mở Modal & Copy Response" },
                    { "t_lang_switched_title", "Đổi Ngôn Ngữ Thành Công" },
                    { "t_lang_switched_msg", "Đã nạp và áp dụng thành công gói ngôn ngữ: {0} ({1})" },
                    { "t_star_github", "⭐ Star on GitHub" }
                }
            };
        }

        public static TxaLanguagePackage CreateEnglishPackage()
        {
            return new TxaLanguagePackage
            {
                lang_name = "English",
                lang_code = "en-US",
                author = "TXAVL",
                txa_key = new Dictionary<string, string>
                {
                    { "t_app_title", "Steam Route Fixer & Traffic Inspector" },
                    { "t_tab_steam", "🎮 STEAM ROUTE FIXER" },
                    { "t_tab_traffic", "🌐 HTTP/HTTPS TRAFFIC & PROCESS INSPECTOR" },
                    { "t_menu_file", "_File" },
                    { "t_menu_steam_dir", "🎮 Open Steam Installation Directory" },
                    { "t_menu_hosts_file", "📝 Open Windows hosts File (Notepad)" },
                    { "t_menu_export_logs", "💾 Export Logs to File..." },
                    { "t_menu_exit", "❌ Exit" },
                    { "t_menu_edit", "_Edit" },
                    { "t_menu_copy_domains", "📋 Copy Domain List" },
                    { "t_menu_copy_logs", "📋 Copy Console Logs" },
                    { "t_menu_clear_log", "🧹 Clear Console Log" },
                    { "t_menu_refresh", "🔄 Refresh Data" },
                    { "t_menu_view", "_View" },
                    { "t_menu_tools", "_Tools" },
                    { "t_menu_auto_fix", "⚡ 1-Click Auto Fix Steam" },
                    { "t_menu_diagnose", "🔍 Diagnose Connections Now" },
                    { "t_menu_revert", "🧹 Revert Original Hosts File" },
                    { "t_menu_flush_dns", "🔄 Flush Windows DNS Cache" },
                    { "t_menu_appdata", "📂 Open AppData Folder (%LocalAppData%)" },
                    { "t_menu_settings", "⚙️ System Settings..." },
                    { "t_menu_help", "_Help" },
                    { "t_menu_check_update", "🔄 Check for Updates..." },
                    { "t_menu_about", "ℹ️ About SteamRouteFixer..." },
                    { "t_btn_autofix", "⚡ 1-Click Auto Fix Steam" },
                    { "t_btn_diagnose", "🔍 Diagnose Connections" },
                    { "t_btn_revert_hosts", "🧹 Revert Hosts" },
                    { "t_btn_flush_dns", "🔄 Flush DNS" },
                    { "t_btn_open_steam", "🚀 Launch Steam" },
                    { "t_filter_status", "Filter status: " },
                    { "t_filter_all", "All" },
                    { "t_filter_open", "Open" },
                    { "t_filter_poisoned", "Poisoned" },
                    { "t_filter_blocked", "Blocked" },
                    { "t_sentinel_title", "STEAM SENTINEL PROTECTION:" },
                    { "t_sentinel_installed", "Steam Installed" },
                    { "t_sentinel_running", "Running" },
                    { "t_sentinel_stopped", "Stopped" },
                    { "t_progress_ready", "Ready to diagnose and fix Steam routing issues." },
                    { "t_eta_ready", "ETA: Ready" },
                    { "t_col_domain", "Domain (Steam Hostname)" },
                    { "t_col_dns_ip", "Current ISP IP" },
                    { "t_col_clean_ip", "Clean IP (Optimized)" },
                    { "t_col_latency", "Latency" },
                    { "t_col_status", "Status" },
                    { "t_col_action", "Action" },
                    { "t_log_console", "REAL-TIME ACTIVITY LOG (LIVE CONSOLE):" },
                    { "t_filter_app", "📱 FILTER BY APP: " },
                    { "t_btn_scan_app", "🔄 Scan Apps" },
                    { "t_stat_requests", "📊 Total Requests" },
                    { "t_stat_download", "📥 Downloaded" },
                    { "t_stat_upload", "📤 Uploaded" },
                    { "t_stat_speed", "⚡ Current Network Speed" },
                    { "t_btn_pause", "⏸ Pause" },
                    { "t_btn_resume", "▶ Resume" },
                    { "t_btn_clear_table", "🧹 Clear Table" },
                    { "t_tip_tab2", "💡 Double-click any Request row to open Detail Modal & Copy Response" },
                    { "t_lang_switched_title", "Language Changed Successfully" },
                    { "t_lang_switched_msg", "Successfully loaded and applied language package: {0} ({1})" },
                    { "t_star_github", "⭐ Star on GitHub" }
                }
            };
        }

        public static Dictionary<string, string> GetDefaultEnglishDictionary()
        {
            return CreateEnglishPackage().txa_key;
        }

        public static void RegisterFileAssociation()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return;

                // HKCU\Software\Classes\.txal
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.txal"))
                {
                    key.SetValue("", "TxaLanguagePackageFile");
                }

                // Legacy HKCU\Software\Classes\.txa
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.txa"))
                {
                    key.SetValue("", "TxaLanguagePackageFile");
                }

                // HKCU\Software\Classes\TxaLanguagePackageFile
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\TxaLanguagePackageFile"))
                {
                    key.SetValue("", "TXA Language Package File");
                    using (var iconKey = key.CreateSubKey("DefaultIcon"))
                    {
                        iconKey.SetValue("", $"\"{exePath}\",0");
                    }
                    using (var shellKey = key.CreateSubKey(@"shell\open\command"))
                    {
                        shellKey.SetValue("", $"\"{exePath}\" \"%1\"");
                    }
                }
            }
            catch { }
        }

        public static void UnregisterFileAssociation()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\.txal", false);
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\.txa", false);
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\TxaLanguagePackageFile", false);
            }
            catch { }
        }
    }
}
