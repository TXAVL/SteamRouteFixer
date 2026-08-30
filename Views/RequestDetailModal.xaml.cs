using System.Windows;
using System.Windows.Controls;
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
                AnimateButtonFeedback(BtnCopyUrl, ScaleBtnCopyUrl, "📋 Copy URL", "✅ Đã Copy URL!", "📋 Đã sao chép liên kết URL vào Clipboard!");
            }
            catch { }
        }

        private void BtnCopyRequest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(TxtRequestBody.Text);
                AnimateButtonFeedback(BtnCopyRequest, ScaleBtnCopyRequest, "📋 Copy Request", "✅ Đã Copy Request!", "📥 Đã sao chép nội dung Request vào Clipboard!");
            }
            catch { }
        }

        private void BtnCopyResponse_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string textToCopy = string.IsNullOrEmpty(_item.ResponseBody) ? TxtResponseBody.Text : _item.ResponseBody;
                Clipboard.SetText(textToCopy);
                AnimateButtonFeedback(BtnCopyResponse, ScaleBtnCopyResponse, "✨ 📋 COPY RESPONSE (BODY)", "✨ ✅ ĐÃ COPY BODY!", "✨ 📤 Đã sao chép toàn bộ Response Body vào Clipboard!");
            }
            catch { }
        }

        private async void AnimateButtonFeedback(Button btn, System.Windows.Media.ScaleTransform scale, string originalText, string copiedText, string toastMsg)
        {
            try
            {
                // 1. Text change & scale bounce
                btn.Content = copiedText;

                var scaleAnim = new DoubleAnimation
                {
                    From = 1.0,
                    To = 1.08,
                    Duration = TimeSpan.FromMilliseconds(100),
                    AutoReverse = true
                };
                scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnim);
                scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnim);

                // 2. Show toast
                ShowCopyNotice(toastMsg);

                // 3. Revert after 1.3 seconds
                await Task.Delay(1300);
                btn.Content = originalText;
            }
            catch
            {
                btn.Content = originalText;
            }
        }

        private void ShowCopyNotice(string message)
        {
            TxtCopyNotice.Text = message;
            BorderCopyToast.Opacity = 1.0;

            var fadeAnim = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                BeginTime = TimeSpan.FromMilliseconds(1400),
                Duration = TimeSpan.FromMilliseconds(600)
            };
            BorderCopyToast.BeginAnimation(OpacityProperty, fadeAnim);
        }
    }
}
