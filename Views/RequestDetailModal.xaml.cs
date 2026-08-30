using System.Windows;
using System.Windows.Media.Animation;
using SteamRouteFixer.Models;

namespace SteamRouteFixer.Views
{
    public partial class RequestDetailModal : Window
    {
        private readonly NetworkRequestItem _item;

        public RequestDetailModal(NetworkRequestItem item)
        {
            InitializeComponent();
            _item = item;
            LoadItemData();
        }

        private void LoadItemData()
        {
            TxtFullUrl.Text = string.IsNullOrEmpty(_item.Url) ? $"https://{_item.Host}{_item.Path}" : _item.Url;
            TxtMethod.Text = $"{_item.Protocol} {_item.Method}";
            TxtStatus.Text = _item.StatusText;
            BadgeStatus.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_item.StatusBadgeColor)
            );

            TxtProcess.Text = $"{_item.ProcessName} (PID: {_item.Pid})";
            TxtRemote.Text = _item.RemoteEndpoint;
            TxtDuration.Text = _item.DurationDisplay;
            TxtReqSize.Text = _item.RequestBytes > 0 ? $"{_item.RequestBytes} B" : "--";
            TxtRespSize.Text = _item.FormattedSize;

            // Format Request Display
            TxtRequestBody.Text = $"{_item.RequestHeaders}\r\n\r\n{_item.RequestBody}";

            // Format Response Display
            TxtResponseBody.Text = $"{_item.ResponseHeaders}\r\n\r\n{_item.ResponseBody}";
        }

        private void BtnCopyUrl_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(TxtFullUrl.Text);
                ShowCopyNotice("✓ Đã copy URL!");
            }
            catch { }
        }

        private void BtnCopyRequest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(TxtRequestBody.Text);
                ShowCopyNotice("✓ Đã copy Request Content!");
            }
            catch { }
        }

        private void BtnCopyResponse_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string textToCopy = string.IsNullOrEmpty(_item.ResponseBody) ? TxtResponseBody.Text : _item.ResponseBody;
                Clipboard.SetText(textToCopy);

                // Play Animation
                if (TryFindResource("CopySuccessAnimation") is Storyboard sb)
                {
                    sb.Begin(this);
                }
                ShowCopyNotice("✨ ✓ Đã copy toàn bộ Response Body!");
            }
            catch { }
        }

        private void ShowCopyNotice(string message)
        {
            TxtCopyNotice.Text = message;
            TxtCopyNotice.Opacity = 1.0;
            var anim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromSeconds(2.0));
            TxtCopyNotice.BeginAnimation(OpacityProperty, anim);
        }
    }
}
