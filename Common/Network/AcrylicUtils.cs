using System;
using System.IO;
using System.Threading.Tasks;
using SNIBypassGUI.Common.Interop;
using SNIBypassGUI.Common.IO;
using SNIBypassGUI.Common.System;
using SNIBypassGUI.Consts;
using SNIBypassGUI.Services;
using static SNIBypassGUI.Common.LogManager;

namespace SNIBypassGUI.Common.Network
{
    public static class AcrylicUtils
    {
        /// <summary>
        /// Checks if the Acrylic DNS Proxy service is installed.
        /// </summary>
        public static bool IsAcrylicServiceInstalled() => ServiceUtils.CheckServiceState(AppConsts.DnsServiceName) == 1;

        /// <summary>
        /// Installs the Acrylic DNS Proxy service.
        /// </summary>
        public static async Task InstallAcrylicServiceAsync()
        {
            if (!IsAcrylicServiceInstalled())
            {
                ProcessUtils.StartProcess(PathConsts.AcrylicServiceExe, "/INSTALL /SILENT");

                TimeSpan timeout = TimeSpan.FromSeconds(30);
                DateTime startTime = DateTime.Now;

                while (!IsAcrylicServiceInstalled())
                {
                    if (DateTime.Now - startTime > timeout)
                    {
                        WriteLog("Timeout waiting for service installation.", LogLevel.Warning);
                        break;
                    }
                    await Task.Delay(1000);
                }

                // The installation sets the start type to "Automatic" by default. 
                // Adjust to "Manual" to prevent unexpected autostarts.
                ServiceUtils.ChangeServiceStartType(AppConsts.DnsServiceName, Advapi32.SERVICE_DEMAND_START);
            }
        }

        /// <summary>
        /// Uninstalls the Acrylic DNS Proxy service.
        /// </summary>
        public static async Task UninstallAcrylicServiceAsync()
        {
            if (ServiceUtils.CheckServiceState(AppConsts.DnsServiceName) == 0) return;

            // Ensure the process is killed before uninstalling
            if (ProcessUtils.IsProcessRunning("AcrylicService"))
            {
                ProcessUtils.KillProcess("AcrylicService");
                await Task.Delay(500);
            }

            ServiceUtils.StopService(AppConsts.DnsServiceName);

            // Execute uninstall command silently
            ProcessUtils.StartProcess(PathConsts.AcrylicServiceExe, "/UNINSTALL /SILENT", workingDirectory: null, createNoWindow: true);

            TimeSpan timeout = TimeSpan.FromSeconds(30);
            DateTime startTime = DateTime.Now;

            int currentState = ServiceUtils.CheckServiceState(AppConsts.DnsServiceName);
            while (currentState != 0)
            {
                if (DateTime.Now - startTime > timeout)
                {
                    WriteLog("Timeout waiting for service uninstallation.", LogLevel.Warning);
                    break;
                }
                await Task.Delay(1000);
                currentState = ServiceUtils.CheckServiceState(AppConsts.DnsServiceName);
            }
        }

        /// <summary>
        /// Checks if the Acrylic DNS Proxy service is currently running.
        /// </summary>
        public static bool IsAcrylicServiceRunning() => ProcessUtils.IsProcessRunning("AcrylicService");

        /// <summary>
        /// Starts the Acrylic DNS Proxy service.
        /// </summary>
        public static async Task StartAcrylicService()
        {
            await Task.Run(() =>
            {
                if (!IsAcrylicServiceRunning())
                    ServiceUtils.StartServiceByName(AppConsts.DnsServiceName);
            });
        }

        /// <summary>
        /// Stops the Acrylic DNS Proxy service.
        /// </summary>
        public static async Task StopAcrylicService()
        {
            await Task.Run(() =>
            {
                if (IsAcrylicServiceRunning())
                    ServiceUtils.StopService(AppConsts.DnsServiceName);
            });
        }

        /// <summary>
        /// Gets the path for the hit log file.
        /// </summary>
        public static string GetLogPath() => Path.Combine(PathConsts.LogDirectory, $"HitLog-{DateTime.Now:yyyy-MM-dd}.log");

        /// <summary>
        /// Checks if the hit log is enabled in the App Configuration.
        /// </summary>
        public static bool IsAcrylicServiceHitLogEnabled() => ConfigManager.Instance.Settings.Advanced.AcrylicDebug;

        /// <summary>
        /// Removes the cache file of the Acrylic DNS Proxy service.
        /// </summary>
        public static void RemoveAcrylicCacheFile() => FileUtils.TryDelete(PathConsts.AcrylicCache);

        /// <summary>
        /// Enables the hit log for the Acrylic DNS Proxy service by modifying its INI config.
        /// </summary>
        public static void EnableAcrylicServiceHitLog()
        {
            // Log everything
            IniUtils.WriteString(AcrylicConsts.GlobalSection, AcrylicConsts.HitLogFileWhat, "XHCFRU", PathConsts.AcrylicConfig);

            // Log full dump of the response
            IniUtils.WriteString(AcrylicConsts.GlobalSection, AcrylicConsts.HitLogFullDump, "Yes", PathConsts.AcrylicConfig);

            // Disable log buffering
            IniUtils.WriteString(AcrylicConsts.GlobalSection, AcrylicConsts.HitLogMaxPendingHits, "0", PathConsts.AcrylicConfig);

            // Set the log file path
            IniUtils.WriteString(AcrylicConsts.GlobalSection, AcrylicConsts.HitLogFileName, GetLogPath(), PathConsts.AcrylicConfig);
        }

        /// <summary>
        /// Disables the hit log for the Acrylic DNS Proxy service.
        /// </summary>
        public static void DisableAcrylicServiceHitLog() =>
            IniUtils.WriteString(AcrylicConsts.GlobalSection, AcrylicConsts.HitLogFileName, string.Empty, PathConsts.AcrylicConfig);
    }
}
