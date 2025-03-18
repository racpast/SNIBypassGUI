using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace SNIBypassGUI.Consts
{
    public static class PathConsts
    {
        public readonly static string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        public readonly static string dataDirectory = Path.Combine(currentDirectory, "data");
        public readonly static string NginxDirectory = Path.Combine(dataDirectory, "core");
        public readonly static string FaviconsDirectory = Path.Combine(dataDirectory, "favicons");
        public readonly static string BackgroundDirectory = Path.Combine(dataDirectory, "backgrounds");
        public readonly static string nginxPath = Path.Combine(NginxDirectory, "SNIBypass.exe");
        public readonly static string nginxConfigDirectory = Path.Combine(NginxDirectory, "conf");
        public readonly static string nginxConfigFile = Path.Combine(nginxConfigDirectory, "nginx.conf");
        public readonly static string CADirectory = Path.Combine(nginxConfigDirectory, "ca");
        public readonly static string CERFile = Path.Combine(CADirectory, "ca.cer");
        public readonly static string CRTFile = Path.Combine(CADirectory, "SNIBypassCrt.crt");
        public readonly static string KeyFile = Path.Combine(CADirectory, "SNIBypassKey.key");
        public readonly static string nginxLogDirectory = Path.Combine(NginxDirectory, "logs");
        public readonly static string nginxTempDirectory = Path.Combine(NginxDirectory, "temp");
        public readonly static string nginxLogFile_A = Path.Combine(nginxLogDirectory, "access.log");
        public readonly static string nginxLogFile_B = Path.Combine(nginxLogDirectory, "error.log");
        public readonly static string INIPath = Path.Combine(dataDirectory, "config.ini");
        public readonly static string LogDirectory = Path.Combine(dataDirectory, "logs");
        public readonly static string SystemHosts = "C:\\Windows\\System32\\drivers\\etc\\hosts";
        public readonly static string dnsDirectory = Path.Combine(dataDirectory, "dns");
        public readonly static string AcrylicServiceExeFilePath = Path.Combine(dnsDirectory, "AcrylicService.exe");
        public readonly static string AcrylicDebugLogFilePath = Path.Combine(dnsDirectory, "AcrylicDebug.txt");
        public readonly static string AcrylicCacheFilePath = Path.Combine(dnsDirectory, "AcrylicCache.dat");
        public readonly static string AcrylicHostsPath = Path.Combine(dnsDirectory, "AcrylicHosts.txt");
        public readonly static string AcrylicConfigurationPath = Path.Combine(dnsDirectory, "AcrylicConfiguration.ini");
        public readonly static string CustomBackground = Path.Combine(dataDirectory, "CustomBkg.png");
        public readonly static string SwitchData = Path.Combine(dataDirectory, "SwitchData.json");
        public readonly static string AcrylicHostsAll = Path.Combine(dataDirectory, "AcrylicHosts_All.dat");
        public readonly static string SystemHostsAll = Path.Combine(dataDirectory, "SystemHosts_All.dat");
        public readonly static string NewVersionExe = Path.Combine(dataDirectory, "SNIBypassGUI.exe");
        public readonly static string CurrentExe = Assembly.GetExecutingAssembly().Location;
        public readonly static string OldVersionExe = Path.Combine(currentDirectory, "SNIBypassGUI.exe.old");
        public readonly static string SNIBypassGUIExeFilePath = System.Windows.Forms.Application.ExecutablePath;
        public readonly static List<string> TempFilesPaths = [nginxLogFile_A, nginxLogFile_B, AcrylicCacheFilePath];
        public readonly static List<string> TempFilesPathsIncludingGUILogs = [nginxLogFile_A, nginxLogFile_B, AcrylicCacheFilePath, LogDirectory];
        public readonly static List<string> NeccesaryDirectories = [dataDirectory, NginxDirectory, nginxConfigDirectory, CADirectory, nginxLogDirectory, nginxTempDirectory, dnsDirectory, FaviconsDirectory, BackgroundDirectory];
    }
}
