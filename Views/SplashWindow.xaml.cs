using System.Windows;

namespace SteamRouteFixer.Views
{
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();
        }

        public void SetStatus(string message)
        {
            Dispatcher.Invoke(() =>
            {
                TxtStatus.Text = message;
            });
        }
    }
}
