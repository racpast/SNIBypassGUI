﻿using System;
using System.IO;
using static SNIBypassGUI.Utils.FileUtils;
using static SNIBypassGUI.Consts.AppConsts;
using static SNIBypassGUI.Consts.PathConsts;

namespace SNIBypassGUI.Utils
{
    public static class LogManager
    {
        private static readonly object LockObject = new();
        private static bool OutputLog = false;
        private static readonly LogLevel CurrentLogLevel = LogLevel.Debug;
        private static string LogPath;

        /// <summary>
        /// 是否启用日志
        /// </summary>
        public static bool IsLogEnabled => OutputLog;

        /// <summary>
        /// 启用日志
        /// </summary>
        public static void EnableLog()
        {
            OutputLog = true;
            EnsureDirectoryExists(LogDirectory);
            LogPath = GetLogPath();
            AppendToFile(LogPath, LogHead);
        }

        /// <summary>
        /// 停用日志
        /// </summary>
        public static void DisableLog() => OutputLog = false;

        /// <summary>
        /// 删除所有日志
        /// </summary>
        public static void DeleteLogs() => ClearFolder(LogDirectory);

        /// <summary>
        /// 获取日志文件路径
        /// </summary>
        public static string GetLogPath() => Path.Combine(LogDirectory, $"SNIBypassGUI-{DateTime.Now:yyyy-MM-dd}.log");

        /// <summary>
        /// 写入日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="logLevel">日志等级</param>
        /// <param name="ex">异常</param>
        public static void WriteLog(string message, LogLevel logLevel = LogLevel.Info, Exception ex = null)
        {
            if (!OutputLog || string.IsNullOrEmpty(LogPath) || logLevel > CurrentLogLevel) return;
            lock (LockObject)
            {
                string logMessage = $"{DateTime.Now} [{logLevel}] {message}";
                if (ex != null) logMessage += $" | 异常：{ex.Message} | 调用堆栈：{ex.StackTrace}";
                logMessage += $"{Environment.NewLine}";
                AppendToFile(LogPath, logMessage );
            }
        }

        /// <summary>
        /// 日志级别枚举
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
