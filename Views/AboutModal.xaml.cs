using System.Diagnostics;
using System.Windows;

namespace SteamRouteFixer.Views
{
    public partial class AboutModal : Window
    {
        public AboutModal()
        {
            InitializeComponent();
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
