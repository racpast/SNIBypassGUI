using System;
using System.IO;
using static SNIBypassGUI.LogHelper;
using System.Windows.Media.Imaging;
using System.Text;
using static SNIBypassGUI.PathsSet;
using System.Collections.Generic;

namespace RpNet.FileHelper
{
    public class FileHelper
    {
        // 释放资源型的图像调用方法
        public static BitmapImage GetImage(string imagePath)
        {
            BitmapImage bitmap = new BitmapImage();
            if (File.Exists(imagePath))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                using (Stream ms = new MemoryStream(File.ReadAllBytes(imagePath)))
                {
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();  // 释放资源  
                }
            }
            return bitmap;
        }

        // 用于移除文件中从“#   sectionName Start”到“#   sectionName End”部分的方法，用来操作 hosts
        public static void RemoveSection(string filePath, string sectionName)
        {
            WriteLog($"进入RemoveSection。", LogLevel.Debug);

            if (!File.Exists(filePath))
            {
                WriteLog($"文件{filePath}不存在！", LogLevel.Warning);

                return;
            }

            string startMarker = $"#\t{sectionName} Start";
            string endMarker = $"#\t{sectionName} End";
            StringBuilder newContent = new StringBuilder();
            bool isRemoving = false;
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
                else if (!isRemoving)
                {
                    newContent.AppendLine(line);
                }
            }
            File.WriteAllText(filePath, newContent.ToString());

            WriteLog($"完成RemoveSection。", LogLevel.Debug);
        }

        // 用于把string[] linesToWrite写入一个文件尾部的方法
        public static void WriteLinesToFileEnd(string[] linesToWrite, string filePath)
        {
            WriteLog($"进入WriteLinesToFileEnd。", LogLevel.Debug);

            if (!File.Exists(filePath))
            {
                WriteLog($"文件{filePath}不存在！", LogLevel.Warning);

                return;
            }

            using (StreamWriter writer = new StreamWriter(filePath, true, Encoding.UTF8))
            {
                foreach (string line in linesToWrite)
                {
                    writer.WriteLine(line);
                }
            }

            WriteLog($"完成WriteLinesToFileEnd。", LogLevel.Debug);
        }

        // 用于把string[] linesToWrite写入一个文件顶部的方法
        public static void WriteLinesToFileTop(string[] linesToAdd, string filePath)
        {
            WriteLog($"进入WriteLinesToFileTop。", LogLevel.Debug);

            // 如果文件不存在，直接创建它
            if (!File.Exists(filePath))
            {
                File.WriteAllLines(filePath, linesToAdd);
                return;
            }

            // 读取现有文件的内容
            string[] existingLines = File.ReadAllLines(filePath);

            // 将新的内容（linesToAdd）和现有内容拼接
            var allLines = new string[linesToAdd.Length + existingLines.Length];
            linesToAdd.CopyTo(allLines, 0);
            existingLines.CopyTo(allLines, linesToAdd.Length);

            // 将合并后的内容写回文件
            File.WriteAllLines(filePath, allLines);

            WriteLog($"完成WriteLinesToFileTop。", LogLevel.Debug);
        }

        /// <summary>
        /// 释放resx里面的普通类型文件
        /// </summary>
        /// <param name="resource">resx里面的资源</param>
        /// <param name="path">释放到的路径</param>
        public static void ExtractNormalFileInResx(byte[] resource, String path)
        {
            FileStream file = new FileStream(path, FileMode.Create);
            file.Write(resource, 0, resource.Length);
            file.Flush();
            file.Close();
        }

        // 用于计算给定文件路径列表中的文件总大小（以MB为单位）的静态方法
        public static double GetTotalFileSizeInMB(List<string> filePaths)
        {
            // 定义一个变量来存储文件总大小（以字节为单位）
            long totalSizeInBytes = 0;
            // 遍历给定的文件路径列表
            foreach (string filePath in filePaths)
            {
                if (File.Exists(filePath))
                {
                    // 使用文件路径创建一个FileInfo对象，该对象提供有关文件的详细信息
                    FileInfo fileInfo = new FileInfo(filePath);
                    // 将当前文件的长度（以字节为单位）添加到总大小中
                    totalSizeInBytes += fileInfo.Length;
                }
            }
            // 将总大小（以字节为单位）转换为MB，并保留两位小数
            double totalSizeInMB = Math.Round((double)totalSizeInBytes / (1024 * 1024), 2);

            // 返回文件总大小（以MB为单位）
            return totalSizeInMB;
        }

        // 该方法用于确保指定路径的目录存在，如果目录不存在，则创建它
        public static void EnsureDirectoryExists(string path)
        {
            WriteLog($"进入EnsureDirectoryExists。", LogLevel.Debug);

            // 如果目录不存在
            if (!Directory.Exists(path))
            {
                WriteLog($"目录{path}不存在，创建目录。", LogLevel.Info);

                // 创建目录
                Directory.CreateDirectory(path);
            }
            // 如果目录已存在，则不执行任何操作

            WriteLog($"完成EnsureDirectoryExists。", LogLevel.Debug);
        }
    }

    // 操作配置文件的类
    public class FilesINI
    {
        // 声明INI文件的写操作函数 WritePrivateProfileString()
        [System.Runtime.InteropServices.DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);

        // 声明INI文件的读操作函数 GetPrivateProfileString()
        [System.Runtime.InteropServices.DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);


        // 写入INI的方法
        public void INIWrite(string section, string key, string value, string path)
        {
            // section=配置节点名称，key=键名，value=返回键值，path=路径
            WritePrivateProfileString(section, key, value, path);
        }

        //读取INI的方法
        public string INIRead(string section, string key, string path)
        {
            // 每次从ini中读取多少字节
            StringBuilder temp = new StringBuilder(255);

            // section=配置节点名称，key = 键名，temp = 上面，path = 路径
            GetPrivateProfileString(section, key, "", temp, 255, path);

            return temp.ToString();
        }

        //删除一个INI文件
        public void INIDelete(string FilePath)
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
    }
}
