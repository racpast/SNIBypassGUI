using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace SNIBypassGUI.Consts
{
    /// <summary>
    /// Defines all file system paths used by the application.
    /// </summary>
    public static class PathConsts
    {
        /// <summary>
        /// The base directory where the application is running.
        /// </summary>
        public static readonly string CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>
        /// The main data directory.
        /// </summary>
        public static readonly string DataDirectory = Path.Combine(CurrentDirectory, "Data");

        /// <summary>
        /// The temporary directory for the application in %TEMP%.
        /// </summary>
        public static readonly string TempDirectory = Path.Combine(Path.GetTempPath(), "SNIBypassGUI");

        #region Nginx

        public static readonly string NginxDirectory = Path.Combine(DataDirectory, "Core");
        public static readonly string Nginx = Path.Combine(NginxDirectory, "SNIBypass.exe");

        public static readonly string NginxConfigDirectory = Path.Combine(NginxDirectory, "conf");
        public static readonly string NginxConfig = Path.Combine(NginxConfigDirectory, "nginx.conf");

        public static readonly string NginxLogsDirectory = Path.Combine(NginxDirectory, "logs");
        public static readonly string NginxAccessLog = Path.Combine(NginxLogsDirectory, "access.log");
        public static readonly string NginxErrorLog = Path.Combine(NginxLogsDirectory, "error.log");

        public static readonly string NginxTempDirectory = Path.Combine(NginxDirectory, "temp");
        public static readonly string NginxCacheDirectory = Path.Combine(NginxDirectory, "cache");

        // Certificates
        public static readonly string CADirectory = Path.Combine(NginxConfigDirectory, "ca");
        public static readonly string CA = Path.Combine(CADirectory, "ca.cer");
        public static readonly string SNIBypassCrt = Path.Combine(CADirectory, "SNIBypassCrt.crt");
        public static readonly string SNIBypassKey = Path.Combine(CADirectory, "SNIBypassKey.key");

        #endregion

        #region DNS (Acrylic)

        public static readonly string DnsDirectory = Path.Combine(DataDirectory, "Acrylic");
        public static readonly string AcrylicServiceExe = Path.Combine(DnsDirectory, "AcrylicService.exe");
        public static readonly string AcrylicCache = Path.Combine(DnsDirectory, "AcrylicCache.dat");
        public static readonly string AcrylicHosts = Path.Combine(DnsDirectory, "AcrylicHosts.txt");
        public static readonly string AcrylicConfig = Path.Combine(DnsDirectory, "AcrylicConfiguration.ini");
        public static readonly string AcrylicConfigTemplate = Path.Combine(DnsDirectory, "AcrylicConfiguration.Template.ini");

        #endregion

        #region App Data

        public static readonly string ConfigJson = Path.Combine(DataDirectory, "Config.json");
        public static readonly string LogDirectory = Path.Combine(DataDirectory, "Logs");
        public static readonly string BackgroundDirectory = Path.Combine(DataDirectory, "Backgrounds");
        public static readonly string CustomBackground = Path.Combine(DataDirectory, "CustomBkg.png");
        public static readonly string ProxyRules = Path.Combine(DataDirectory, "ProxyRules.json");
        public static readonly string TailExe = Path.Combine(DataDirectory, "Tail.exe");

        #endregion

        #region System & Update

        /// <summary>
        /// The system hosts file path.
        /// Usually: C:\Windows\System32\drivers\etc\hosts
        /// </summary>
        public static readonly string SystemHosts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");

        public static readonly string UpdateDirectory = Path.Combine(TempDirectory, "Update");
        public static readonly string NewVersionExe = Path.Combine(UpdateDirectory, "SNIBypassGUI.exe");
        public static readonly string OldVersionExe = Path.Combine(CurrentDirectory, "SNIBypassGUI.exe.old");

        /// <summary>
        /// The full path of the executable that started the application.
        /// </summary>
        public static readonly string CurrentExe = Application.ExecutablePath;

        #endregion

        /// <summary>
        /// A read-only list of directories that must exist for the application to function.
        /// </summary>
        public static readonly IReadOnlyList<string> NecessaryDirectories =
        [
            DataDirectory,
            NginxDirectory,
            NginxConfigDirectory,
            CADirectory,
            NginxLogsDirectory,
            NginxTempDirectory,
            DnsDirectory,
            BackgroundDirectory,
            LogDirectory,
            TempDirectory
        ];
    }
}
