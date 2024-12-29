using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace RpNet.CMDHelper
{
    // --------------------------------------------------------------------------
    //
    //  CMD 类
    //  该类用于在 C# 应用程序中运行 Windows CMD 命令，并捕获执行结果，包括标准输出和错误信息。
    //  提供了对 CMD 命令运行的灵活控制，如设置工作目录、超时时间等，以便轻松集成和使用 CMD 功能。
    //
    //  功能：
    //  - 异步运行 CMD 命令并捕获标准输出和错误信息。
    //  - 提供自定义工作目录和超时时间的支持。
    //  - 在命令执行完成后返回命令执行的状态（成功或失败）、标准输出、以及错误信息。
    //  - 在超时或异常情况下安全地终止进程并返回相关错误信息。
    //
    //  注意：
    //  - 使用本类时，请确保提供的 CMD 命令是有效的，并检查相关权限以确保命令可以成功执行。
    //  - 默认超时时间为 30 秒，可以根据需求调整 `timeoutMilliseconds` 参数。
    //  - 如果命令运行时间较长，请适当增加超时时间，避免不必要的进程终止。
    //  - 当指定工作目录时，请确保目录路径存在且具有访问权限。
    //
    //  示例：
    //  ```csharp
    //  var result = await CMD.RunCommand("ipconfig");
    //  if (result.Success)
    //  {
    //      Console.WriteLine("Output:");
    //      Console.WriteLine(result.Output);
    //  }
    //  else
    //  {
    //      Console.WriteLine("Error:");
    //      Console.WriteLine(result.Error);
    //  }
    //  ```
    //
    // --------------------------------------------------------------------------
    public class CMD
    {
        // 运行 CMD 命令的方法
        public static async Task<(bool Success, string Output, string Error)> RunCommand(
            string command,
            string workingDirectory = "",
            int timeoutMilliseconds = 30000) // 设置默认超时30秒
        {
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

            var process = new Process
            {
                StartInfo = processStartInfo,
                EnableRaisingEvents = true
            };

            StringBuilder output = new StringBuilder();
            StringBuilder error = new StringBuilder();

            // 订阅输出流
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                    output.AppendLine(e.Data);
            };

            // 订阅错误流
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                    error.AppendLine(e.Data);
            };

            try
            {
                // 启动进程并开始异步读取输出和错误
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // 等待进程完成或超时
                var completionTask = Task.Run(() => process.WaitForExit());
                var timeoutTask = Task.Delay(timeoutMilliseconds);

                var completedTask = await Task.WhenAny(completionTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    // 超时，终止进程
                    process.Kill();
                    return (false, output.ToString(), "进程超时。");
                }

                // 如果进程正常结束，返回输出和错误信息
                return (process.ExitCode == 0, output.ToString(), error.ToString());
            }
            catch (Exception ex)
            {
                return (false, output.ToString(), $"错误：{ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}