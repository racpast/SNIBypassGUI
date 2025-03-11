using System;
using System.Runtime.InteropServices;
using static SNIBypassGUI.Utils.ServiceUtils;

namespace SNIBypassGUI.Utils
{
    public static class WinApiUtils
    {
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr OpenSCManager(
    string lpMachineName,
    string lpDatabaseName,
    uint dwDesiredAccess);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateService(
            IntPtr hSCManager,
            string lpServiceName,
            string lpDisplayName,
            uint dwDesiredAccess,
            uint dwServiceType,
            uint dwStartType,
            uint dwErrorControl,
            string lpBinaryPathName,
            string lpLoadOrderGroup,
            IntPtr lpdwTagId,
            string lpDependencies,
            string lpServiceStartName,
            string lpPassword);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool ChangeServiceConfig(
            IntPtr hService,
            uint dwServiceType,
            uint dwStartType,
            uint dwErrorControl,
            string lpBinaryPathName,
            string lpLoadOrderGroup,
            IntPtr lpdwTagId,
            string lpDependencies,
            string lpServiceStartName,
            string lpPassword,
            string lpDisplayName);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool StartService(
            IntPtr hService,
            int dwNumServiceArgs,
            string lpServiceArgVectors);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr OpenService(
            IntPtr hSCManager,
            string lpServiceName,
            uint dwDesiredAccess);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool DeleteService(IntPtr hService);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool ControlService(
            IntPtr hService,
            uint dwControl,
            ref SERVICE_STATUS lpServiceStatus);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool QueryServiceStatus(
            IntPtr hService,
            ref SERVICE_STATUS lpServiceStatus);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool CloseServiceHandle(IntPtr hSCObject);

        [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
        public static extern UInt32 DnsFlushResolverCache();

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);

        [DllImport("user32.dll")]
        static extern bool GetClientRect(IntPtr handle, out RECT rect);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr handle, UInt32 message, Int32 wParam, Int32 lParam);

        struct RECT
        {
            public int left, top, right, bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SERVICE_STATUS
        {
            public uint dwServiceType;
            public uint dwCurrentState;
            public uint dwControlsAccepted;
            public uint dwWin32ExitCode;
            public uint dwServiceSpecificExitCode;
            public uint dwCheckPoint;
            public uint dwWaitHint;
        }

        // SCM 访问权限
        public const uint SC_MANAGER_ALL_ACCESS = 0xF003F;
        // 用于写操作访问权限
        public const uint GENERIC_WRITE = 0x40000000;
        // 服务访问权限，全部权限
        public const uint SERVICE_ALL_ACCESS = 0xF01FF;
        // 修改服务配置权限
        public const uint SERVICE_CHANGE_CONFIG = 0x0002;
        // 删除服务时需要的权限
        public const uint DELETE = 0x10000;

        // 服务类型
        public const uint SERVICE_WIN32_OWN_PROCESS = 0x00000010;
        // 启动类型
        public const uint SERVICE_AUTO_START = 0x00000002;
        public const uint SERVICE_DEMAND_START = 0x00000003;
        // 错误控制类型
        public const uint SERVICE_ERROR_NORMAL = 0x00000001;
        // 表示不更改该项设置
        public const uint SERVICE_NO_CHANGE = 0xFFFFFFFF;

        // 控制代码
        public const uint SERVICE_CONTROL_STOP = 0x00000001;
        // 服务状态定义
        public const uint SERVICE_STOPPED = 0x00000001;
        public const uint SERVICE_START_PENDING = 0x00000002;
        public const uint SERVICE_STOP_PENDING = 0x00000003;
        public const uint SERVICE_RUNNING = 0x00000004;
        public const uint SERVICE_CONTINUE_PENDING = 0x00000005;
        public const uint SERVICE_PAUSE_PENDING = 0x00000006;
        public const uint SERVICE_PAUSED = 0x00000007;

        /// <summary>
        /// 刷新系统通知区域
        /// </summary>
        public static void RefreshNotification()
        {
            var NotifyAreaHandle = GetNotifyAreaHandle();
            if (NotifyAreaHandle != IntPtr.Zero) RefreshWindow(NotifyAreaHandle);
            var NotifyOverHandle = GetNotifyOverHandle();
            if (NotifyOverHandle != IntPtr.Zero) RefreshWindow(NotifyOverHandle);
        }

        /// <summary>
        /// 刷新窗口
        /// </summary>
        private static void RefreshWindow(IntPtr windowHandle)
        {
            const uint WM_MOUSEMOVE = 0x0200;
            GetClientRect(windowHandle, out RECT rect);
            for (var x = 0; x < rect.right; x += 5)
                for (var y = 0; y < rect.bottom; y += 5)
                    SendMessage(windowHandle, WM_MOUSEMOVE, 0, (y << 16) + x);
        }

        /// <summary>
        /// 获取系统通知区域句柄
        /// </summary>
        private static IntPtr GetNotifyAreaHandle()
        {
            var TrayWndHandle = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Shell_TrayWnd", null);
            var TrayNotifyWndHandle = FindWindowEx(TrayWndHandle, IntPtr.Zero, "TrayNotifyWnd", null);
            var SysPagerHandle = FindWindowEx(TrayNotifyWndHandle, IntPtr.Zero, "SysPager", null);
            var NotifyAreaHandle = FindWindowEx(SysPagerHandle, IntPtr.Zero, "ToolbarWindow32", null);
            return NotifyAreaHandle;
        }

        /// <summary>
        /// 获取溢出通知区域句柄
        /// </summary>
        private static IntPtr GetNotifyOverHandle()
        {
            var OverHandle = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "NotifyIconOverflowWindow", null);
            var NotifyOverHandle = FindWindowEx(OverHandle, IntPtr.Zero, "ToolbarWindow32", null);
            return NotifyOverHandle;
        }
    }
}
