using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using static SNIBypassGUI.Utils.LogManager;

namespace SNIBypassGUI
{
    public partial class App : Application
    {
        public App()
        {
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;

            // 捕获非 UI 线程中的未处理异常
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            WriteLog("发生未经处理的异常！", LogLevel.Error, e.Exception);
            MessageBox.Show($"遇到未处理的异常：{e.Exception}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = (Exception)e.ExceptionObject;
            WriteLog("发生未经处理的异常！", LogLevel.Error, ex);
            MessageBox.Show($"遇到未处理的异常：{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void Application_SessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            var _mainWindow = Current.Windows
            .Cast<Window>()
            .FirstOrDefault(window => window is Views.MainWindow) as Views.MainWindow;
            _mainWindow.ExitBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
    }
}