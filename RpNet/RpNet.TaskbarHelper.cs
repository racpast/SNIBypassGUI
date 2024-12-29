using System;
using System.Runtime.InteropServices;

namespace RpNet.TaskBarHelper
{
	public class TaskBarIconHelper
	{

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

		public static void RefreshNotification()
		{
			var NotifyAreaHandle = GetNotifyAreaHandle();
			if (NotifyAreaHandle != IntPtr.Zero)
			{
				RefreshWindow(NotifyAreaHandle);
			}

			var NotifyOverHandle = GetNotifyOverHandle();
			if (NotifyOverHandle != IntPtr.Zero)
			{
				RefreshWindow(NotifyOverHandle);
			}
		}

		private static void RefreshWindow(IntPtr windowHandle)
		{
			const uint WM_MOUSEMOVE = 0x0200;
			RECT rect;
			GetClientRect(windowHandle, out rect);
			for (var x = 0; x < rect.right; x += 5)
				for (var y = 0; y < rect.bottom; y += 5)
					SendMessage(windowHandle, WM_MOUSEMOVE, 0, (y << 16) + x);
		}

		private static IntPtr GetNotifyAreaHandle()
		{
			var TrayWndHandle = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Shell_TrayWnd", null);
			var TrayNotifyWndHandle = FindWindowEx(TrayWndHandle, IntPtr.Zero, "TrayNotifyWnd", null);
			var SysPagerHandle = FindWindowEx(TrayNotifyWndHandle, IntPtr.Zero, "SysPager", null);
			var NotifyAreaHandle = FindWindowEx(SysPagerHandle, IntPtr.Zero, "ToolbarWindow32", null);

			return NotifyAreaHandle;
		}

		private static IntPtr GetNotifyOverHandle()
		{
			var OverHandle = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "NotifyIconOverflowWindow", null);
			var NotifyOverHandle = FindWindowEx(OverHandle, IntPtr.Zero, "ToolbarWindow32", null);

			return NotifyOverHandle;
		}
	}
}
