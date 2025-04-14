using System;
using System.Windows;
using System.Windows.Threading;
using static SNIBypassGUI.Consts.ConfigConsts;
using static SNIBypassGUI.Consts.PathConsts;
using static SNIBypassGUI.Utils.ConvertUtils;
using static SNIBypassGUI.Utils.FileUtils;
using static SNIBypassGUI.Utils.IniFileUtils;
using static SNIBypassGUI.Utils.LogManager;

namespace SNIBypassGUI
{
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            // 捕获非 UI 线程中的未处理异常
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            if (StringToBool(INIRead(AdvancedSettings, GUIDebug, INIPath)))
            {
                // 获取日志路径
                string logPath = GetLogPath();

                // 实时追踪日志文件
                TailFile(logPath, "GUIDebug", true);

                // 启用日志
                EnableLog();
            }
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
    }
}