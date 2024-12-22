using System;
using System.IO;
using System.Text;
using static SNIBypassGUI.PublicHelper;

namespace SNIBypassGUI
{
    public class LogHelper
    {
        // 锁对象，用于线程安全
        private static readonly object LockObject = new object();

        public static string[] GUILogHead =
        {
            "——————————————————————————————————————————",
            "  ___   _  _   ___   ___                                   ___   _   _   ___ ",
            " / __| | \\| | |_ _| | _ )  _  _   _ __   __ _   ___  ___  / __| | | | | |_ _|",
            " \\__ \\ | .` |  | |  | _ \\ | || | | '_ \\ / _` | (_-< (_-< | (_ | | |_| |  | | ",
            " |___/ |_|\\_| |___| |___/  \\_, | | .__/ \\__,_| /__/ /__/  \\___|  \\___/  |___|",
            "                           |__/  |_|                                         ",
            "——————————————————————————————————————————",
            "版本号：" + PresetGUIVersion,
            "记录时间：" +DateTime.UtcNow.ToString(),
            "系统信息：" + OSDescription,
            "——————————————————————————————————————————"
        };

        // 写入日志的方法
        public static void WriteLog(string message, LogLevel logLevel = LogLevel.Info)
        {
            if (OutputLog)
            {
                lock (LockObject)
                {
                    if (logLevel != LogLevel.None)
                    {
                        string logMessage = $"{DateTime.Now} [{logLevel}] {message}{Environment.NewLine}";
                        File.AppendAllText(PathsSet.GUILogPath, logMessage, Encoding.UTF8);
                    }
                    else
                    {
                        string logMessage = $"{message}{Environment.NewLine}";
                        File.AppendAllText(PathsSet.GUILogPath, logMessage, Encoding.UTF8);
                    }
                }
            }
        }

        // 日志级别枚举
        public enum LogLevel
        {
            Error,
            Warning,
            Info,
            Debug,
            None
        }
    }
}
