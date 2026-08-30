using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using SteamRouteFixer.Models;

using SteamRouteFixer.Services.Common;

namespace SteamRouteFixer.Views
{
    public partial class RequestDetailModal : Window
    {
        private readonly NetworkRequestItem _item;

        public RequestDetailModal(NetworkRequestItem item)
        {
            InitializeComponent();
            _item = item;
            ApplyLanguageTranslations();
            TxaLanguageManager.OnLanguageChanged += ApplyLanguageTranslations;
            Closed += (s, e) => TxaLanguageManager.OnLanguageChanged -= ApplyLanguageTranslations;
            LoadItemData();
        }

        private void ApplyLanguageTranslations()
        {
            Title = TxaLanguageManager.GetString("t_req_detail_title", "Chi Tiết HTTP Request & Response");
            if (TxtHeaderTitle != null) TxtHeaderTitle.Text = TxaLanguageManager.GetString("t_req_detail_header", "🔍 THÔNG TIN CHI TIẾT REQUEST & RESPONSE");

            if (TxtMetaProcess != null) TxtMetaProcess.Text = TxaLanguageManager.GetString("t_meta_process", "📱 Tiến trình: ");
            if (TxtMetaRemote != null) TxtMetaRemote.Text = TxaLanguageManager.GetString("t_meta_remote", "🌐 Remote: ");
            if (TxtMetaDuration != null) TxtMetaDuration.Text = TxaLanguageManager.GetString("t_meta_latency", "⏱ Độ trễ: ");
            if (TxtMetaReqSize != null) TxtMetaReqSize.Text = TxaLanguageManager.GetString("t_meta_req_size", "📦 Request Size: ");
            if (TxtMetaRespSize != null) TxtMetaRespSize.Text = TxaLanguageManager.GetString("t_meta_resp_size", "📥 Response Size: ");

            if (TabItemResponse != null) TabItemResponse.Header = TxaLanguageManager.GetString("t_tab_response", "📤 RESPONSE (PHẢN HỒI)");
            if (TxtRespSub != null) TxtRespSub.Text = TxaLanguageManager.GetString("t_resp_sub", "Response Headers & Payload:");

            if (TabItemRequest != null) TabItemRequest.Header = TxaLanguageManager.GetString("t_tab_request", "📥 REQUEST (YÊU CẦU)");
            if (TxtReqSub != null) TxtReqSub.Text = TxaLanguageManager.GetString("t_req_sub", "Request Headers & Body:");

            if (BtnCopyUrl != null) BtnCopyUrl.Content = TxaLanguageManager.GetString("t_btn_copy_url", "📋 Copy URL");
            if (BtnCopyRequest != null) BtnCopyRequest.Content = TxaLanguageManager.GetString("t_btn_copy_req", "📋 Copy Request");
            if (BtnCopyResponse != null) BtnCopyResponse.Content = TxaLanguageManager.GetString("t_btn_copy_resp", "✨ 📋 COPY RESPONSE (BODY)");
            if (BtnCloseModal != null) BtnCloseModal.Content = TxaLanguageManager.GetString("t_btn_close", "Đóng");
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
                string original = TxaLanguageManager.GetString("t_btn_copy_url", "📋 Copy URL");
                string toast = TxaLanguageManager.GetString("t_toast_copy_url", "📋 Đã sao chép liên kết URL vào Clipboard!");
                AnimateButtonFeedback(BtnCopyUrl, ScaleBtnCopyUrl, original, "✅ Copied!", toast);
            }
            catch { }
        }

        private void BtnCopyRequest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(TxtRequestBody.Text);
                string original = TxaLanguageManager.GetString("t_btn_copy_req", "📋 Copy Request");
                string toast = TxaLanguageManager.GetString("t_toast_copy_req", "📥 Đã sao chép nội dung Request vào Clipboard!");
                AnimateButtonFeedback(BtnCopyRequest, ScaleBtnCopyRequest, original, "✅ Copied!", toast);
            }
            catch { }
        }

        private void BtnCopyResponse_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string textToCopy = string.IsNullOrEmpty(_item.ResponseBody) ? TxtResponseBody.Text : _item.ResponseBody;
                Clipboard.SetText(textToCopy);
                string original = TxaLanguageManager.GetString("t_btn_copy_resp", "✨ 📋 COPY RESPONSE (BODY)");
                string toast = TxaLanguageManager.GetString("t_toast_copy_resp", "✨ 📤 Đã sao chép toàn bộ Response Body vào Clipboard!");
                AnimateButtonFeedback(BtnCopyResponse, ScaleBtnCopyResponse, original, "✨ ✅ Copied Body!", toast);
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
