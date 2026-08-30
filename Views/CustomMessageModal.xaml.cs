using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SteamRouteFixer.Services.Common;

namespace SteamRouteFixer.Views
{
    public partial class CustomMessageModal : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        public CustomMessageModal(string message, string title = "Thông báo", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information)
        {
            InitializeComponent();
            TxtTitle.Text = string.IsNullOrWhiteSpace(title) ? TxaLanguageManager.GetString("t_dialog_notice", "Thông báo") : title;
            TxtMessage.Text = message;

            ConfigureIcon(image);
            ConfigureButtons(buttons);

            KeyDown += CustomMessageModal_KeyDown;
            MouseDown += CustomMessageModal_MouseDown;
        }

        private void ConfigureIcon(MessageBoxImage image)
        {
            switch (image)
            {
                case MessageBoxImage.Information:
                    TxtIconEmoji.Text = "ℹ️";
                    BorderIconBadge.Background = new SolidColorBrush(Color.FromArgb(40, 76, 194, 255));
                    break;
                case MessageBoxImage.Warning:
                    TxtIconEmoji.Text = "⚠️";
                    BorderIconBadge.Background = new SolidColorBrush(Color.FromArgb(40, 255, 185, 0));
                    break;
                case MessageBoxImage.Error:
                    TxtIconEmoji.Text = "❌";
                    BorderIconBadge.Background = new SolidColorBrush(Color.FromArgb(40, 255, 82, 82));
                    break;
                case MessageBoxImage.Question:
                    TxtIconEmoji.Text = "❓";
                    BorderIconBadge.Background = new SolidColorBrush(Color.FromArgb(40, 224, 64, 251));
                    break;
                default:
                    TxtIconEmoji.Text = "✨";
                    BorderIconBadge.Background = new SolidColorBrush(Color.FromArgb(40, 38, 224, 127));
                    break;
            }
        }

        private void ConfigureButtons(MessageBoxButton buttons)
        {
            BtnOk.Visibility = Visibility.Collapsed;
            BtnYes.Visibility = Visibility.Collapsed;
            BtnNo.Visibility = Visibility.Collapsed;
            BtnCancel.Visibility = Visibility.Collapsed;

            // Translated button texts
            BtnOk.Content = TxaLanguageManager.GetString("t_btn_ok", "OK");
            BtnYes.Content = TxaLanguageManager.GetString("t_btn_yes", "Đồng Ý");
            BtnNo.Content = TxaLanguageManager.GetString("t_btn_no", "Không");
            BtnCancel.Content = TxaLanguageManager.GetString("t_btn_cancel", "Hủy");

            switch (buttons)
            {
                case MessageBoxButton.OK:
                    BtnOk.Visibility = Visibility.Visible;
                    BtnOk.Focus();
                    break;
                case MessageBoxButton.OKCancel:
                    BtnOk.Visibility = Visibility.Visible;
                    BtnCancel.Visibility = Visibility.Visible;
                    BtnOk.Focus();
                    break;
                case MessageBoxButton.YesNo:
                    BtnYes.Visibility = Visibility.Visible;
                    BtnNo.Visibility = Visibility.Visible;
                    BtnYes.Focus();
                    break;
                case MessageBoxButton.YesNoCancel:
                    BtnYes.Visibility = Visibility.Visible;
                    BtnNo.Visibility = Visibility.Visible;
                    BtnCancel.Visibility = Visibility.Visible;
                    BtnYes.Focus();
                    break;
            }
        }

        private void CustomMessageModal_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                try { DragMove(); } catch { }
            }
        }

        private void CustomMessageModal_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (BtnYes.Visibility == Visibility.Visible) BtnYes_Click(this, new RoutedEventArgs());
                else if (BtnOk.Visibility == Visibility.Visible) BtnOk_Click(this, new RoutedEventArgs());
            }
            else if (e.Key == Key.Escape)
            {
                if (BtnNo.Visibility == Visibility.Visible) BtnNo_Click(this, new RoutedEventArgs());
                else if (BtnCancel.Visibility == Visibility.Visible) BtnCancel_Click(this, new RoutedEventArgs());
                else BtnClose_Click(this, new RoutedEventArgs());
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            DialogResult = true;
            Close();
        }

        private void BtnYes_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Yes;
            DialogResult = true;
            Close();
        }

        private void BtnNo_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.No;
            DialogResult = false;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Cancel;
            DialogResult = false;
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.None;
            DialogResult = false;
            Close();
        }
    }
}
