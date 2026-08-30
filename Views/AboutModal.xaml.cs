using System.Diagnostics;
using System.Windows;
using SteamRouteFixer.Services.Common;

namespace SteamRouteFixer.Views
{
    public partial class AboutModal : Window
    {
        public AboutModal()
        {
            InitializeComponent();
            ApplyLanguageTranslations();
            TxaLanguageManager.OnLanguageChanged += ApplyLanguageTranslations;
            Closed += (s, e) => TxaLanguageManager.OnLanguageChanged -= ApplyLanguageTranslations;
        }

        private void ApplyLanguageTranslations()
        {
            Title = TxaLanguageManager.GetString("t_about_title", "Giới thiệu - Steam Route Fixer");
            if (TxtAboutSubtitle != null) TxtAboutSubtitle.Text = TxaLanguageManager.GetString("t_about_subtitle", "Tool Steam Route Fixer & HTTP/HTTPS Traffic Inspector");
            if (TxtAboutDev != null) TxtAboutDev.Text = TxaLanguageManager.GetString("t_about_dev", "Phát triển bởi TXA Studio • Version 1.1 (Build 2026.08)");
            if (TxtAboutAppHeader != null) TxtAboutAppHeader.Text = TxaLanguageManager.GetString("t_about_app_header", "🎮 Về ứng dụng Steam Route Fixer");
            if (TxtAboutAppDesc != null) TxtAboutAppDesc.Text = TxaLanguageManager.GetString("t_about_app_desc", "Ứng dụng chuyên nghiệp giúp game thủ Việt Nam tự động quét, chẩn đoán và khắc phục triệt để tình trạng lỗi kết nối mạng Steam (Steam Store, Community, Friends, Cloud Sync) bằng kỹ thuật tối ưu hóa định tuyến IP sạch chuẩn xác, hoàn toàn không cần cài đặt VPN hay phần mềm bên thứ 3.");
            if (TxtAboutFeaturesHeader != null) TxtAboutFeaturesHeader.Text = TxaLanguageManager.GetString("t_about_features_header", "⚡ Các tính năng cốt lõi");
            if (TxtAboutF1 != null) TxtAboutF1.Text = TxaLanguageManager.GetString("t_about_f1", "• 1-Click Auto Fix: Tự động phát hiện lỗi và ghim IP sạch vào hosts.");
            if (TxtAboutF2 != null) TxtAboutF2.Text = TxaLanguageManager.GetString("t_about_f2", "• Giám sát lưu lượng HTTP/HTTPS & TCP Socket theo thời gian thực.");
            if (TxtAboutF3 != null) TxtAboutF3.Text = TxaLanguageManager.GetString("t_about_f3", "• Hệ thống bảo vệ Steam Sentinel & Tự động sao lưu file hosts.");
            if (TxtAboutF4 != null) TxtAboutF4.Text = TxaLanguageManager.GetString("t_about_f4", "• Giao diện Microsoft WinUI 3 Fluent Dark sắc nét, độ tương phản cao.");
            if (TxtAboutTechHeader != null) TxtAboutTechHeader.Text = TxaLanguageManager.GetString("t_about_tech_header", "🛠️ Thông tin kỹ thuật & Mã nguồn");
            if (TxtAboutTechBody != null) TxtAboutTechBody.Text = TxaLanguageManager.GetString("t_about_tech_body", "Framework: .NET 10 (Desktop Runtime) • Windows Native Win32 API\r\nRepository: https://github.com/TXAVL/SteamRouteFixer\r\nBản quyền © 2026 TXA Studio. All rights reserved.");
            if (TxtStarGithub != null) TxtStarGithub.Text = TxaLanguageManager.GetString("t_star_github", "⭐ Star on GitHub");
            if (BtnClose != null) BtnClose.Content = TxaLanguageManager.GetString("t_btn_close", "Đóng");

            var cur = TxaLanguageManager.CurrentLanguage;
            string langName = !string.IsNullOrWhiteSpace(cur.lang_name) ? cur.lang_name : "Tiếng Việt";
            string author = !string.IsNullOrWhiteSpace(cur.author) ? cur.author : "TXAVL";
            string prefixVer = TxaLanguageManager.GetString("t_about_lang_ver_prefix", "Phiên bản ");
            string prefixBy = TxaLanguageManager.GetString("t_about_translator_prefix", "Dịch bởi ");

            if (TxtLangVersionName != null) TxtLangVersionName.Text = $"{prefixVer}{langName}";
            if (TxtLangTranslatorAuthor != null) TxtLangTranslatorAuthor.Text = $"{prefixBy}{author}";
        }

        private void BtnGithub_Click(object sender, RoutedEventArgs e)
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

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
