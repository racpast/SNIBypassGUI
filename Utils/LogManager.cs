using System;
using System.IO;
using static SNIBypassGUI.Consts.AppConsts;
using static SNIBypassGUI.Consts.PathConsts;
using static SNIBypassGUI.Utils.FileUtils;

namespace SNIBypassGUI.Utils
{
    public static class LogManager
    {
        private static readonly object LockObject = new();
        private static bool OutputLog = false;
        private static readonly LogLevel CurrentLogLevel = LogLevel.Debug;

        /// <summary>
        /// 是否启用日志。
        /// </summary>
        public static bool IsLogEnabled => OutputLog;

        /// <summary>
        /// 启用日志。
        /// </summary>
        public static void EnableLog()
        {
            OutputLog = true;
            AppendToFile(GetLogPath(), LogHead);
        }

        /// <summary>
        /// 停用日志。
        /// </summary>
        public static void DisableLog() => OutputLog = false;

        /// <summary>
        /// 获取日志文件路径。
        /// </summary>
        public static string GetLogPath() => Path.Combine(LogDirectory, $"SNIBypassGUI-{DateTime.Now:yyyy-MM-dd}.log");

        /// <summary>
        /// 写入日志。
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="logLevel">日志等级</param>
        /// <param name="ex">异常</param>
        public static void WriteLog(string message, LogLevel logLevel = LogLevel.Info, Exception ex = null)
        {
            if (!OutputLog || string.IsNullOrEmpty(GetLogPath()) || logLevel > CurrentLogLevel) return;
            lock (LockObject)
            {
                string logMessage = $"{DateTime.Now} [{logLevel}] {message}";
                if (ex != null) logMessage += $" | 异常：{ex.Message} | 调用堆栈：{ex.StackTrace}";
                logMessage += $"{Environment.NewLine}";
                AppendToFile(GetLogPath(), logMessage);
            }
        }

        /// <summary>
        /// 日志级别枚举。
        /// </summary>
        public enum LogLevel
        {
            Error,
            Warning,
            Info,
            Debug
        }
    }
}
