using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using static SNIBypassGUI.Utils.LogManager;

namespace SNIBypassGUI.Utils
{
    public static class IniFileUtils
    {
        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern uint GetPrivateProfileSection(string lpAppName, byte[] lpReturnedString, uint nSize, string lpFileName);

        /// <summary>
        /// 写入配置文件
        /// </summary>
        public static void INIWrite(string section, string key, string value, string path) => WritePrivateProfileString(section, key, value, path);

        /// <summary>
        /// 读取配置文件
        /// </summary>
        public static string INIRead(string section, string key, string path)
        {
            int size = 1024; // 初始缓冲区大小
            StringBuilder temp = new(size);
            int ret = GetPrivateProfileString(section, key, "", temp, size, path);

            // 如果返回值是 size - 2 且未达到最大限制，则增加缓冲区并重试
            while (ret == size - 2 && size < 65536)
            {
                size *= 2; // 缓冲区大小加倍
                temp.Capacity = size; // 调整 StringBuilder 的容量
                ret = GetPrivateProfileString(section, key, "", temp, size, path);
            }

            // 如果达到最大大小仍被截断，记录日志
            if (ret == size - 2) WriteLog("INI 文件的值过长，无法完整读取", LogLevel.Warning);

            return temp.ToString();
        }

        /// <summary>
        /// 获取指定部分的所有键名
        /// </summary>
        public static List<string> GetKeys(string section, string path)
        {
            List<string> keys = [];
            Encoding fileEncoding = Encoding.Default;

            // 打开配置文件进行读取
            using FileStream fs = new(path, FileMode.Open, FileAccess.Read);
            using StreamReader reader = new(fs, fileEncoding);
            string line;
            bool isInSection = false;

            // 逐行读取文件
            while ((line = reader.ReadLine()) != null)
            {
                // 检查是否进入指定部分
                if (line.Trim().StartsWith("[" + section + "]"))
                {
                    isInSection = true;
                    continue;
                }

                // 如果已进入指定部分，开始提取键名
                if (isInSection)
                {
                    // 遇到另一个部分，停止读取
                    if (line.Trim().StartsWith("[") && line.Trim().EndsWith("]")) break;

                    // 检查是否是键值对
                    int equalIndex = line.IndexOf('=');
                    if (equalIndex > 0)
                    {
                        string key = line.Substring(0, equalIndex).Trim();
                        if (!string.IsNullOrEmpty(key)) keys.Add(key);
                    }
                }
            }
            return keys;
        }

        /// <summary>
        /// 删除配置文件
        /// </summary>
        public static void INIDelete(string filePath)
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }
}