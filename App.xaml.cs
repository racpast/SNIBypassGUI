using System;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using static SNIBypassGUI.Consts.ConfigConsts;
using static SNIBypassGUI.Consts.LinksConsts;
using static SNIBypassGUI.Consts.PathConsts;
using static SNIBypassGUI.Utils.ConvertUtils;
using static SNIBypassGUI.Utils.FileUtils;
using static SNIBypassGUI.Utils.IniFileUtils;
using static SNIBypassGUI.Utils.LogManager;
using static SNIBypassGUI.Utils.ProcessUtils;

namespace SNIBypassGUI
{
    public partial class App : Application
    {
        /// <summary>
        /// 构造函数。
        /// </summary>
        public App()
        {
            if (!IsDotNet472OrHigherInstalled())
            {
                if (MessageBox.Show("此应用程序需要 .NET Framework 4.7.2 或更高版本。\n是否需要打开 Microsoft 官方下载页面？", "缺少必要组件", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) StartProcess(Net472DownloadUrl, useShellExecute: true);
                Environment.Exit(1);
            }

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

        /// <summary>
        /// 检查是否安装了 .NET Framework 4.7.2 或更高版本。
        /// </summary>
        private bool IsDotNet472OrHigherInstalled()
        {
            const string registryKeyPath = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full";

            // .NET Framework 4.7.2 的最小 Release 值，参考：https://learn.microsoft.com/zh-cn/dotnet/framework/install/how-to-determine-which-versions-are-installed
            const int RequiredReleaseKey = 461808; 

            using RegistryKey key = Registry.LocalMachine.OpenSubKey(registryKeyPath);
            if (key != null)
            {
                object releaseValue = key.GetValue("Release");
                if (releaseValue != null && (int)releaseValue >= RequiredReleaseKey) return true;
            }
            return false;
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