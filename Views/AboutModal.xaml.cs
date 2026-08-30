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
            LoadLanguageInfo();
        }

        private void LoadLanguageInfo()
        {
            var cur = TxaLanguageManager.CurrentLanguage;
            string langName = !string.IsNullOrWhiteSpace(cur.lang_name) ? cur.lang_name : "Tiếng Việt";
            string author = !string.IsNullOrWhiteSpace(cur.author) ? cur.author : "TXAVL";

            TxtLangVersionName.Text = $"Phiên bản {langName}";
            TxtLangTranslatorAuthor.Text = $"Dịch bởi {author}";
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
