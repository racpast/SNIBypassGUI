using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using static SNIBypassGUI.Consts.PathConsts;
using static SNIBypassGUI.Utils.CommandLineUtils;
using static SNIBypassGUI.Utils.LogManager;
using static SNIBypassGUI.Utils.ProcessUtils;

namespace SNIBypassGUI.Utils
{
    public static class FileUtils
    {
        public static List<string> RunTailBatFiles = [];

        /// <summary>
        /// 将内容写到指定文件顶部
        /// </summary>
        /// <param name="filePath">指定的文件</param>
        /// <param name="linesToAdd">需要追加的内容（数组形式）</param>
        public static void PrependToFile(string filePath, string[] linesToAdd)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    File.WriteAllLines(filePath, linesToAdd);
                    return;
                }
                string[] existingLines = File.ReadAllLines(filePath);
                var allLines = new string[linesToAdd.Length + existingLines.Length];
                linesToAdd.CopyTo(allLines, 0);
                existingLines.CopyTo(allLines, linesToAdd.Length);
                File.WriteAllLines(filePath, allLines);
            }
            catch (Exception ex)
            {
                WriteLog($"写入文件 {filePath} 时遇到异常。", LogLevel.Error, ex);
                throw;
            }
        }

        /// <summary>
        /// 将内容写到指定文件顶部
        /// </summary>
        /// <param name="filePath">指定的文件路径</param>
        /// <param name="linesToAdd">需要写入的内容（列表形式）</param>
        public static void PrependToFile(string filePath, List<string> linesToAdd)
        {
            PrependToFile(filePath, linesToAdd.ToArray());
        }

        /// <summary>
        /// 将内容写到指定文件顶部
        /// </summary>
        /// <param name="filePath">指定的文件路径</param>
        /// <param name="linesToAdd">需要写入的内容（单行）</param>
        public static void PrependToFile(string filePath, string lineToAdd) => PrependToFile(filePath, new[] { lineToAdd });

        /// <summary>
        /// 将内容写入指定文件末尾
        /// </summary>
        /// <param name="filePath">指定的文件路径</param>
        /// <param name="linesToAdd">需要追加的内容（数组形式）</param>
        public static void AppendToFile(string filePath, string[] linesToAdd, Encoding encoding = null)
        {
            try
            {
                encoding ??= Encoding.UTF8;
                if (!File.Exists(filePath))
                {
                    File.WriteAllLines(filePath, linesToAdd);
                    return;
                }
                using StreamWriter writer = new(filePath, true, encoding);
                foreach (string line in linesToAdd) writer.WriteLine(line);
            }
            catch (Exception ex)
            {
                WriteLog($"写入文件 {filePath} 时遇到异常。", LogLevel.Error, ex);
                throw;
            }
        }

        /// <summary>
        /// 将内容写入指定文件末尾
        /// </summary>
        /// <param name="filePath">指定的文件路径</param>
        /// <param name="linesToAdd">需要追加的内容（列表形式）</param>
        public static void AppendToFile(string filePath, List<string> linesToAdd, Encoding encoding = null) => AppendToFile(filePath, linesToAdd.ToArray(), encoding);

        /// <summary>
        /// 将单行内容写入指定文件末尾
        /// </summary>
        /// <param name="filePath">指定的文件路径</param>
        /// <param name="lineToAdd">需要追加的内容（单行）</param>
        public static void AppendToFile(string filePath, string lineToAdd, Encoding encoding = null) => AppendToFile(filePath, new[] { lineToAdd }, encoding);

        /// <summary>
        /// 确保指定目录存在
        /// </summary>
        /// <param name="path">指定目录</param>
        public static void EnsureDirectoryExists(string path)
        {
            try
            {
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            }
            catch (Exception ex)
            {
                WriteLog($"创建目录 {path} 时遇到异常。", LogLevel.Error, ex);
                throw;
            }
        }

        /// <summary>
        /// 清空指定目录
        /// </summary>
        /// <param name="folderPath">指定的目录</param>
        /// <param name="deleteFilesIndividually">指示是否递归删除</param>
        public static void ClearFolder(string folderPath, bool deleteFilesIndividually = false)
        {
            try
            {
                if (!Directory.Exists(folderPath))
                {
                    DirectoryNotFoundException ex = new($"无法找到指定的文件夹路径：{folderPath}");
                    WriteLog($"指定的文件夹路径 {folderPath} 不存在。", LogLevel.Error, ex);
                    throw ex;
                }

                if (deleteFilesIndividually) EmptyFolder(folderPath);
                else
                {
                    Directory.Delete(folderPath, true);
                    Directory.CreateDirectory(folderPath);
                }
            }
            catch (DirectoryNotFoundException ex)
            {
                WriteLog($"未找到目录 {folderPath}。", LogLevel.Error, ex);
            }
            catch (Exception ex)
            {
                WriteLog($"清空目录 {folderPath} 时遇到异常。", LogLevel.Error, ex);
                throw;
            }
        }

        /// <summary>
        /// 递归删除指定目录
        /// </summary>
        /// <param name="folderPath">指定的目录</param>
        private static void EmptyFolder(string folderPath)
        {
            if (Directory.Exists(folderPath))
            {
                string[] files = Directory.GetFiles(folderPath);
                foreach (string file in files)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        WriteLog($"删除文件 {file} 时遇到异常。", LogLevel.Error, ex);
                        throw;
                    }
                }
                string[] dirs = Directory.GetDirectories(folderPath);
                foreach (string dir in dirs)
                {
                    try
                    {
                        EmptyFolder(dir);
                        Directory.Delete(dir, true);
                    }
                    catch (Exception ex)
                    {
                        WriteLog($"删除目录 {dir} 时遇到异常。", LogLevel.Error, ex);
                        throw;
                    }
                }
            }
            else
            {
                DirectoryNotFoundException ex = new($"无法找到指定的文件夹路径：{folderPath}");
                WriteLog("指定的文件夹路径不存在。", LogLevel.Error, ex);
                throw ex;
            }
        }

        /// <summary>
        /// 释放资源文件
        /// </summary>
        /// <param name="resource">指定的资源</param>
        /// <param name="path">指定释放的路径</param>
        public static void ExtractResourceToFile(byte[] resource, String path)
        {
            using FileStream file = new(path, FileMode.Create);
            file.Write(resource, 0, resource.Length);
        }

        /// <summary>
        /// 获取文件大小（以字节为单位）
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public static long GetFileSize(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    WriteLog($"指定的文件路径 {filePath} 不存在。", LogLevel.Warning);
                    return 0;
                }
                FileInfo fileInfo = new(filePath);
                return fileInfo.Length;
            }
            catch (Exception ex)
            {
                WriteLog($"获取 {filePath} 文件大小时遇到异常。", LogLevel.Error, ex);
                throw;
            }
        }

        /// <summary>
        /// 获取目录大小（以指定单位为单位）
        /// </summary>
        /// <param name="directoryPath">指定目录</param>
        public static long GetDirectorySize(string directoryPath)
        {
            long size = 0;
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    WriteLog($"指定的文件夹路径 {directoryPath} 不存在。", LogLevel.Warning);
                    return 0;
                }
                string[] files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
                foreach (string file in files) size += GetFileSize(file);
            }
            catch (Exception ex)
            {
                WriteLog($"获取 {directoryPath} 目录大小时遇到异常。", LogLevel.Error, ex);
                throw;
            }
            return size;
        }

        /// <summary>
        /// 获取给定文件路径列表中的文件总大小（以字节为单位）
        /// </summary>
        /// <param name="paths">包含指定路径的数组</param>
        public static long GetTotalSize(string[] paths)
        {
            long totalSize = 0;
            try
            {
                foreach (var path in paths)
                {
                    if (File.Exists(path)) totalSize += GetFileSize(path);
                    else if (Directory.Exists(path)) totalSize += GetDirectorySize(path);
                    else WriteLog($"路径 {path} 不存在。", LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                WriteLog("获取总大小时遇到异常。", LogLevel.Error, ex);
                throw;
            }
            return totalSize;
        }

        /// <summary>
        /// 获取给定文件路径列表中的文件总大小（以字节为单位）
        /// </summary>
        /// <param name="paths">包含指定路径的列表</param>
        public static long GetTotalSize(List<string> paths) => GetTotalSize(paths.ToArray());

        /// <summary>
        /// 释放资源型加载图像，支持缓存和动态调整解码尺寸
        /// </summary>
        /// <param name="imagePath">图像路径</param>
        /// <param name="cache">缓存字典（可选）</param>
        /// <param name="maxDecodeSize">最大解码尺寸（可选）</param>
        public static BitmapImage LoadImage(string imagePath, Dictionary<string, BitmapImage> cache = null, int? maxDecodeSize = null)
        {
            // 检查缓存
            if (cache != null && cache.TryGetValue(imagePath, out BitmapImage cachedImage)) return cachedImage;

            BitmapImage image = new();

            try
            {
                image.BeginInit();
                image.UriSource = new Uri(imagePath);
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;

                // 动态调整解码尺寸
                if (maxDecodeSize.HasValue)
                {
                    var (w, h) = GetImageSize(imagePath);
                    if (w > maxDecodeSize || h > maxDecodeSize)
                    {
                        double ratio = Math.Min((double)maxDecodeSize.Value / w, (double)maxDecodeSize.Value / h);
                        image.DecodePixelWidth = (int)(w * ratio);
                        image.DecodePixelHeight = (int)(h * ratio);
                    }
                }

                image.EndInit();
                image.Freeze();

                // 写入缓存
                if (cache != null) cache[imagePath] = image;
            }
            catch (FileNotFoundException)
            {
                WriteLog($"文件 {imagePath} 不存在！", LogLevel.Warning);
            }
            catch (Exception ex)
            {
                WriteLog("加载图像时发生异常。", LogLevel.Error, ex);
            }

            return image;
        }

        /// <summary>
        /// 从文件中移除从“#   sectionName Start”到“#   sectionName End”的部分
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="sectionName">部分名称</param>
        public static void RemoveSection(string filePath, string sectionName)
        {
            if (!File.Exists(filePath))
            {
                WriteLog($"文件 {filePath} 不存在！", LogLevel.Warning);
                return;
            }
            string startMarker = $"#\t{sectionName} Start";
            string endMarker = $"#\t{sectionName} End";
            bool isRemoving = false;
            StringBuilder newContent = new();
            try
            {
                foreach (string line in File.ReadAllLines(filePath))
                {
                    if (line == startMarker)
                    {
                        isRemoving = true;
                        continue;
                    }
                    else if (line == endMarker)
                    {
                        isRemoving = false;
                        continue;
                    }
                    else if (!isRemoving) newContent.AppendLine(line);
                }
                File.WriteAllText(filePath, newContent.ToString());
            }
            catch (Exception ex)
            {
                WriteLog($"移除文件 {filePath} 中的 {sectionName} 部分时遇到异常。", LogLevel.Error, ex);
                throw;
            }
        }

        /// <summary>
        /// 从文件中获取从“#   sectionName Start”到“#   sectionName End”的部分
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="sectionName">部分名称</param>
        public static string[] GetSection(string filePath, string sectionName)
        {
            if (!File.Exists(filePath))
            {
                WriteLog($"文件 {filePath} 不存在！", LogLevel.Warning);
                return [];
            }
            string startMarker = $"#\t{sectionName} Start";
            string endMarker = $"#\t{sectionName} End";
            bool isInSection = false;
            List<String> section = [];
            try
            {
                foreach (string line in File.ReadAllLines(filePath))
                {
                    if (line == startMarker)
                    {
                        isInSection = true;
                        section.Add(line);
                    }
                    else if (isInSection)
                    {
                        section.Add(line);
                        if (line == endMarker) break;
                    }
                }
                return [.. section];
            }
            catch (Exception ex)
            {
                WriteLog($"读取文件 {filePath} 中的 {sectionName} 部分时遇到异常。", LogLevel.Error, ex);
                throw;
            }
        }

        /// <summary>
        /// 尝试删除指定文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public static void TryDelete(string filePath)
        {
            try
            {
                if (File.Exists(filePath)) File.Delete(filePath);
            }
            catch (Exception ex)
            {
                WriteLog($"删除文件 {filePath} 时遇到异常。", LogLevel.Error, ex);
                throw;
            }
        }

        /// <summary>
        /// 尝试删除指定文件
        /// </summary>
        /// <param name="filePaths">包含文件路径的数组</param>
        public static void TryDelete(string[] filePaths)
        {
            foreach (var filePath in filePaths)
            {
                TryDelete(filePath);
            }
        }

        /// <summary>
        /// 尝试删除指定文件
        /// </summary>
        /// <param name="filePaths">包含文件路径的列表</param>
        public static void TryDelete(List<string> filePaths) => TryDelete(filePaths.ToArray());

        /// <summary>
        /// 创建文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public static void EnsureFileExists(string filePath)
        {
            try
            {
                string dir = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(dir)) EnsureDirectoryExists(dir);
                if (!File.Exists(filePath)) File.Create(filePath).Dispose();
            }
            catch (Exception ex)
            {
                WriteLog($"创建文件 {filePath} 时遇到异常。", LogLevel.Error, ex);
                throw;
            }
        }

        /// <summary>
        /// 将 Base64 转换为图片文件，并返回图片路径
        /// </summary>
        public static string SaveBase64AsImage(string base64, string title, string basepath)
        {
            if (string.IsNullOrEmpty(base64)) return string.Empty;

            string filePath = Path.Combine(basepath, $"{title}.png");
            byte[] imageBytes = Convert.FromBase64String(base64);

            try
            {
                File.WriteAllBytes(filePath, imageBytes);
                return filePath;
            }
            catch (Exception ex)
            {
                WriteLog($"将 Base64 保存为图片 {filePath} 时遇到异常。", LogLevel.Error, ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// 计算文件哈希值
        /// </summary>
        public static string CalculateFileHash(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                WriteLog("指定的文件未找到！", LogLevel.Warning);
                return null;
            }
            try
            {
                using var sha256 = SHA256.Create();
                using var stream = new BufferedStream(File.OpenRead(filePath), 1024 * 1024); // 1MB 缓冲区
                byte[] hashBytes = sha256.ComputeHash(stream);
                StringBuilder hashStringBuilder = new(64);
                foreach (byte b in hashBytes) hashStringBuilder.Append(b.ToString("x2"));
                return hashStringBuilder.ToString();
            }
            catch (Exception ex)
            {
                WriteLog("计算文件哈希值时出现异常。", LogLevel.Error, ex);
                return null;
            }
        }

        /// <summary>
        /// 获取图片尺寸
        /// </summary>
        public static (int, int) GetImageSize(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                var frame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                return (frame.PixelWidth, frame.PixelHeight);
            }
            catch (Exception ex)
            {
                WriteLog("获取图像尺寸时遇到异常。", LogLevel.Error, ex);
                return (0, 0);
            }
        }

        /// <summary>
        /// 停止所有对指定文件的追踪进程，若路径为空则终止所有追踪进程
        /// </summary>
        /// <param name="filePath">要停止监控的文件路径，空则终止所有</param>
        /// <returns>成功终止的进程数量</returns>
        public static int StopTailProcesses(string filePath = "")
        {
            int stoppedCount = 0;
            try
            {
                // 获取所有名为 tail.exe 的进程
                Process[] tailProcesses = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(TailExePath));

                foreach (var process in tailProcesses)
                {
                    try
                    {
                        // 获取进程的完整命令行参数
                        string commandLine = GetCommandLine(process);

                        // 判断是否终止：路径为空时直接终止所有，非空时检查路径匹配
                        if (string.IsNullOrEmpty(filePath) || commandLine.ToLower().Contains(filePath.ToLower()))
                        {
                           // 尝试结束进程
                            process.Kill();
                            process.WaitForExit();
                            stoppedCount++;
                            WriteLog($"成功结束 {(string.IsNullOrEmpty(filePath) ? "所有" : $"对文件 {filePath} 的")} 追踪进程，PID为 {process.Id}。", LogLevel.Info);
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteLog($"尝试终止 PID为 {process.Id} 的文件追踪进程时遇到异常。", LogLevel.Error, ex);
                    }
                }

                if (stoppedCount == 0) WriteLog($"{(string.IsNullOrEmpty(filePath) ? "未找到正在运行的" : $"未找到对文件 {filePath} 的")} 追踪进程。", LogLevel.Warning);
                else WriteLog($"共结束 {stoppedCount} 个 {(string.IsNullOrEmpty(filePath) ? "正在运行的" : $"对文件 {filePath} 的")} 追踪进程。", LogLevel.Info);

                return stoppedCount;
            }
            catch (Exception ex)
            {
                WriteLog($"停止 {(string.IsNullOrEmpty(filePath) ? "所有" : $"对文件 {filePath} 的")} 追踪进程时遇到异常。", LogLevel.Error, ex);
                return 0;
            }
        }

        /// <summary>
        /// 追踪文件实时变化，不等待启动时返回 null
        /// </summary>
        public static Process TailFile(string filePath, string title = "TailTracking", bool waitForStart = false)
        {
            try
            {
                if (!File.Exists(filePath)) EnsureFileExists(filePath);

                // 确保 tail.exe 存在
                if (!File.Exists(TailExePath)) ExtractResourceToFile(Properties.Resources.tail, TailExePath);

                // 生成唯一标识符
                string uniqueId = Guid.NewGuid().ToString("N");

                // 创建唯一窗口标题
                string expectedTitle = $"{title}_{uniqueId}";

                // 创建临时批处理文件内容
                string tempBatchFile = Path.Combine(Path.GetTempPath(), $"run_tail_{uniqueId}.bat");
                string batchContent = $"@echo off{Environment.NewLine}" +
                                            // 预先将含中文的变量以 ANSI 格式写入环境变量中
                                            $"set \"expectedTitle={expectedTitle}\"{Environment.NewLine}" +
                                            $"set \"TailExePath={TailExePath}\"{Environment.NewLine}" +
                                            $"set \"filePath={filePath}\"{Environment.NewLine}" +
                                            // 切换到 UTF-8，确保 tail 输出中文正常
                                            $"chcp 65001>nul{Environment.NewLine}" +
                                            // 后续引用预先定义好的环境变量
                                            $"title %expectedTitle%{Environment.NewLine}" +
                                            $"\"%TailExePath%\" -f -m 0 \"%filePath%\"{Environment.NewLine}";
                File.WriteAllText(tempBatchFile, batchContent, Encoding.GetEncoding(936));
                RunTailBatFiles.Add(tempBatchFile);

                // 启动批处理文件，显示窗口
                StartProcess(tempBatchFile, useShellExecute: true, createNoWindow: false);
                WriteLog($"成功启动批处理文件 {tempBatchFile}。", LogLevel.Debug);

                if (waitForStart)
                {
                    // 轮询查找具有唯一窗口标题的追踪进程
                    const int timeout = 5000;
                    int waited = 0;
                    Process tailProcess = null;
                    while (waited < timeout)
                    {
                        // 获取所有 tail.exe 进程
                        Process[] tails = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(TailExePath));
                        foreach (var proc in tails)
                        {
                            // 检查窗口标题中是否包含唯一标识符
                            if (!string.IsNullOrEmpty(proc.MainWindowTitle) && proc.MainWindowTitle.Contains(expectedTitle))
                            {
                                tailProcess = proc;
                                break;
                            }
                        }
                        if (tailProcess != null) break;

                        Thread.Sleep(50);
                        waited += 50;
                    }

                    if (tailProcess != null) return tailProcess;
                    else
                    {
                        WriteLog("追踪进程启动超时。", LogLevel.Warning);
                        return null;
                    }
                }
                else return null;
            }
            catch (Exception ex)
            {
                WriteLog($"追踪文件 {filePath} 实时变化时遇到异常。", LogLevel.Error, ex);
                throw;
            }
        }
    }
}
