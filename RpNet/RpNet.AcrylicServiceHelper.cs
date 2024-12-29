using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using RpNet.CMDHelper;
using static SNIBypassGUI.PathsSet;

namespace RpNet.AcrylicServiceHelper
{
    // --------------------------------------------------------------------------
    //
    //  AcrylicService 类
    //  该类提供了对 Acrylic DNS Proxy 服务的管理功能，包括安装、卸载、启动、停止服务，以及相关的日志与缓存管理。
    //  通过该类，用户可以在 C# 环境中进行对 Acrylic DNS Proxy 服务的各种操作，例如检查服务状态、安装/卸载服务、清理缓存等。
    //  （By Racpast）修改自原 Pascal 文件 `AcrylicUIUtils.pas` 的 C# 版本。
    //
    //  功能：
    //  - 检查 Acrylic DNS Proxy 服务是否已安装
    //  - 安装与卸载 Acrylic DNS Proxy 服务
    //  - 启动与停止 Acrylic DNS Proxy 服务
    //  - 操作调试日志（启用/禁用日志、删除日志文件）
    //  - 清理缓存文件
    //
    //  注意：
    //  - 本类的方法多数为异步方法（`async`），使用 `Task` 来执行异步操作，以便在并发环境中提高性能。
    //  - 使用时请确保目标路径配置正确，相关的文件权限与访问权限已设置好。
    //
    // --------------------------------------------------------------------------
    public class AcrylicService
    {
        private static bool ProcessExists(string exeFileName)
        {
            Process[] ps = Process.GetProcessesByName(exeFileName);
            if (ps.Length > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static bool AcrylicServiceIsInstalled()
        {
            using (var regKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\AcrylicDNSProxySvc"))
            {
                return regKey != null;
            }
        }

        public static async Task<bool> InstallAcrylicService()
        {
            if (!AcrylicServiceIsInstalled())
            {
                bool result = (await CMD.RunCommand($"\"{AcrylicServiceExeFilePath}\" /INSTALL /SILENT")).Success;
                if (result)
                {
                    await CMD.RunCommand($"ICACLS \"{AcrylicServiceExeFilePath}\" /inheritance:d");
                    await CMD.RunCommand($"ICACLS \"{AcrylicServiceExeFilePath}\" /remove:g \"Authenticated Users\"");
                    // 安装服务的时候会将启动类型设置为“自动”导致开机自动启动从而窗口显示状态文本误导用户的问题，应调整为“手动”
                    await CMD.RunCommand("sc config AcrylicDNSProxySvc start= demand");
                }
                return result;
            }
            return true;
        }

        public static async Task<bool> UninstallAcrylicService()
        {
            if (AcrylicServiceIsInstalled())
            {
                return (await CMD.RunCommand($"\"{AcrylicServiceExeFilePath}\" /UNINSTALL /SILENT")).Success;
            }
            return true;
        }

        public static bool AcrylicServiceIsRunning()
        {
            return ProcessExists("AcrylicService");
        }

        public static async Task<bool> StartAcrylicService()
        {
            if (!AcrylicServiceIsRunning())
            {
                return (await CMD.RunCommand("Net.exe Start AcrylicDNSProxySvc")).Success;
            }
            return true;
        }

        public static async Task<bool> StopAcrylicService()
        {
            if (AcrylicServiceIsRunning())
            {
                return (await CMD.RunCommand("Net.exe Stop AcrylicDNSProxySvc")).Success;
            }
            return true;
        }

        public static bool AcrylicServiceDebugLogIsEnabled()
        {
            return File.Exists(AcrylicDebugLogFilePath);
        }

        public static void RemoveAcrylicCacheFile()
        {
            if (File.Exists(AcrylicCacheFilePath))
            {
                File.Delete(AcrylicCacheFilePath);
            }
        }

        public static void CreateAcrylicServiceDebugLog()
        {
            if (!File.Exists(AcrylicDebugLogFilePath))
            {
                File.Create(AcrylicDebugLogFilePath).Dispose();
            }
        }

        public static void RemoveAcrylicServiceDebugLog()
        {
            if (File.Exists(AcrylicDebugLogFilePath))
            {
                File.Delete(AcrylicDebugLogFilePath);
            }
        }
    }
}
