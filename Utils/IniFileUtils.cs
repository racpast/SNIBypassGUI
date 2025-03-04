using System;
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
            byte[] buffer = new byte[32767]; // 32KB 缓冲区
            uint bytesReturned = GetPrivateProfileSection(section, buffer, (uint)buffer.Length, path);

            List<string> keys = [];
            if (bytesReturned > 0)
            {
                string sectionData = Encoding.Unicode.GetString(buffer, 0, (int)bytesReturned).Trim('\0');
                string[] entries = sectionData.Split(new char[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string entry in entries)
                {
                    int equalIndex = entry.IndexOf('=');
                    if (equalIndex > 0)
                    {
                        keys.Add(entry.Substring(0, equalIndex));
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