using System;
using System.IO;
using System.Threading.Tasks;
using static SNIBypassGUI.Consts.AppConsts;
using static SNIBypassGUI.Consts.ConfigConsts;
using static SNIBypassGUI.Consts.PathConsts;
using static SNIBypassGUI.Utils.ConvertUtils;
using static SNIBypassGUI.Utils.FileUtils;
using static SNIBypassGUI.Utils.IniFileUtils;
using static SNIBypassGUI.Utils.LogManager;
using static SNIBypassGUI.Utils.ProcessUtils;
using static SNIBypassGUI.Utils.ServiceUtils;
using static SNIBypassGUI.Utils.WinApiUtils;

namespace SNIBypassGUI.Utils
{
    public static class AcrylicServiceUtils
    {
        /// <summary>
        /// 检查 Acrylic DNS Proxy 服务是否已安装
        /// </summary>
        public static bool IsAcrylicServiceInstalled() => IsServiceInstalled(DnsServiceName);

        /// <summary>
        /// 安装 Acrylic DNS Proxy 服务
        /// </summary>
        public static async Task InstallAcrylicService()
        {
            if (!IsAcrylicServiceInstalled())
            {
                StartProcess(AcrylicServiceExeFilePath, "/INSTALL /SILENT");

                TimeSpan timeout = TimeSpan.FromSeconds(30);
                DateTime startTime = DateTime.Now;
                while (!IsAcrylicServiceInstalled())
                {
                    if (DateTime.Now - startTime > timeout) WriteLog("等待服务安装超时。", LogLevel.Warning);
                    await Task.Delay(1000);
                }

                // 安装服务的时候会将启动类型设置为“自动”导致开机自动启动从而窗口显示状态文本误导用户的问题，应调整为“手动”
                ChangeServiceStartType(DnsServiceName, SERVICE_DEMAND_START);
            }
        }

        /// <summary>
        /// 卸载 Acrylic DNS Proxy 服务
        /// </summary>
        public static async Task UninstallAcrylicService()
        {
            if (IsAcrylicServiceInstalled())
            {
                StartProcess(AcrylicServiceExeFilePath, "/UNINSTALL /SILENT");
                TimeSpan timeout = TimeSpan.FromSeconds(30);
                DateTime startTime = DateTime.Now;
                while (IsAcrylicServiceInstalled())
                {
                    if (DateTime.Now - startTime > timeout) WriteLog("等待服务卸载超时。", LogLevel.Warning);
                    await Task.Delay(1000);
                }
            }
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
            await Task.Run(() =>
            {
                if (!IsAcrylicServiceRunning()) StartServiceByName(DnsServiceName);
            });
        }

        /// <summary>
        /// 停止 Acrylic DNS Proxy 服务
        /// </summary>
        public static async Task  StopAcrylicService()
        {
            await Task.Run(() =>
            {
                if (IsAcrylicServiceRunning()) StopService(DnsServiceName);
            });
        }

        /// <summary>
        /// 获取命中日志路径
        /// </summary>
        public static string GetLogPath() => Path.Combine(LogDirectory, $"HitLog-{DateTime.Now:yyyy-MM-dd}.log");

        /// <summary>
        /// 检查 Acrylic DNS Proxy 服务的命中日志是否启用
        /// </summary>
        public static bool IsAcrylicServiceHitLogEnabled() => StringToBool(INIRead(AdvancedSettings, AcrylicDebug, INIPath));

        /// <summary>
        /// 清理 Acrylic DNS Proxy 服务的缓存文件
        /// </summary>
        public static void RemoveAcrylicCacheFile() => TryDelete(AcrylicCacheFilePath);

        /// <summary>
        /// 启用 Acrylic DNS Proxy 服务的命中日志
        /// </summary>
        public static void EnableAcrylicServiceHitLog()
        {
            // 记录所有内容
            INIWrite("GlobalSection", "HitLogFileWhat", "XHCFRU", AcrylicConfigurationPath);

            // 记录响应的完整转储
            INIWrite("GlobalSection", "HitLogFullDump", "Yes", AcrylicConfigurationPath);

            // 禁用日志缓冲
            INIWrite("GlobalSection", "HitLogMaxPendingHits", "0", AcrylicConfigurationPath);

            // 设置日志文件路径
            INIWrite("GlobalSection", "HitLogFileName", GetLogPath(), AcrylicConfigurationPath);
        }

        /// <summary>
        /// 禁用 Acrylic DNS Proxy 服务的命中日志
        /// </summary>
        public static void DisableAcrylicServiceHitLog() => INIWrite("GlobalSection", "HitLogFileName", string.Empty, AcrylicConfigurationPath);
    }
}
