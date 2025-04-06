﻿using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using static SNIBypassGUI.Utils.LogManager;

namespace SNIBypassGUI.Utils
{
    public static class CommandUtils
    {
        /*
        /// <summary>
        /// 执行指定的 PowerShell 命令
        /// </summary>
        /// <param name="command">PowerShell命令</param>
        public static async Task RunPowerShell(string command)
        {
            try
            {
                await Task.Run(() =>
                {
                    using var powerShell = PowerShell.Create();
                    powerShell.AddScript(command);
                    var result = powerShell.Invoke();
                    if (powerShell.HadErrors)
                    {
                        var errorMessages = powerShell.Streams.Error.Select(e => e.ToString()).ToList();
                        throw new InvalidOperationException($"PowerShell 执行失败：{string.Join(Environment.NewLine, errorMessages)}");
                    }
                });
            }
            catch (Exception ex)
            {
                WriteLog($"执行 PowerShell 命令 {command} 时遇到异常。", LogLevel.Error, ex);
                throw;
            }
        }
        */

        /// <summary>
        /// 执行指定的 CMD 命令
        /// </summary>
        /// <param name="command">CMD 命令</param>
        /// <param name="workingDirectory">工作目录</param>
        /// <param name="timeoutMilliseconds">超时时间（以毫秒为单位）</param>
        public static async Task<(bool Success, string Output, string Error)> RunCommand(string command, string workingDirectory = "", int timeoutMilliseconds = 15000)
        {
            if (string.IsNullOrWhiteSpace(command)) return (false, "", "命令不能为空。");

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {command}",
                WorkingDirectory = workingDirectory,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = new Process { StartInfo = processStartInfo };

            StringBuilder output = new();
            StringBuilder error = new();

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null) output.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null) error.AppendLine(e.Data);
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var completionTask = Task.Run(() => process.WaitForExit());
                var timeoutTask = Task.Delay(timeoutMilliseconds);
                var completedTask = await Task.WhenAny(completionTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    process.Kill();
                    WriteLog($"执行命令 {command} 超时。", LogLevel.Warning);
                    return (false, output.ToString(), "进程超时。");
                }

                await completionTask;
                return (process.ExitCode == 0, output.ToString(), error.ToString());
            }
            catch (Exception ex)
            {
                WriteLog($"执行命令 {command} 时遇到异常。", LogLevel.Error, ex);
                return (false, output.ToString(), ex.Message);
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
