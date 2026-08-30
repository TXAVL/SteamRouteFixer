using System.Windows;
using SteamRouteFixer.Views;

namespace SteamRouteFixer.Services.Common
{
    public static class TxaMessageBox
    {
        public static MessageBoxResult Show(string message, string title = "Thông báo", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information)
        {
            if (Application.Current?.Dispatcher?.CheckAccess() == false)
            {
                return Application.Current.Dispatcher.Invoke(() => Show(message, title, buttons, image));
            }

            Window? activeWindow = null;
            try
            {
                if (Application.Current?.Windows != null)
                {
                    foreach (Window w in Application.Current.Windows)
                    {
                        if (w != null && w.IsActive)
                        {
                            activeWindow = w;
                            break;
                        }
                    }
                }
                activeWindow ??= Application.Current?.MainWindow;
            }
            catch { }

            var modal = new CustomMessageModal(message, title, buttons, image);
            if (activeWindow != null && activeWindow.IsVisible)
            {
                modal.Owner = activeWindow;
            }

            modal.ShowDialog();
            return modal.Result;
        }

        public static MessageBoxResult Show(Window owner, string message, string title = "Thông báo", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information)
        {
            if (Application.Current?.Dispatcher?.CheckAccess() == false)
            {
                return Application.Current.Dispatcher.Invoke(() => Show(owner, message, title, buttons, image));
            }

            var modal = new CustomMessageModal(message, title, buttons, image);
            if (owner != null && owner.IsVisible)
            {
                modal.Owner = owner;
            }

            modal.ShowDialog();
            return modal.Result;
        }

        public static bool Confirm(string message, string title = "Xác nhận")
        {
            return Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        public static bool Confirm(Window owner, string message, string title = "Xác nhận")
        {
            return Show(owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }
    }
}
