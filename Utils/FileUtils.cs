using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;
using static SNIBypassGUI.Utils.LogManager;

namespace SNIBypassGUI.Utils
{
    public static class FileUtils
    {
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
        public static void AppendToFile(string filePath, string[] linesToAdd)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    File.WriteAllLines(filePath, linesToAdd);
                    return;
                }
                using (StreamWriter writer = new(filePath, true, Encoding.UTF8))
                {
                    foreach (string line in linesToAdd)
                    {
                        writer.WriteLine(line);
                    }
                }
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
        public static void AppendToFile(string filePath, List<string> linesToAdd) => AppendToFile(filePath, linesToAdd.ToArray());

        /// <summary>
        /// 将单行内容写入指定文件末尾
        /// </summary>
        /// <param name="filePath">指定的文件路径</param>
        /// <param name="lineToAdd">需要追加的内容（单行）</param>
        public static void AppendToFile(string filePath, string lineToAdd) => AppendToFile(filePath, new[] { lineToAdd });

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
                WriteLog($"指定的文件夹路径不存在。", LogLevel.Error, ex);
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
            using (FileStream file = new(path, FileMode.Create))
            {
                file.Write(resource, 0, resource.Length);
            }
        }

        /// <summary>
        /// 获取文件大小（以字节为单位）
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public static long GetFileSize(string filePath)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(filePath);
                return fileInfo.Length;
            }
            catch (Exception ex)
            {
                WriteLog($"获取 {filePath} 文件大小时遇到错误。", LogLevel.Error, ex);
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
                string[] files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    size += GetFileSize(file);
                }
            }
            catch (Exception ex)
            {
                WriteLog($"获取 {directoryPath} 目录大小时遇到错误。", LogLevel.Error, ex);
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
                WriteLog("获取总大小时遇到错误。", LogLevel.Error, ex);
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
        /// 释放资源型的获取图像
        /// </summary>
        /// <param name="imagePath">指定图像的路径</param>
        public static BitmapImage GetImage(string imagePath)
        {
            BitmapImage bitmap = new();
            if (File.Exists(imagePath))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                using (Stream ms = new MemoryStream(File.ReadAllBytes(imagePath)))
                {
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();
                }
            }
            else
            {
                WriteLog($"文件 {imagePath} 不存在！", LogLevel.Warning);
            }
            return bitmap;
        }

        /// <summary>
        /// 从文件中移除从“#   sectionName Start”到“#   sectionName End”的部分
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="sectionName"></param>
        public static void RemoveSection(string filePath, string sectionName)
        {
            if (!File.Exists(filePath))
            {
                WriteLog($"文件{filePath}不存在！", LogLevel.Warning);
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
                if (!File.Exists(filePath)) File.Create(filePath).Dispose();
            }
            catch (Exception ex)
            {
                WriteLog($"创建文件 {filePath} 时遇到异常。", LogLevel.Error, ex);
                throw;
            }
        }
    }
}
