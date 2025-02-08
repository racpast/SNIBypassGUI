using Microsoft.Win32;
using System.IO;
using System.Threading.Tasks;
using static SNIBypassGUI.Utils.CommandUtils;
using static SNIBypassGUI.Utils.ProcessUtils;
using static SNIBypassGUI.Utils.FileUtils;
using static SNIBypassGUI.Consts.PathConsts;

namespace SNIBypassGUI.Utils
{
    public static class AcrylicServiceUtils
    {
        /// <summary>
        /// 检查 Acrylic DNS Proxy 服务是否已安装
        /// </summary>
        public static bool IsAcrylicServiceInstalled() => Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\AcrylicDNSProxySvc") != null;

        /// <summary>
        /// 安装 Acrylic DNS Proxy 服务
        /// </summary>
        public static async Task InstallAcrylicService()
        {
            if (!IsAcrylicServiceInstalled())
            {
                await RunCommand($"\"{AcrylicServiceExeFilePath}\" /INSTALL /SILENT");
                await RunCommand($"ICACLS \"{AcrylicServiceExeFilePath}\" /inheritance:d");
                await RunCommand($"ICACLS \"{AcrylicServiceExeFilePath}\" /remove:g \"Authenticated Users\"");

                // 安装服务的时候会将启动类型设置为“自动”导致开机自动启动从而窗口显示状态文本误导用户的问题，应调整为“手动”
                await RunCommand("sc config AcrylicDNSProxySvc start= demand");
            }
        }

        /// <summary>
        /// 卸载 Acrylic DNS Proxy 服务
        /// </summary>
        public static async Task UninstallAcrylicService()
        {
            if (IsAcrylicServiceInstalled()) await RunCommand($"\"{AcrylicServiceExeFilePath}\" /UNINSTALL /SILENT");
        }

        /// <summary>
        /// 检查 Acrylic DNS Proxy 服务是否正在运行
        /// </summary>
        public static bool IsAcrylicServiceRunning() => IsProcessRunning("AcrylicService");

        /// <summary>
        /// 启动 Acrylic DNS Proxy 服务
        /// </summary>
        public static async Task StartAcrylicService()
        {
            if (!IsAcrylicServiceRunning()) await RunCommand("Net.exe Start AcrylicDNSProxySvc");
        }

        /// <summary>
        /// 停止 Acrylic DNS Proxy 服务
        /// </summary>
        public static async Task StopAcrylicService()
        {
            if (IsAcrylicServiceRunning()) await RunCommand("Net.exe Stop AcrylicDNSProxySvc");
        }

        /// <summary>
        /// 检查 Acrylic DNS Proxy 服务的调试日志是否启用
        /// </summary>
        public static bool IsAcrylicServiceDebugLogEnabled() => File.Exists(AcrylicDebugLogFilePath);

        /// <summary>
        /// 清理 Acrylic DNS Proxy 服务的缓存文件
        /// </summary>
        public static void RemoveAcrylicCacheFile() => TryDelete(AcrylicCacheFilePath);

        /// <summary>
        /// 启用 Acrylic DNS Proxy 服务的调试日志
        /// </summary>
        public static void EnableAcrylicServiceDebugLog() => EnsureFileExists(AcrylicDebugLogFilePath);

        /// <summary>
        /// 禁用 Acrylic DNS Proxy 服务的调试日志
        /// </summary>
        public static void DisableAcrylicServiceDebugLog() => TryDelete(AcrylicDebugLogFilePath);
    }
}
