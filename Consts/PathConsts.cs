using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace SNIBypassGUI.Consts
{
    public static class PathConsts
    {
        public static readonly string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string dataDirectory = Path.Combine(currentDirectory, "data");
        public static readonly string NginxDirectory = Path.Combine(dataDirectory, "core");
        public static readonly string FaviconsDirectory = Path.Combine(dataDirectory, "favicons");
        public static readonly string BackgroundDirectory = Path.Combine(dataDirectory, "backgrounds");
        public static readonly string nginxPath = Path.Combine(NginxDirectory, "SNIBypass.exe");
        public static readonly string nginxConfigDirectory = Path.Combine(NginxDirectory, "conf");
        public static readonly string nginxConfigFile = Path.Combine(nginxConfigDirectory, "nginx.conf");
        public static readonly string CADirectory = Path.Combine(nginxConfigDirectory, "ca");
        public static readonly string CERFile = Path.Combine(CADirectory, "ca.cer");
        public static readonly string CRTFile = Path.Combine(CADirectory, "SNIBypassCrt.crt");
        public static readonly string KeyFile = Path.Combine(CADirectory, "SNIBypassKey.key");
        public static readonly string nginxLogDirectory = Path.Combine(NginxDirectory, "logs");
        public static readonly string nginxAccessLogPath = Path.Combine(nginxLogDirectory, "access.log");
        public static readonly string nginxErrorLogPath = Path.Combine(nginxLogDirectory, "error.log");
        public static readonly string nginxTempDirectory = Path.Combine(NginxDirectory, "temp");
        public static readonly string INIPath = Path.Combine(dataDirectory, "config.ini");
        public static readonly string LogDirectory = Path.Combine(dataDirectory, "logs");
        public static readonly string SystemHosts = "C:\\Windows\\System32\\drivers\\etc\\hosts";
        public static readonly string dnsDirectory = Path.Combine(dataDirectory, "dns");
        public static readonly string AcrylicServiceExeFilePath = Path.Combine(dnsDirectory, "AcrylicService.exe");
        public static readonly string AcrylicCacheFilePath = Path.Combine(dnsDirectory, "AcrylicCache.dat");
        public static readonly string AcrylicHostsPath = Path.Combine(dnsDirectory, "AcrylicHosts.txt");
        public static readonly string AcrylicConfigurationPath = Path.Combine(dnsDirectory, "AcrylicConfiguration.ini");
        public static readonly string CustomBackground = Path.Combine(dataDirectory, "CustomBkg.png");
        public static readonly string SwitchData = Path.Combine(dataDirectory, "SwitchData.json");
        public static readonly string AcrylicHostsAll = Path.Combine(dataDirectory, "AcrylicHosts_All.dat");
        public static readonly string SystemHostsAll = Path.Combine(dataDirectory, "SystemHosts_All.dat");
        public static readonly string TailExePath = Path.Combine(dataDirectory, "tail.exe");
        public static readonly string TempDirectory = Path.Combine(Path.GetTempPath(), "SNIBypassGUI");
        public static readonly string NewVersionExe = Path.Combine(dataDirectory, "SNIBypassGUI.exe");
        public static readonly string CurrentExe = Assembly.GetExecutingAssembly().Location;
        public static readonly string OldVersionExe = Path.Combine(currentDirectory, "SNIBypassGUI.exe.old");
        public static readonly string SNIBypassGUIExeFilePath = System.Windows.Forms.Application.ExecutablePath;
        public static readonly List<string> NeccesaryDirectories = [dataDirectory, NginxDirectory, nginxConfigDirectory, CADirectory, nginxLogDirectory, nginxTempDirectory, dnsDirectory, FaviconsDirectory, BackgroundDirectory];
    }
}
