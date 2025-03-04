using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace SNIBypassGUI.Consts
{
    public static class PathConsts
    {
        public static string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        public static string dataDirectory = Path.Combine(currentDirectory, "data");
        public static string NginxDirectory = Path.Combine(dataDirectory, "core");
        public static string FaviconsDirectory = Path.Combine(dataDirectory, "favicons");
        public static string nginxPath = Path.Combine(NginxDirectory, "SNIBypass.exe");
        public static string nginxConfigDirectory = Path.Combine(NginxDirectory, "conf");
        public static string nginxConfigFile = Path.Combine(nginxConfigDirectory, "nginx.conf");
        public static string CADirectory = Path.Combine(nginxConfigDirectory, "ca");
        public static string CERFile = Path.Combine(CADirectory, "ca.cer");
        public static string CRTFile = Path.Combine(CADirectory, "SNIBypassCrt.crt");
        public static string KeyFile = Path.Combine(CADirectory, "SNIBypassKey.key");
        public static string nginxLogDirectory = Path.Combine(NginxDirectory, "logs");
        public static string nginxTempDirectory = Path.Combine(NginxDirectory, "temp");
        public static string nginxLogFile_A = Path.Combine(nginxLogDirectory, "access.log");
        public static string nginxLogFile_B = Path.Combine(nginxLogDirectory, "error.log");
        public static string INIPath = Path.Combine(dataDirectory, "config.ini");
        public static string LogDirectory = Path.Combine(dataDirectory, "logs");
        public static string SystemHosts = "C:\\Windows\\System32\\drivers\\etc\\hosts";
        public static string dnsDirectory = Path.Combine(dataDirectory, "dns");
        public static string AcrylicServiceExeFilePath = Path.Combine(dnsDirectory, "AcrylicService.exe");
        public static string AcrylicDebugLogFilePath = Path.Combine(dnsDirectory, "AcrylicDebug.txt");
        public static string AcrylicCacheFilePath = Path.Combine(dnsDirectory, "AcrylicCache.dat");
        public static string AcrylicHostsPath = Path.Combine(dnsDirectory, "AcrylicHosts.txt");
        public static string AcrylicConfigurationPath = Path.Combine(dnsDirectory, "AcrylicConfiguration.ini");
        public static string CustomBackground = Path.Combine(dataDirectory, "CustomBkg.png");
        public static string SwitchData = Path.Combine(dataDirectory, "SwitchData.json");
        public static string AcrylicHostsAll = Path.Combine(dataDirectory, "AcrylicHosts_All.dat");
        public static string SystemHostsAll = Path.Combine(dataDirectory, "SystemHosts_All.dat");
        public static string NewVersionExe = Path.Combine(dataDirectory, "SNIBypassGUI.exe");
        public static string CurrentExe = Assembly.GetExecutingAssembly().Location;
        public static string OldVersionExe = Path.Combine(currentDirectory, "SNIBypassGUI.exe.old");
        public static string SNIBypassGUIExeFilePath = System.Windows.Forms.Application.ExecutablePath;
        public static List<string> TempFilesPaths = [nginxLogFile_A, nginxLogFile_B, AcrylicCacheFilePath];
        public static List<string> TempFilesPathsIncludingGUILogs = [nginxLogFile_A, nginxLogFile_B, AcrylicCacheFilePath, LogDirectory];
        public static List<string> NeccesaryDirectories = [dataDirectory, NginxDirectory, nginxConfigDirectory, CADirectory, nginxLogDirectory, nginxTempDirectory, dnsDirectory, FaviconsDirectory];
    }
}
