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
                    TxaMessageBox.Show(
                        "File ngôn ngữ .txal không hợp lệ hoặc đã bị chỉnh sửa/hư hại.",
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

                    TxaMessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Information);
                }

                return true;
            }
            catch (Exception ex)
            {
                TxaMessageBox.Show($"Lỗi nạp file .txal: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    // Global & App
                    { "t_app_title", "Steam Route Fixer & Traffic Inspector" },
                    { "t_tab_steam", "🎮 STEAM ROUTE FIXER" },
                    { "t_tab_traffic", "🌐 HTTP/HTTPS TRAFFIC & PROCESS INSPECTOR" },
                    { "t_admin_ok", "🛡️ Administrator" },
                    { "t_admin_needed", "⚠️ Cần Quyền Admin" },
                    { "t_btn_settings", "⚙️ Cài Đặt" },
                    { "t_star_github", "⭐ Star on GitHub" },
                    { "t_btn_close", "Đóng" },
                    { "t_btn_save", "Lưu Cài Đặt" },

                    // Top Menu
                    { "t_menu_file", "_File" },
                    { "t_menu_steam_dir", "🎮 Mở thư mục cài đặt Steam" },
                    { "t_menu_hosts_file", "📝 Mở file hosts Windows (Notepad)" },
                    { "t_menu_export_logs", "💾 Xuất nhật ký Logs ra file..." },
                    { "t_menu_exit", "❌ Thoát" },
                    { "t_menu_edit", "_Edit" },
                    { "t_menu_copy_domains", "📋 Copy danh sách Domain" },
                    { "t_menu_copy_logs", "📋 Copy toàn bộ Console Logs" },
                    { "t_menu_clear_log", "🧹 Xóa sạch Console Log" },
                    { "t_menu_refresh", "🔄 Làm mới dữ liệu" },
                    { "t_menu_tools", "_Tools" },
                    { "t_menu_auto_fix", "⚡ 1-Click Auto Fix Steam" },
                    { "t_menu_diagnose", "🔍 Chẩn đoán kết nối ngay" },
                    { "t_menu_revert", "🧹 Khôi phục file Hosts gốc" },
                    { "t_menu_flush_dns", "🔄 Flush DNS Cache Windows" },
                    { "t_menu_appdata", "📂 Mở thư mục dữ liệu AppData (%LocalAppData%)" },
                    { "t_menu_settings", "⚙️ Cài đặt hệ thống..." },
                    { "t_menu_help", "_Help" },
                    { "t_menu_check_update", "🔄 Kiểm tra cập nhật (Check Update...)" },
                    { "t_menu_about", "ℹ️ Giới thiệu (About SteamRouteFixer...)" },

                    // Settings Modal Additional Keys
                    { "t_theme_winui3", "WinUI 3 (Windows 11 Fluent Mica)" },
                    { "t_theme_steam", "Steam Dark (Cyberpunk Glow)" },
                    { "t_theme_vscode", "VS Code Studio Dark" },
                    { "t_backup_confirm_restore_fmt", "Bạn có chắc chắn muốn khôi phục file hosts từ bản sao lưu:\n{0}?" },
                    { "t_backup_restore_title", "Xác nhận khôi phục" },
                    { "t_backup_restore_success", "Khôi phục file hosts thành công và đã Flush DNS!" },
                    { "t_backup_restore_error", "Không thể khôi phục file hosts. Vui lòng chạy phần mềm với quyền Administrator." },
                    { "t_backup_select_warning", "Vui lòng chọn 1 bản sao lưu trong danh sách!" },

                    // Tab 1 Steam Sentinel & Warnings
                    { "t_sentinel_title", "BẢO VỆ STEAM SENTINEL: " },
                    { "t_sentinel_installed", "Steam đã cài đặt" },
                    { "t_sentinel_running", "Đang chạy" },
                    { "t_sentinel_stopped", "Đang tắt" },
                    { "t_sentinel_detecting", "Đang phát hiện cài đặt và tiến trình Steam..." },
                    { "t_sentinel_path_prefix", "Đường dẫn Steam: " },
                    { "t_sentinel_path_not_found", "Chưa phát hiện đường dẫn cài đặt Steam." },
                    { "t_steam_running_title", "PHÁT HIỆN TIẾN TRÌNH STEAM ĐANG CHẠY - CÁC TÁC VỤ SỬA ROUTE ĐÃ ĐƯỢC TẠM KHÓA" },
                    { "t_steam_running_desc", "Vui lòng nhấn 'Đóng Steam Ngay' để giải phóng Socket và ghi file hosts an toàn trước khi thao tác." },
                    { "t_btn_close_steam", "🛑 Đóng Steam Ngay" },
                    { "t_btn_open_steam", "🚀 Mở Steam" },

                    // Tab 1 Action Buttons & Filters
                    { "t_btn_autofix", "⚡ 1-Click Auto Fix Steam" },
                    { "t_btn_diagnose", "🔍 Kiểm Tra (Diagnose)" },
                    { "t_btn_revert_hosts", "🧹 Khôi Phục Hosts" },
                    { "t_btn_flush_dns", "🔄 Flush DNS" },
                    { "t_filter_status", "Lọc trạng thái: " },
                    { "t_filter_all", "Tất cả" },
                    { "t_filter_open", "Open" },
                    { "t_filter_poisoned", "Poisoned" },
                    { "t_filter_blocked", "Blocked" },

                    // Tab 1 Progress & Grid
                    { "t_progress_ready", "Sẵn sàng kiểm tra và sửa lỗi kết nối Steam." },
                    { "t_eta_ready", "ETA: Sẵn sàng" },
                    { "t_col_purpose", "Mục Đích" },
                    { "t_col_domain", "Tên Miền (Steam Hostname)" },
                    { "t_col_dns_ip", "IP Mạng Hiện Tại" },
                    { "t_col_clean_ip", "IP Sạch (Tối Ưu)" },
                    { "t_col_latency", "Độ Trễ" },
                    { "t_col_status", "Trạng Thái" },
                    { "t_log_console", "📜 NHẬT KÝ HOẠT ĐỘNG (REAL-TIME LOGS)" },
                    { "t_btn_copy_logs", "📋 Copy Logs" },
                    { "t_btn_clear_log_short", "🧹 Clear" },

                    // Tab 2 Traffic Inspector
                    { "t_filter_app", "📱 LỌC THEO APP: " },
                    { "t_btn_scan_app", "🔄 Quét App" },
                    { "t_stat_requests", "📊 Tổng Requests" },
                    { "t_stat_download", "📥 Đã Tải Về (Download)" },
                    { "t_stat_upload", "📤 Đã Tải Lên (Upload)" },
                    { "t_stat_speed", "⚡ Tốc Độ Mạng Hiện Tại" },
                    { "t_btn_pause", "⏸ Tạm Dừng" },
                    { "t_btn_resume", "▶ Tiếp Tục" },
                    { "t_btn_clear_table", "🧹 Xóa Bảng" },
                    { "t_col_time", "Thời Gian" },
                    { "t_col_app", "Ứng Dụng" },
                    { "t_col_proto", "Giao Thức" },
                    { "t_col_host", "Host / Endpoint URL" },
                    { "t_col_size", "Dung Lượng (↓/↑)" },
                    { "t_col_block_action", "Trạng Thái Chặn" },
                    { "t_search_placeholder", "🔍 Tìm kiếm URL, API, Domain, IP..." },

                    // Bottom Tips
                    { "t_tip_tab1", "💡 Mẹo: Nhấn '1-Click Auto Fix' để tự động ghim IP sạch & sửa lỗi Steam không cần VPN" },
                    { "t_tip_tab2", "💡 Mẹo: Nhấp đúp vào bất kỳ dòng Request nào để mở Modal chi tiết và Copy Response" },

                    // Confirm Exit
                    { "t_confirm_exit_title", "Xác nhận đóng ứng dụng" },
                    { "t_confirm_exit_msg", "Bạn có chắc chắn muốn thoát khỏi Steam Route Fixer?" },

                    // Switch Language Alert
                    { "t_lang_switched_title", "Đổi Ngôn Ngữ Thành Công" },
                    { "t_lang_switched_msg", "Đã nạp và áp dụng thành công gói ngôn ngữ: {0} ({1})" },

                    // Settings Modal
                    { "t_settings_title", "Cài Đặt Hệ Thống & Giao Diện" },
                    { "t_theme_header", "🎨 CHỦ ĐỀ GIAO DIỆN (THEME)" },
                    { "t_theme_desc", "Tùy biến phong cách hiển thị WinUI 3 Fluent, Steam Dark Gaming hoặc VS Code Studio Dark:" },
                    { "t_lang_header", "🌐 NGÔN NGỮ ỨNG DỤNG (TXA LANGUAGE)" },
                    { "t_lang_desc", "Quét tự động các gói ngôn ngữ (.txal) trong %LocalAppData%\\SteamRouteFixer\\languages\\:" },
                    { "t_btn_import_txa", "📂 Nạp File .txal" },
                    { "t_btn_open_lang_dir", "📁 Mở Thư Mục Lang" },
                    { "t_btn_translate_app", "🌐 Tự Tạo Bản Dịch Mới (Translate App...)" },
                    { "t_steam_path_header", "🎮 ĐƯỜNG DẪN STEAM TÙY CHỌN" },
                    { "t_steam_path_desc", "Nếu bạn cài Steam ở ổ đĩa khác và công cụ chưa tự nhận diện, hãy chọn file steam.exe:" },
                    { "t_btn_browse", "Duyệt File..." },
                    { "t_btn_auto_detect", "Tự Nhận Diện" },
                    { "t_hosts_backup_header", "🛡️ SAO LƯU FILE HOSTS HỆ THỐNG" },
                    { "t_hosts_backup_desc", "Danh sách các bản sao lưu tự động tạo trước mỗi lần sửa đổi:" },
                    { "t_btn_restore_backup", "Khôi Phục Bản Này" },
                    { "t_btn_create_backup", "Tạo Sao Lưu Mới" },

                    // About Modal
                    { "t_about_title", "Giới thiệu - Steam Route Fixer" },
                    { "t_about_subtitle", "Tool Steam Route Fixer & HTTP/HTTPS Traffic Inspector" },
                    { "t_about_dev", "Phát triển bởi TXA Studio • Version 1.0 (Build 2026.08)" },
                    { "t_about_app_header", "🎮 Về ứng dụng Steam Route Fixer" },
                    { "t_about_app_desc", "Ứng dụng chuyên nghiệp giúp game thủ Việt Nam tự động quét, chẩn đoán và khắc phục triệt để tình trạng lỗi kết nối mạng Steam (Steam Store, Community, Friends, Cloud Sync) bằng kỹ thuật tối ưu hóa định tuyến IP sạch chuẩn xác, hoàn toàn không cần cài đặt VPN hay phần mềm bên thứ 3." },
                    { "t_about_features_header", "⚡ Các tính năng cốt lõi" },
                    { "t_about_f1", "• 1-Click Auto Fix: Tự động phát hiện lỗi và ghim IP sạch vào hosts." },
                    { "t_about_f2", "• Giám sát lưu lượng HTTP/HTTPS & TCP Socket theo thời gian thực." },
                    { "t_about_f3", "• Hệ thống bảo vệ Steam Sentinel & Tự động sao lưu file hosts." },
                    { "t_about_f4", "• Giao diện Microsoft WinUI 3 Fluent Dark sắc nét, độ tương phản cao." },
                    { "t_about_tech_header", "🛠️ Thông tin kỹ thuật & Mã nguồn" },
                    { "t_about_tech_body", "Framework: .NET 10 (Desktop Runtime) • Windows Native Win32 API\r\nRepository: https://github.com/TXAVL/SteamRouteFixer\r\nBản quyền © 2026 TXA Studio. All rights reserved." },
                    { "t_about_lang_ver_prefix", "Phiên bản " },
                    { "t_about_translator_prefix", "Dịch bởi " },

                    // Request Detail Modal
                    { "t_req_detail_title", "Chi Tiết HTTP Request & Response" },
                    { "t_req_detail_header", "🔍 THÔNG TIN CHI TIẾT REQUEST & RESPONSE" },
                    { "t_meta_process", "📱 Tiến trình: " },
                    { "t_meta_remote", "🌐 Remote: " },
                    { "t_meta_latency", "⏱ Độ trễ: " },
                    { "t_meta_req_size", "📦 Request Size: " },
                    { "t_meta_resp_size", "📥 Response Size: " },
                    { "t_tab_response", "📤 RESPONSE (PHẢN HỒI)" },
                    { "t_resp_sub", "Response Headers & Payload:" },
                    { "t_tab_request", "📥 REQUEST (YÊU CẦU)" },
                    { "t_req_sub", "Request Headers & Body:" },
                    { "t_btn_copy_url", "📋 Copy URL" },
                    { "t_btn_copy_req", "📋 Copy Request" },
                    { "t_btn_copy_resp", "✨ 📋 COPY RESPONSE (BODY)" },
                    { "t_toast_copy_url", "📋 Đã sao chép liên kết URL vào Clipboard!" },
                    { "t_toast_copy_req", "📥 Đã sao chép nội dung Request vào Clipboard!" },
                    { "t_toast_copy_resp", "✨ 📤 Đã sao chép toàn bộ Response Body vào Clipboard!" },

                    // Translation Modal
                    { "t_trans_title", "Biên dịch Ngôn ngữ - TXA Language Translator" },
                    { "t_trans_header", "TRÌNH BIÊN DỊCH NGÔN NGỮ (TXA TRANSLATOR)" },
                    { "t_trans_sub", "Biên dịch toàn bộ giao diện từ Tiếng Anh chuẩn sang ngôn ngữ mong muốn. Tiến độ nháp tự động lưu liên tục." },
                    { "t_trans_target_lbl", "🎯 CHỌN NGÔN NGỮ ĐÍCH BIÊN DỊCH:" },
                    { "t_trans_author_lbl", "✍️ TÊN / NICKNAME TÁC GIẢ BẢN DỊCH:" },
                    { "t_trans_col_source", "🔤 VĂN BẢN TIẾNG ANH GỐC (SOURCE EN-US)" },
                    { "t_trans_col_target", "✏️ BẢN DỊCH NGÔN NGỮ ĐÍCH CỦA BẠN (TARGET TRANSLATION)" },
                    { "t_trans_btn_save", "💾 Lưu & Áp Dụng (.txal)" },
                    { "t_trans_btn_submit", "🚀 Gửi Lên GitHub (100%)" },
                    { "t_draft_saved", "Đã lưu nháp tự động" },
                    { "t_draft_saving", "Đang lưu nháp..." },
                    { "t_draft_loaded", "Đã nạp bản nháp tự động" },
                    { "t_draft_new", "Bản dịch mới" },
                    { "t_trans_progress_fmt", "Tiến độ dịch: {0} / {1} chuỗi ({2:F1}%)" },
                    { "t_trans_select_lang_warning", "Vui lòng chọn một ngôn ngữ đích để lưu." },
                    { "t_trans_need_translation_warning", "Vui lòng dịch ít nhất một vài câu trước khi lưu & áp dụng." },
                    { "t_trans_save_success_fmt", "Đã lưu thành công gói ngôn ngữ {0} ({1}) và áp dụng ngay lập tức vào Steam Route Fixer!" },
                    { "t_trans_save_success_title", "Hoàn tất biên dịch" },
                    { "t_trans_save_error_fmt", "Lỗi khi lưu gói ngôn ngữ: {0}" },
                    { "t_trans_browser_error_fmt", "Không thể mở trình duyệt: {0}" },
                    { "t_trans_var_error_msg", "Phát hiện {0} chuỗi dịch chưa nhập đúng hoặc còn thiếu các biến định dạng:\n\n{1}\n\nVui lòng kiểm tra và điền đầy đủ các biến trước khi lưu để tránh gây lỗi hiển thị trong ứng dụng!" },
                    { "t_trans_var_error_title", "Cảnh Báo Biến Định Dạng" },

                    // Settings Modal
                    { "t_settings_title", "⚙️ THIẾT LẬP CẤU HÌNH ỨNG DỤNG" },
                    { "t_theme_header", "🎨 GIAO DIỆN & PHONG CÁCH (THEME)" },
                    { "t_theme_desc", "Chọn giao diện hiển thị phù hợp với sở thích của bạn:" },
                    { "t_theme_winui3", "WinUI 3 (Windows 11 Fluent Mica)" },
                    { "t_theme_steam", "Steam Dark (Cyberpunk Glow)" },
                    { "t_theme_vscode", "VS Code Studio Dark" },
                    { "t_lang_header", "🌐 NGÔN NGỮ ỨNG DỤNG (TXA LANGUAGE)" },
                    { "t_lang_desc", "Quét tự động các gói ngôn ngữ (.txal) trong %LocalAppData%\\SteamRouteFixer\\languages\\:" },
                    { "t_btn_import_txa", "📂 Nạp File .txal" },
                    { "t_btn_open_lang_dir", "📁 Mở Thư Mục Lang" },
                    { "t_btn_translate_app", "🌐 Tự Tạo Bản Dịch Mới (Translate App...)" },
                    { "t_steam_path_header", "🎮 ĐƯỜNG DẪN STEAM TÙY CHỌN" },
                    { "t_steam_path_desc", "Nếu bạn cài Steam ở ổ đĩa khác và công cụ chưa tự nhận diện, hãy chọn file steam.exe:" },
                    { "t_btn_browse", "📁 Chọn file steam.exe" },
                    { "t_hosts_backup_header", "🛡️ SAO LƯU & KHÔI PHỤC FILE HOSTS" },
                    { "t_hosts_backup_desc", "Danh sách các bản sao lưu tự động trong %LocalAppData%\\SteamRouteFixer\\backups\\:" },
                    { "t_btn_restore_backup", "🔄 Khôi Phục Bản Chọn" },
                    { "t_backup_select_warning", "Vui lòng chọn 1 bản sao lưu trong danh sách!" },
                    { "t_backup_confirm_restore_fmt", "Bạn có chắc chắn muốn khôi phục file hosts từ bản sao lưu:\n{0}?" },
                    { "t_backup_restore_title", "Xác nhận khôi phục" },
                    { "t_backup_restore_success", "Khôi phục file hosts thành công và đã Flush DNS!" },
                    { "t_backup_restore_error", "Không thể khôi phục file hosts. Vui lòng chạy phần mềm với quyền Administrator." },
                    { "t_btn_save", "💾 Lưu Cài Đặt" },
                    { "t_btn_close", "Đóng" },
                    { "t_dialog_import_txa_title", "Chọn gói ngôn ngữ TXA Language (*.txal, *.txa)" },
                    { "t_dialog_browse_steam_title", "Chọn file steam.exe" }
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
                    // Global & App
                    { "t_app_title", "Steam Route Fixer & Traffic Inspector" },
                    { "t_tab_steam", "🎮 STEAM ROUTE FIXER" },
                    { "t_tab_traffic", "🌐 HTTP/HTTPS TRAFFIC & PROCESS INSPECTOR" },
                    { "t_admin_ok", "🛡️ Administrator" },
                    { "t_admin_needed", "⚠️ Admin Rights Required" },
                    { "t_btn_settings", "⚙️ Settings" },
                    { "t_star_github", "⭐ Star on GitHub" },
                    { "t_btn_close", "Close" },
                    { "t_btn_save", "Save Settings" },

                    // Top Menu
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

                    // Settings Modal Additional Keys
                    { "t_theme_winui3", "WinUI 3 (Windows 11 Fluent Mica)" },
                    { "t_theme_steam", "Steam Dark (Cyberpunk Glow)" },
                    { "t_theme_vscode", "VS Code Studio Dark" },
                    { "t_backup_confirm_restore_fmt", "Are you sure you want to restore the hosts file from backup:\n{0}?" },
                    { "t_backup_restore_title", "Confirm Restore" },
                    { "t_backup_restore_success", "Hosts file restored successfully and DNS flushed!" },
                    { "t_backup_restore_error", "Cannot restore hosts file. Please run with Administrator privileges." },
                    { "t_backup_select_warning", "Please select a backup from the list!" },

                    // Tab 1 Steam Sentinel & Warnings
                    { "t_sentinel_title", "STEAM SENTINEL PROTECTION: " },
                    { "t_sentinel_installed", "Steam Installed" },
                    { "t_sentinel_running", "Running" },
                    { "t_sentinel_stopped", "Stopped" },
                    { "t_sentinel_detecting", "Detecting Steam installation and processes..." },
                    { "t_sentinel_path_prefix", "Steam Path: " },
                    { "t_sentinel_path_not_found", "Steam installation directory not found." },
                    { "t_steam_running_title", "STEAM PROCESS IS ACTIVE - ROUTE MODIFICATIONS TEMPORARILY LOCKED" },
                    { "t_steam_running_desc", "Please click 'Close Steam Now' to release sockets and safely update the hosts file." },
                    { "t_btn_close_steam", "🛑 Close Steam Now" },
                    { "t_btn_open_steam", "🚀 Launch Steam" },

                    // Tab 1 Action Buttons & Filters
                    { "t_btn_autofix", "⚡ 1-Click Auto Fix Steam" },
                    { "t_btn_diagnose", "🔍 Diagnose Connections" },
                    { "t_btn_revert_hosts", "🧹 Revert Hosts" },
                    { "t_btn_flush_dns", "🔄 Flush DNS" },
                    { "t_filter_status", "Filter status: " },
                    { "t_filter_all", "All" },
                    { "t_filter_open", "Open" },
                    { "t_filter_poisoned", "Poisoned" },
                    { "t_filter_blocked", "Blocked" },

                    // Tab 1 Progress & Grid
                    { "t_progress_ready", "Ready to diagnose and fix Steam routing issues." },
                    { "t_eta_ready", "ETA: Ready" },
                    { "t_col_purpose", "Purpose" },
                    { "t_col_domain", "Domain (Steam Hostname)" },
                    { "t_col_dns_ip", "Current ISP IP" },
                    { "t_col_clean_ip", "Clean IP (Optimized)" },
                    { "t_col_latency", "Latency" },
                    { "t_col_status", "Status" },
                    { "t_log_console", "📜 REAL-TIME ACTIVITY LOG (LIVE CONSOLE)" },
                    { "t_btn_copy_logs", "📋 Copy Logs" },
                    { "t_btn_clear_log_short", "🧹 Clear" },

                    // Tab 2 Traffic Inspector
                    { "t_filter_app", "📱 FILTER BY APP: " },
                    { "t_btn_scan_app", "🔄 Scan Apps" },
                    { "t_stat_requests", "📊 Total Requests" },
                    { "t_stat_download", "📥 Downloaded" },
                    { "t_stat_upload", "📤 Uploaded" },
                    { "t_stat_speed", "⚡ Current Network Speed" },
                    { "t_btn_pause", "⏸ Pause" },
                    { "t_btn_resume", "▶ Resume" },
                    { "t_btn_clear_table", "🧹 Clear Table" },
                    { "t_col_time", "Time" },
                    { "t_col_app", "Application" },
                    { "t_col_proto", "Protocol" },
                    { "t_col_host", "Host / Endpoint URL" },
                    { "t_col_size", "Size (↓/↑)" },
                    { "t_col_block_action", "Block Status" },
                    { "t_search_placeholder", "🔍 Search URL, API, Domain, IP..." },

                    // Bottom Tips
                    { "t_tip_tab1", "💡 Tip: Click '1-Click Auto Fix' to automatically pin clean IPs and fix Steam issues without a VPN" },
                    { "t_tip_tab2", "💡 Tip: Double-click any Request row to inspect full details and copy responses" },

                    // Confirm Exit
                    { "t_confirm_exit_title", "Confirm Exit" },
                    { "t_confirm_exit_msg", "Are you sure you want to close and exit Steam Route Fixer?" },

                    // Switch Language Alert
                    { "t_lang_switched_title", "Language Changed Successfully" },
                    { "t_lang_switched_msg", "Successfully loaded and applied language package: {0} ({1})" },

                    // Settings Modal
                    { "t_settings_title", "System Settings & Appearance" },
                    { "t_theme_header", "🎨 APP THEME" },
                    { "t_theme_desc", "Customize display style: WinUI 3 Fluent, Steam Dark Gaming, or VS Code Studio Dark:" },
                    { "t_lang_header", "🌐 APP LANGUAGE (TXA LANGUAGE)" },
                    { "t_lang_desc", "Automatically scans (.txal) language packages in %LocalAppData%\\SteamRouteFixer\\languages\\:" },
                    { "t_btn_import_txa", "📂 Import .txal File" },
                    { "t_btn_open_lang_dir", "📁 Open Lang Folder" },
                    { "t_btn_translate_app", "🌐 Translate App (New Language...)" },
                    { "t_steam_path_header", "🎮 CUSTOM STEAM PATH" },
                    { "t_steam_path_desc", "If Steam is installed in a custom location, locate the steam.exe binary manually:" },
                    { "t_btn_browse", "Browse File..." },
                    { "t_btn_auto_detect", "Auto Detect" },
                    { "t_hosts_backup_header", "🛡️ SYSTEM HOSTS BACKUPS" },
                    { "t_hosts_backup_desc", "List of automated backups created before each modification:" },
                    { "t_btn_restore_backup", "Restore Selected" },
                    { "t_btn_create_backup", "Create Backup" },

                    // About Modal
                    { "t_about_title", "About - Steam Route Fixer" },
                    { "t_about_subtitle", "Tool Steam Route Fixer & HTTP/HTTPS Traffic Inspector" },
                    { "t_about_dev", "Developed by TXA Studio • Version 1.0 (Build 2026.08)" },
                    { "t_about_app_header", "🎮 About Steam Route Fixer" },
                    { "t_about_app_desc", "Professional network utility designed to automatically scan, diagnose, and permanently resolve Steam connection errors (Store, Community, Market, Cloud Sync) by routing through optimized clean IPs without requiring VPNs." },
                    { "t_about_features_header", "⚡ Core Features" },
                    { "t_about_f1", "• 1-Click Auto Fix: Auto-detects blocked routes and pins clean IPs into hosts." },
                    { "t_about_f2", "• Real-time HTTP/HTTPS traffic and TCP Socket Inspector." },
                    { "t_about_f3", "• Steam Sentinel real-time process shield & automated hosts backups." },
                    { "t_about_f4", "• High-contrast Microsoft WinUI 3 Fluent Dark interface." },
                    { "t_about_tech_header", "🛠️ Technical Information & Source Code" },
                    { "t_about_tech_body", "Framework: .NET 10 (Desktop Runtime) • Windows Native Win32 API\r\nRepository: https://github.com/TXAVL/SteamRouteFixer\r\nCopyright © 2026 TXA Studio. All rights reserved." },
                    { "t_about_lang_ver_prefix", "Version: " },
                    { "t_about_translator_prefix", "Translated by " },

                    // Request Detail Modal
                    { "t_req_detail_title", "HTTP Request & Response Details" },
                    { "t_req_detail_header", "🔍 DETAILED REQUEST & RESPONSE" },
                    { "t_meta_process", "📱 Process: " },
                    { "t_meta_remote", "🌐 Remote: " },
                    { "t_meta_latency", "⏱ Latency: " },
                    { "t_meta_req_size", "📦 Request Size: " },
                    { "t_meta_resp_size", "📥 Response Size: " },
                    { "t_tab_response", "📤 RESPONSE" },
                    { "t_resp_sub", "Response Headers & Payload:" },
                    { "t_tab_request", "📥 REQUEST" },
                    { "t_req_sub", "Request Headers & Body:" },
                    { "t_btn_copy_url", "📋 Copy URL" },
                    { "t_btn_copy_req", "📋 Copy Request" },
                    { "t_btn_copy_resp", "✨ 📋 COPY RESPONSE (BODY)" },
                    { "t_toast_copy_url", "📋 URL copied to Clipboard!" },
                    { "t_toast_copy_req", "📥 Request contents copied to Clipboard!" },
                    { "t_toast_copy_resp", "✨ 📤 Response Body copied to Clipboard!" },

                    // Translation Modal
                    { "t_trans_title", "Language Translator - TXA Language Translator" },
                    { "t_trans_header", "APPLICATION LANGUAGE TRANSLATOR (TXA TRANSLATOR)" },
                    { "t_trans_sub", "Translate the entire app from standard English into any desired language. Drafts auto-save continuously." },
                    { "t_trans_target_lbl", "🎯 SELECT TARGET TRANSLATION LANGUAGE:" },
                    { "t_trans_author_lbl", "✍️ TRANSLATOR / AUTHOR NAME / NICKNAME:" },
                    { "t_trans_col_source", "🔤 ORIGINAL ENGLISH SOURCE TEXT (EN-US)" },
                    { "t_trans_col_target", "✏️ YOUR TARGET TRANSLATION TEXT" },
                    { "t_trans_btn_save", "💾 Save & Apply (.txal)" },
                    { "t_trans_btn_submit", "🚀 Submit to GitHub (100%)" },
                    { "t_draft_saved", "Draft auto-saved" },
                    { "t_draft_saving", "Saving draft..." },
                    { "t_draft_loaded", "Draft auto-loaded" },
                    { "t_draft_new", "New translation" },
                    { "t_trans_progress_fmt", "Progress: {0} / {1} strings ({2:F1}%)" },
                    { "t_trans_select_lang_warning", "Please select a target language to save." },
                    { "t_trans_need_translation_warning", "Please translate at least some strings before saving." },
                    { "t_trans_save_success_fmt", "Successfully saved language package {0} ({1}) and applied immediately!" },
                    { "t_trans_save_success_title", "Compilation Complete" },
                    { "t_trans_save_error_fmt", "Error saving language package: {0}" },
                    { "t_trans_browser_error_fmt", "Cannot open browser: {0}" },
                    { "t_trans_var_error_msg", "Detected {0} translation strings with missing or invalid format variables:\n\n{1}\n\nPlease provide all required variables before saving to prevent runtime UI errors!" },
                    { "t_trans_var_error_title", "Variable Format Warning" },

                    // Settings Modal
                    { "t_settings_title", "⚙️ APPLICATION CONFIGURATION & SETTINGS" },
                    { "t_theme_header", "🎨 THEME & APPEARANCE" },
                    { "t_theme_desc", "Customize the application visual style according to your preference:" },
                    { "t_theme_winui3", "WinUI 3 (Windows 11 Fluent Mica)" },
                    { "t_theme_steam", "Steam Dark (Cyberpunk Glow)" },
                    { "t_theme_vscode", "VS Code Studio Dark" },
                    { "t_lang_header", "🌐 APPLICATION LANGUAGE (TXA LANGUAGE)" },
                    { "t_lang_desc", "Auto-scanned language packages (.txal) in %LocalAppData%\\SteamRouteFixer\\languages\\:" },
                    { "t_btn_import_txa", "📂 Load .txal File" },
                    { "t_btn_open_lang_dir", "📁 Open Lang Folder" },
                    { "t_btn_translate_app", "🌐 Create New Translation (Translate App...)" },
                    { "t_steam_path_header", "🎮 CUSTOM STEAM INSTALLATION PATH" },
                    { "t_steam_path_desc", "If Steam is installed on another drive and not detected, locate steam.exe:" },
                    { "t_btn_browse", "📁 Browse steam.exe" },
                    { "t_hosts_backup_header", "🛡️ HOSTS FILE BACKUP & RESTORE" },
                    { "t_hosts_backup_desc", "List of automated backup snapshots in %LocalAppData%\\SteamRouteFixer\\backups\\:" },
                    { "t_btn_restore_backup", "🔄 Restore Selected" },
                    { "t_backup_select_warning", "Please select a backup from the list!" },
                    { "t_backup_confirm_restore_fmt", "Are you sure you want to restore the hosts file from backup:\n{0}?" },
                    { "t_backup_restore_title", "Confirm Restore" },
                    { "t_backup_restore_success", "Hosts file restored successfully and DNS cache flushed!" },
                    { "t_backup_restore_error", "Failed to restore hosts file. Please run with Administrator privileges." },
                    { "t_btn_save", "💾 Save Settings" },
                    { "t_btn_close", "Close" },
                    { "t_dialog_import_txa_title", "Select TXA Language Package (*.txal, *.txa)" },
                    { "t_dialog_browse_steam_title", "Select steam.exe file" }
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
