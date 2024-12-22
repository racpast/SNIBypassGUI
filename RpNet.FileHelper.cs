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
        // 搜索以 "CustomBkg" 开头的文件，并返回第一个找到的文件的路径
        public static string FindCustomBkg()
        {
            WriteLog($"FindCustomBkg()被调用。", LogLevel.Debug);

            string filePath = null;
            // 遍历目标目录中的所有文件
            foreach (var file in Directory.GetFiles(dataDirectory))
            {
                // 获取文件名（不包括路径）
                var fileName = Path.GetFileName(file);
                // 检查文件名是否以 "CustomBkg" 开头
                if (fileName.StartsWith("CustomBkg", StringComparison.OrdinalIgnoreCase))
                {
                    // 找到符合条件的文件，返回其路径
                    filePath = file;
                    break; // 只需要第一个找到的文件，退出循环
                }
            }

            WriteLog($"FindCustomBkg()完成，返回{filePath}。", LogLevel.Debug);

            return filePath; // 如果没有找到文件，则返回 null
        }

        // 释放资源型的图像调用方法
        public static BitmapImage GetImage(string imagePath)
        {
            WriteLog($"GetImage(string imagePath)被调用，参数imagePath：{imagePath}。", LogLevel.Debug);

            BitmapImage bitmap = new BitmapImage();
            if (File.Exists(imagePath))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                using (Stream ms = new MemoryStream(File.ReadAllBytes(imagePath)))
                {
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();  // 在这里释放资源  
                }
            }

            WriteLog($"GetImage(string imagePath)完成，返回{bitmap}。", LogLevel.Debug);

            return bitmap;
        }

        // 用于移除文件中从“#   sectionName Start”到“#   sectionName End”部分的方法，用来操作 hosts
        public static void RemoveSection(string filePath, string sectionName)
        {
            WriteLog($"RemoveSection(string filePath, string sectionName)被调用，参数filePath：{filePath}，参数sectionName：{sectionName}。", LogLevel.Debug);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"文件{filePath}不存在！", filePath);
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

            WriteLog($"RemoveSection(string filePath, string sectionName)完成。", LogLevel.Debug);
        }

        // 用于把string[] linesToWrite写入一个文件的方法
        public static void WriteLinesToFile(string[] linesToWrite, string filePath)
        {
            WriteLog($"WriteLinesToFile(string[] linesToWrite, string filePath)被调用，参数string[] linesToWrite：{linesToWrite}，参数filePath：{filePath}。", LogLevel.Debug);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"文件{filePath}不存在！", filePath);
            }

            using (StreamWriter writer = new StreamWriter(filePath, true, Encoding.UTF8))
            {
                foreach (string line in linesToWrite)
                {
                    writer.WriteLine(line);
                }
            }

            WriteLog($"WriteLinesToFile(string[] linesToWrite, string filePath)完成。", LogLevel.Debug);
        }

        /// <summary>
        /// 释放resx里面的普通类型文件
        /// </summary>
        /// <param name="resource">resx里面的资源</param>
        /// <param name="path">释放到的路径</param>
        public static void ExtractNormalFileInResx(byte[] resource, String path)
        {
            WriteLog($"ExtractNormalFileInResx(byte[] resource, String path)被调用，参数resource：{resource}，参数path：{path}。", LogLevel.Debug);

            FileStream file = new FileStream(path, FileMode.Create);
            file.Write(resource, 0, resource.Length);
            file.Flush();
            file.Close();

            WriteLog($"ExtractNormalFileInResx(byte[] resource, String path)完成。", LogLevel.Debug);
        }

        // 用于计算给定文件路径列表中的文件总大小（以MB为单位）的静态方法
        public static double GetTotalFileSizeInMB(List<string> filePaths)
        {
            WriteLog($"GetTotalFileSizeInMB(List<string> filePaths)被调用，参数filePaths：{filePaths}。", LogLevel.Debug);

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

            WriteLog($"GetTotalFileSizeInMB(List<string> filePaths)完成，返回{totalSizeInMB}。", LogLevel.Debug);

            // 返回文件总大小（以MB为单位）
            return totalSizeInMB;
        }

        // 该方法用于确保指定路径的目录存在，如果目录不存在，则创建它
        public static void EnsureDirectoryExists(string path)
        {
            WriteLog($"EnsureDirectoryExists(string path)被调用，参数path：{path}。", LogLevel.Debug);

            // 如果目录不存在
            if (!Directory.Exists(path))
            {
                WriteLog($"目录{path}不存在，创建目录。", LogLevel.Info);

                // 创建目录
                Directory.CreateDirectory(path);
            }
            // 如果目录已存在，则不执行任何操作

            WriteLog($"EnsureDirectoryExists(string path)完成。", LogLevel.Debug);
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
        private static extern int GetPrivateProfileString(string section, string key, string def, System.Text.StringBuilder retVal, int size, string filePath);


        // 写入INI的方法
        public void INIWrite(string section, string key, string value, string path)
        {
            WriteLog($"INIWrite(string section, string key, string value, string path)被调用，参数section：{section}，参数key：{key}，参数value：{value}，参数path：{path}。", LogLevel.Debug);

            // section=配置节点名称，key=键名，value=返回键值，path=路径
            WritePrivateProfileString(section, key, value, path);

            WriteLog($"INIWrite(string section, string key, string value, string path)完成。", LogLevel.Debug);
        }

        //读取INI的方法
        public string INIRead(string section, string key, string path)
        {
            WriteLog($"INIRead(string section, string key, string path)被调用，参数section：{section}，参数key：{key}，参数path：{path}。", LogLevel.Debug);

            // 每次从ini中读取多少字节
            System.Text.StringBuilder temp = new System.Text.StringBuilder(255);

            // section=配置节点名称，key = 键名，temp = 上面，path = 路径
            GetPrivateProfileString(section, key, "", temp, 255, path);

            WriteLog($"INIRead(string section, string key, string path)完成，返回{temp}。", LogLevel.Debug);

            return temp.ToString();
        }

        //删除一个INI文件
        public void INIDelete(string FilePath)
        {
            WriteLog($"INIDelete(string FilePath)被调用，参数FilePath：{FilePath}。", LogLevel.Debug);

            File.Delete(FilePath);

            WriteLog($"INIDelete(string FilePath)完成。", LogLevel.Debug);
        }

    }
}
