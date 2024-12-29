using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using static SNIBypassGUI.LogHelper;

namespace SNIBypassGUI
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
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
            // 日志记录
            WriteLog("发生未经处理的异常！", LogLevel.Error, e.Exception);

            // 弹出窗口提示
            MessageBox.Show($"遇到未处理的异常：{e.Exception}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);

            // 阻止程序崩溃
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = (Exception)e.ExceptionObject;

            // 日志记录
            WriteLog("发生未经处理的异常！", LogLevel.Error, ex);

            // 弹出窗口提示
            MessageBox.Show($"遇到未处理的异常：{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void Application_SessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            // 关机前模拟点击“退出工具”按钮
            var _mainWindow = Current.Windows
            .Cast<Window>()
            .FirstOrDefault(window => window is MainWindow) as MainWindow;
            _mainWindow.ExitBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

    }
}
