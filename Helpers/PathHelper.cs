using System;
using System.Collections.Generic;
using System.IO;

namespace SNIBypassGUI
{
    public class PathsSet
    {
        public static string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        public static string dataDirectory = Path.Combine(currentDirectory, "data");
        public static string NginxDirectory = Path.Combine(dataDirectory, "core");
        public static string nginxPath = Path.Combine(NginxDirectory, "SNIBypass.exe");
        public static string nginxConfigDirectory = Path.Combine(NginxDirectory, "conf");
        public static string nginxConfigFile_A = Path.Combine(nginxConfigDirectory, "nginx.conf");
        public static string nginxConfigFile_B = Path.Combine(nginxConfigDirectory, "bypass.conf");
        public static string nginxConfigFile_C = Path.Combine(nginxConfigDirectory, "shared-proxy-params-1.conf");
        public static string nginxConfigFile_D = Path.Combine(nginxConfigDirectory, "shared-proxy-params-2.conf");
        public static string nginxConfigFile_E = Path.Combine(nginxConfigDirectory, "cert.conf");
        public static string CADirectory = Path.Combine(nginxConfigDirectory, "ca");
        public static string CERFile = Path.Combine(CADirectory, "ca.cer");
        public static string CRTFile = Path.Combine(CADirectory, "SNIBypassCrt.crt");
        public static string KeyFile = Path.Combine(CADirectory, "SNIBypassKey.key");
        public static string nginxLogDirectory = Path.Combine(NginxDirectory, "logs");
        public static string nginxTempDirectory = Path.Combine(NginxDirectory, "temp");
        public static string nginxLogFile_A = Path.Combine(nginxLogDirectory, "access.log");
        public static string nginxLogFile_B = Path.Combine(nginxLogDirectory, "error.log");
        public static string INIPath = Path.Combine(dataDirectory, "config.ini");
        public static string GUILogDirectory = Path.Combine(dataDirectory, "logs");
        public static string GUILogPath = Path.Combine(GUILogDirectory, "GUI.log");
        public static string SystemHosts = "C:\\Windows\\System32\\drivers\\etc\\hosts";
        public static string dnsDirectory = Path.Combine(dataDirectory, "dns");
        public static string AcrylicServiceExeFilePath = Path.Combine(dnsDirectory, "AcrylicService.exe");
        public static string AcrylicDebugLogFilePath = Path.Combine(dnsDirectory, "AcrylicDebug.txt");
        public static string AcrylicCacheFilePath = Path.Combine(dnsDirectory, "AcrylicCache.dat");
        public static string AcrylicHostsPath = Path.Combine(dnsDirectory, "AcrylicHosts.txt");
        public static string AcrylicConfigurationPath = Path.Combine(dnsDirectory, "AcrylicConfiguration.ini");
        public static string CustomBackground = Path.Combine(dataDirectory, "CustomBkg.png");
        public static string SNIBypassGUIExeFilePath = System.Windows.Forms.Application.ExecutablePath;
        public static List<string> TempFilesPaths = new List<String> { nginxLogFile_A, nginxLogFile_B, AcrylicCacheFilePath };
        public static List<string> TempFilesPathsIncludingGUILog = new List<String> { nginxLogFile_A, nginxLogFile_B, AcrylicCacheFilePath,GUILogPath };
        public static List<string> NeccesaryDirectories = new List<String> { dataDirectory, NginxDirectory, nginxConfigDirectory, CADirectory, nginxLogDirectory, nginxTempDirectory, dnsDirectory};
    }

    public class LinksSet
    {
        public static string 如何使用自定义背景功能 = "https://dgithub.xyz/racpast/SNIBypassGUI/wiki/%E2%9D%93%EF%B8%8F-%E4%BD%BF%E7%94%A8%E6%97%B6%E9%81%87%E5%88%B0%E9%97%AE%E9%A2%98#%E5%A6%82%E4%BD%95%E4%BD%BF%E7%94%A8%E8%87%AA%E5%AE%9A%E4%B9%89%E8%83%8C%E6%99%AF%E5%8A%9F%E8%83%BD";
        public static string 当您无法确定当前正在使用的适配器时 = "https://dgithub.xyz/racpast/SNIBypassGUI/wiki/%E2%9D%93%EF%B8%8F-%E4%BD%BF%E7%94%A8%E6%97%B6%E9%81%87%E5%88%B0%E9%97%AE%E9%A2%98#%E5%BD%93%E6%82%A8%E6%97%A0%E6%B3%95%E7%A1%AE%E5%AE%9A%E5%BD%93%E5%89%8D%E6%AD%A3%E5%9C%A8%E4%BD%BF%E7%94%A8%E7%9A%84%E9%80%82%E9%85%8D%E5%99%A8%E6%97%B6";
        public static string 当您找不到当前正在使用的适配器或启动时遇到适配器设置失败时 = "https://dgithub.xyz/racpast/SNIBypassGUI/wiki/%E2%9D%93%EF%B8%8F-%E4%BD%BF%E7%94%A8%E6%97%B6%E9%81%87%E5%88%B0%E9%97%AE%E9%A2%98#%E5%BD%93%E6%82%A8%E6%89%BE%E4%B8%8D%E5%88%B0%E5%BD%93%E5%89%8D%E6%AD%A3%E5%9C%A8%E4%BD%BF%E7%94%A8%E7%9A%84%E9%80%82%E9%85%8D%E5%99%A8%E6%88%96%E5%90%AF%E5%8A%A8%E6%97%B6%E9%81%87%E5%88%B0%E9%80%82%E9%85%8D%E5%99%A8%E8%AE%BE%E7%BD%AE%E5%A4%B1%E8%B4%A5%E6%97%B6";
        public static string 当您在停止时遇到适配器设置失败或不确定该软件是否对适配器造成影响时 = "https://dgithub.xyz/racpast/SNIBypassGUI/wiki/%E2%9D%93%EF%B8%8F-%E4%BD%BF%E7%94%A8%E6%97%B6%E9%81%87%E5%88%B0%E9%97%AE%E9%A2%98#%E5%BD%93%E6%82%A8%E5%9C%A8%E5%81%9C%E6%AD%A2%E6%97%B6%E9%81%87%E5%88%B0%E9%80%82%E9%85%8D%E5%99%A8%E8%AE%BE%E7%BD%AE%E5%A4%B1%E8%B4%A5%E6%88%96%E4%B8%8D%E7%A1%AE%E5%AE%9A%E8%AF%A5%E8%BD%AF%E4%BB%B6%E6%98%AF%E5%90%A6%E5%AF%B9%E9%80%82%E9%85%8D%E5%99%A8%E9%80%A0%E6%88%90%E5%BD%B1%E5%93%8D%E6%97%B6";
        public static string 当您的主服务运行后自动停止或遇到80端口被占用的提示时 = "https://dgithub.xyz/racpast/SNIBypassGUI/wiki/%E2%9D%93%EF%B8%8F-%E4%BD%BF%E7%94%A8%E6%97%B6%E9%81%87%E5%88%B0%E9%97%AE%E9%A2%98/_edit#%E5%BD%93%E6%82%A8%E7%9A%84%E4%B8%BB%E6%9C%8D%E5%8A%A1%E8%BF%90%E8%A1%8C%E5%90%8E%E8%87%AA%E5%8A%A8%E5%81%9C%E6%AD%A2%E6%88%96%E9%81%87%E5%88%B080%E7%AB%AF%E5%8F%A3%E8%A2%AB%E5%8D%A0%E7%94%A8%E7%9A%84%E6%8F%90%E7%A4%BA%E6%97%B6";
        public static string 当您遇到对系统hosts的访问被拒绝的提示时 = "https://dgithub.xyz/racpast/SNIBypassGUI/wiki/%E2%9D%93%EF%B8%8F-%E4%BD%BF%E7%94%A8%E6%97%B6%E9%81%87%E5%88%B0%E9%97%AE%E9%A2%98#%E5%BD%93%E6%82%A8%E9%81%87%E5%88%B0%E5%AF%B9%E7%B3%BB%E7%BB%9Fhosts%E7%9A%84%E8%AE%BF%E9%97%AE%E8%A2%AB%E6%8B%92%E7%BB%9D%E7%9A%84%E6%8F%90%E7%A4%BA%E6%97%B6";
    }
}
