using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

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
            StringBuilder temp = new(255);
            GetPrivateProfileString(section, key, "", temp, 255, path);
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