using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static SNIBypassGUI.Utils.LogManager;

namespace SNIBypassGUI.Utils
{
    public static class ProcessUtils
    {
        /// <summary>
        /// 检查进程是否正在运行
        /// </summary>
        /// <param name="processName">进程名称</param>
        /// <returns>是否正在运行</returns>
        public static bool IsProcessRunning(string processName)
        {
            try
            {
                return GetProcessCount(processName) > 0;
            }
            catch (Exception ex)
            {
                WriteLog($"检查进程 {processName} 是否正在运行时出现异常。", LogLevel.Error, ex);
                return false;
            }
        }

        /// <summary>
        /// 获取进程数量
        /// </summary>
        /// <param name="processName">进程名称</param>
        /// <returns>找到的进程数</returns>
        public static int GetProcessCount(string processName)
        {
            try
            {
                if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    processName = Path.GetFileNameWithoutExtension(processName);
                }
                return Process.GetProcessesByName(processName).Length;
            }
            catch (Exception ex)
            {
                WriteLog($"获取进程 {processName} 数量时出现异常。", LogLevel.Error, ex);
                return -1;
            }
        }

        /// <summary>
        /// 启动进程
        /// </summary>
        /// <param name="fileName">路径</param>
        /// <param name="arguments">启动参数</param>
        /// <param name="workingDirectory">工作目录</param>
        /// <param name="useShellExecute">指示是否使用操作系统 shell 启动进程</param>
        /// <param name="createNoWindow">指示是否在新窗口中启动进程</param>
        public static Process StartProcess(string fileName, string arguments = "", string workingDirectory = "", bool useShellExecute = false, bool createNoWindow = false)
        {
            try
            {
                Process process = new()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        UseShellExecute = useShellExecute,
                        CreateNoWindow = createNoWindow,
                        WorkingDirectory = workingDirectory
                    }
                };
                process.Start();
                WriteLog($"成功启动 {fileName}。", LogLevel.Info);
                return process;
            }
            catch (Exception ex)
            {
                WriteLog($"启动 {fileName} 时出现异常。", LogLevel.Error, ex);
                throw;
            }
        }

        /// <summary>
        /// 结束进程
        /// </summary>
        /// <param name="processName">进程名称</param>
        /// <returns>是否成功终止进程</returns>
        public static bool KillProcess(string processName)
        {
            try
            {
                Process[] processes = Process.GetProcessesByName(processName);
                if (processes.Length == 0)
                {
                    WriteLog($"未找到名称为 {processName} 的进程。", LogLevel.Warning);
                    return false;
                }
                foreach (var process in processes)
                {
                    try
                    {
                        process.Kill();
                        WriteLog($"成功结束 PID 为 {process.Id} 的进程 {processName}。", LogLevel.Info);
                    }
                    catch (Exception ex)
                    {
                        WriteLog($"结束 PID 为 {process.Id} 的进程 {processName} 时出现异常。", LogLevel.Error, ex);
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                WriteLog($"结束进程 {processName} 时出现异常。", LogLevel.Error, ex);
                throw;
            }
        }

        /// <summary>
        /// 同步等待进程初始化完成
        /// </summary>
        public static void WaitForProcessReady(this Process process, int timeout = 5000)
        {
            try
            {
                if (process == null || process.HasExited) return;

                if (!process.StartInfo.UseShellExecute && process.StartInfo.CreateNoWindow)
                {
                    int waited = 0;
                    while (!process.HasExited && process.MainWindowHandle == IntPtr.Zero && waited < timeout)
                    {
                        Thread.Sleep(100);
                        waited += 100;
                    }

                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        WriteLog($"进程 {process.ProcessName} 已准备就绪。", LogLevel.Info);
                    }
                    else
                    {
                        WriteLog($"等待进程 {process.ProcessName} 准备超时。", LogLevel.Warning);
                    }
                }
                else
                {
                    if (!process.WaitForInputIdle(timeout))
                    {
                        WriteLog($"进程 {process.ProcessName} 未进入空闲状态，可能是控制台程序。", LogLevel.Warning);
                    }
                    else
                    {
                        WriteLog($"进程 {process.ProcessName} 已进入空闲状态。", LogLevel.Info);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog($"等待进程 {process.ProcessName} 初始化时发生异常。", LogLevel.Error, ex);
            }
        }

        public static Task<bool> WaitForExitAsync(this Process process, int timeout)
        {
            return Task.Run(() => process.WaitForExit(timeout));
        }
    }
}
