using System.Windows;
using SteamRouteFixer.Services.Common;
using SteamRouteFixer.Views;

namespace SteamRouteFixer
{
    public partial class App : System.Windows.Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Prevent WPF from shutting down when SplashWindow closes
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            TxaLogger.Initialize();
            TxaLogger.Info("Ứng dụng Steam Route Fixer bắt đầu khởi động...");

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    TxaLogger.Error("UNHANDLED CRASH EXCEPTION", ex);
                }
            };

            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                TxaLogger.Error("UNOBSERVED TASK EXCEPTION", args.Exception);
                args.SetObserved();
            };

            // 1. Show Splash Window
            var splash = new SplashWindow();
            splash.Show();

            splash.SetStatus("Đang nạp cấu hình và thiết lập thư mục AppData...");
            StoragePathManager.EnsureDirectories();
            var config = StoragePathManager.LoadConfig();

            // Initialize TxaLanguage (support .txa file double click)
            string? startupTxaFile = e.Args.Length > 0 ? e.Args[0] : null;
            TxaLanguageManager.Initialize(startupTxaFile);

            splash.SetStatus($"Đang áp dụng giao diện ({config.Theme})...");
            ThemeManager.ApplyTheme(config.Theme);

            splash.SetStatus("Đang kiểm tra kết nối mạng và chuẩn bị giao diện chính...");
            await Task.Delay(800); // Smooth splash experience

            // 2. Initialize Main Window
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;

            // Switch to OnMainWindowClose so app closes when MainWindow closes
            ShutdownMode = ShutdownMode.OnMainWindowClose;

            mainWindow.Show();
            splash.Close();

            TxaLogger.Success("Giao diện chính đã hiển thị thành công.");
        }
    }
}
