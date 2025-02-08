using System.IO;
using System.Text;

namespace SNIBypassGUI.Utils
{
    public static class IniFileUtils
    {
        [System.Runtime.InteropServices.DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);

        [System.Runtime.InteropServices.DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        /// <summary>
        /// 写入配置文件
        /// </summary>
        /// <param name="section">部分名称</param>
        /// <param name="key">键名称</param>
        /// <param name="value">值</param>
        /// <param name="path">文件路径</param>
        public static void INIWrite(string section, string key, string value, string path) => WritePrivateProfileString(section, key, value, path);

        /// <summary>
        /// 读取配置文件
        /// </summary>
        /// <param name="section">部分名称</param>
        /// <param name="key">键名称</param>
        /// <param name="path">文件路径</param>
        /// <returns>值</returns>
        public static string INIRead(string section, string key, string path)
        {
            StringBuilder temp = new StringBuilder(255);
            GetPrivateProfileString(section, key, "", temp, 255, path);
            return temp.ToString();
        }

        /// <summary>
        /// 删除配置文件
        /// </summary>
        /// <param name="FilePath">文件路径</param>
        public static void INIDelete(string FilePath)
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
    }
}
