using System;
using System.Diagnostics;
using System.Linq;
using System.Management;

namespace SNIBypassGUI.Utils
{
    public static class CommandLineUtils
    {
        /// <summary>
        /// 尝试获取参数的值。
        /// </summary>
        public static bool TryGetArgumentValue(string[] args, string argName, out string value)
        {
            value = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals(argName, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    value = args[i + 1];
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 判断是否含有参数。
        /// </summary>
        public static bool ContainsArgument(string[] args, string argName)
        {
            return args.Contains(argName, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 获取进程的命令行参数。
        /// </summary>
        public static string GetCommandLine(Process process)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher($"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {process.Id}");
                foreach (var obj in searcher.Get()) return obj["CommandLine"]?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
            return string.Empty;
        }
    }
}
