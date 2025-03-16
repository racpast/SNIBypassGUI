using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;
using static SNIBypassGUI.Utils.WinApiUtils;
using static SNIBypassGUI.Utils.LogManager;

namespace SNIBypassGUI.Utils
{
    /// <summary>
    /// 提供安装、卸载、配置、启动、停止 Windows 服务的通用方法，调用这些方法需要管理员权限。
    /// </summary>
    public static class ServiceUtils
    {
        /// <summary>
        /// 安装服务，并尝试启动服务。
        /// </summary>
        /// <param name="serviceExePath">服务可执行文件完整路径</param>
        /// <param name="serviceName">服务名称（注册时使用）</param>
        /// <param name="displayName">服务显示名称</param>
        /// <param name="startType">启动类型，默认为自动启动，可传入 SERVICE_DEMAND_START 表示手动启动</param>
        /// <returns>安装成功返回 true；否则返回 false</returns>
        public static bool InstallService(string serviceExePath, string serviceName, string displayName, uint startType = SERVICE_AUTO_START)
        {
            IntPtr scm = OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
            if (scm == IntPtr.Zero)
            {
                WriteLog("与服务控制管理器建立连接失败：" + Marshal.GetLastWin32Error(), LogLevel.Error);
                return false;
            }

            IntPtr svc = CreateService(
                scm,
                serviceName,
                displayName,
                SERVICE_ALL_ACCESS,
                SERVICE_WIN32_OWN_PROCESS,
                startType,
                SERVICE_ERROR_NORMAL,
                serviceExePath,
                null,
                IntPtr.Zero,
                null,
                null,
                null);

            if (svc == IntPtr.Zero)
            {
                WriteLog("创建服务失败：" + Marshal.GetLastWin32Error() + "。", LogLevel.Error);
                CloseServiceHandle(scm);
                return false;
            }

            // 尝试启动服务
            bool startResult = StartService(svc, 0, null);
            if (!startResult)
            {
                int err = Marshal.GetLastWin32Error();
                WriteLog($"启动服务失败：{err}。", LogLevel.Error);
            }

            CloseServiceHandle(svc);
            CloseServiceHandle(scm);
            return true;
        }

        /// <summary>
        /// 卸载指定名称的服务。如果服务正在运行，会先尝试停止服务。
        /// </summary>
        /// <param name="serviceName">服务名称</param>
        /// <returns>卸载成功返回 true；否则返回 false</returns>
        public static bool UninstallService(string serviceName)
        {
            // 如果服务正在运行，先尝试停止
            StopService(serviceName);

            IntPtr scm = OpenSCManager(null, null, GENERIC_WRITE);
            if (scm == IntPtr.Zero)
            {
                WriteLog("与服务控制管理器建立连接失败：" + Marshal.GetLastWin32Error() + "。", LogLevel.Error);
                return false;
            }

            IntPtr svc = OpenService(scm, serviceName, DELETE);
            if (svc == IntPtr.Zero)
            {
                WriteLog("打开服务失败：" + Marshal.GetLastWin32Error() + "。", LogLevel.Error);
                CloseServiceHandle(scm);
                return false;
            }

            bool result = DeleteService(svc);
            if (!result)
            {
                WriteLog("删除服务失败：" + Marshal.GetLastWin32Error() + "。", LogLevel.Error);
            }

            CloseServiceHandle(svc);
            CloseServiceHandle(scm);
            return result;
        }

        /// <summary>
        /// 修改指定服务的启动类型。建议使用 SERVICE_DEMAND_START 表示手动启动。
        /// </summary>
        /// <param name="serviceName">服务名称</param>
        /// <param name="startType">新启动类型（例如 SERVICE_DEMAND_START 或 SERVICE_AUTO_START）</param>
        /// <returns>修改成功返回 true；否则返回 false</returns>
        public static bool ChangeServiceStartType(string serviceName, uint startType)
        {
            IntPtr scm = OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
            if (scm == IntPtr.Zero)
            {
                WriteLog("与服务控制管理器建立连接失败：" + Marshal.GetLastWin32Error() + "。", LogLevel.Error);
                return false;
            }

            IntPtr svc = OpenService(scm, serviceName, SERVICE_CHANGE_CONFIG);
            if (svc == IntPtr.Zero)
            {
                WriteLog("打开服务失败：" + Marshal.GetLastWin32Error() + "。", LogLevel.Error);
                CloseServiceHandle(scm);
                return false;
            }

            bool result = ChangeServiceConfig(
                svc,
                SERVICE_NO_CHANGE,
                startType,
                SERVICE_NO_CHANGE,
                null,
                null,
                IntPtr.Zero,
                null,
                null,
                null,
                null);

            if (!result)
            {
                WriteLog("更改服务配置失败：" + Marshal.GetLastWin32Error() + "。", LogLevel.Error);
            }

            CloseServiceHandle(svc);
            CloseServiceHandle(scm);
            return result;
        }

        /// <summary>
        /// 启动指定名称的服务。
        /// </summary>
        /// <param name="serviceName">服务名称</param>
        /// <returns>启动成功返回 true；否则返回 false</returns>
        public static bool StartServiceByName(string serviceName)
        {
            IntPtr scm = OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
            if (scm == IntPtr.Zero)
            {
                WriteLog("与服务控制管理器建立连接失败：" + Marshal.GetLastWin32Error() + "。", LogLevel.Error);
                return false;
            }

            IntPtr svc = OpenService(scm, serviceName, SERVICE_ALL_ACCESS);
            if (svc == IntPtr.Zero)
            {
                WriteLog("打开服务失败：" + Marshal.GetLastWin32Error() + "。", LogLevel.Error);
                CloseServiceHandle(scm);
                return false;
            }

            bool result = StartService(svc, 0, null);
            if (!result)
            {
                WriteLog("启动服务失败：" + Marshal.GetLastWin32Error() + "。", LogLevel.Error);
            }

            CloseServiceHandle(svc);
            CloseServiceHandle(scm);
            return result;
        }

        /// <summary>
        /// 停止指定名称的服务。如果服务未运行，则不做任何操作。
        /// </summary>
        /// <param name="serviceName">服务名称</param>
        /// <returns>停止成功返回 true；否则返回 false</returns>
        public static bool StopService(string serviceName)
        {
            SERVICE_STATUS status = new();

            IntPtr scm = OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
            if (scm == IntPtr.Zero)
            {
                WriteLog("与服务控制管理器建立连接失败：" + Marshal.GetLastWin32Error() + "。", LogLevel.Error);
                return false;
            }

            IntPtr svc = OpenService(scm, serviceName, SERVICE_ALL_ACCESS);
            if (svc == IntPtr.Zero)
            {
                WriteLog("打开服务失败：" + Marshal.GetLastWin32Error() + "。", LogLevel.Error);
                CloseServiceHandle(scm);
                return false;
            }

            // 发送停止控制指令
            bool controlResult = ControlService(svc, SERVICE_CONTROL_STOP, ref status);
            if (!controlResult)
            {
                int error = Marshal.GetLastWin32Error();
                // 如果服务已停止，也可以认为操作成功
                if (error != 1062) // 1062: The service has not been started.
                {
                    WriteLog("停止服务失败：" + error + "。", LogLevel.Error);
                    CloseServiceHandle(svc);
                    CloseServiceHandle(scm);
                    return false;
                }
            }

            // 等待服务状态变为停止
            int timeout = 30000; // 最长等待 30 秒
            int sleepInterval = 500;
            int elapsed = 0;
            while (status.dwCurrentState != SERVICE_STOPPED && elapsed < timeout)
            {
                System.Threading.Thread.Sleep(sleepInterval);
                elapsed += sleepInterval;
                if (!QueryServiceStatus(svc, ref status))
                {
                    WriteLog("查询服务状态失败：" + Marshal.GetLastWin32Error() + "。", LogLevel.Error);
                    break;
                }
            }

            CloseServiceHandle(svc);
            CloseServiceHandle(scm);
            return status.dwCurrentState == SERVICE_STOPPED;
        }

        /// <summary>
        /// 查询指定服务的当前状态。
        /// </summary>
        /// <param name="serviceName">服务名称</param>
        /// <returns>返回 SERVICE_STATUS 结构，如果失败则抛出异常</returns>
        public static SERVICE_STATUS QueryServiceStatusByName(string serviceName)
        {
            SERVICE_STATUS status = new();

            IntPtr scm = OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
            if (scm == IntPtr.Zero)
            {
                throw new Exception("与服务控制管理器建立连接失败：" + Marshal.GetLastWin32Error());
            }

            IntPtr svc = OpenService(scm, serviceName, SERVICE_ALL_ACCESS);
            if (svc == IntPtr.Zero)
            {
                CloseServiceHandle(scm);
                throw new Exception("打开服务失败：" + Marshal.GetLastWin32Error());
            }

            if (!QueryServiceStatus(svc, ref status))
            {
                int err = Marshal.GetLastWin32Error();
                CloseServiceHandle(svc);
                CloseServiceHandle(scm);
                throw new Exception("查询服务状态失败：" + err);
            }

            CloseServiceHandle(svc);
            CloseServiceHandle(scm);
            return status;
        }

        /// <summary>
        /// 获取服务的二进制路径
        /// </summary>
        /// <param name="serviceName">服务名</param>
        /// <returns>服务二进制路径</returns>
        public static string GetServiceBinaryPath(string serviceName)
        {
            string regPath = $@"SYSTEM\CurrentControlSet\Services\{serviceName}";
            using RegistryKey key = Registry.LocalMachine.OpenSubKey(regPath);
            if (key != null)
            {
                object binaryPathValue = key.GetValue("ImagePath");
                if (binaryPathValue != null) return binaryPathValue.ToString();
            }
            return null;
        }

        /// <summary>
        /// 检查指定名称的服务是否已经安装。
        /// </summary>
        /// <param name="serviceName">服务名称</param>
        public static bool IsServiceInstalled(string serviceName)
        {
            IntPtr scm = OpenSCManager(null, null, SC_MANAGER_CONNECT);
            if (scm == IntPtr.Zero)
            {
                WriteLog("与服务控制管理器建立连接失败：" + Marshal.GetLastWin32Error(), LogLevel.Error);
                return false;
            }
            IntPtr svc = OpenService(scm, serviceName, SERVICE_QUERY_STATUS);
            if (svc == IntPtr.Zero)
            {
                CloseServiceHandle(scm);
                return false;
            }
            CloseServiceHandle(svc);
            CloseServiceHandle(scm);
            return true;
        }
    }
}
