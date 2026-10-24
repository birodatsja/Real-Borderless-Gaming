using System;
using System.Runtime.InteropServices;

namespace SRWE.Core
{
	[StructLayout(LayoutKind.Sequential)]
	internal struct NativeRect
	{
		public int Left, Top, Right, Bottom;
		public int Width { get { return Right - Left; } }
		public int Height { get { return Bottom - Top; } }
	}

	internal delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

	internal static class Native
	{
		public const int GWL_STYLE = -16;
		public const int GWL_EXSTYLE = -20;
		public const uint WS_CAPTION = 0x00C00000;
		public const uint WS_THICKFRAME = 0x00040000;
		public const uint WS_EX_DLGMODALFRAME = 0x00000001;
		public const uint WS_EX_TOOLWINDOW = 0x00000080;
		public const uint WS_EX_WINDOWEDGE = 0x00000100;
		public const uint WS_EX_CLIENTEDGE = 0x00000200;
		public const uint WS_EX_STATICEDGE = 0x00020000;
		public const uint SWP_NOZORDER = 0x0004;
		public const uint SWP_NOACTIVATE = 0x0010;
		public const uint SWP_FRAMECHANGED = 0x0020;
		public const int SW_HIDE = 0;
		public const int SW_MAXIMIZE = 3;
		public const int SW_SHOWNA = 8;
		public const int SW_RESTORE = 9;
		public const uint WM_EXITSIZEMOVE = 0x0232;
		public const int WM_HOTKEY = 0x0312;
		public const uint GW_OWNER = 4;
		public static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);

		public static readonly uint ShowMeMessage = RegisterWindowMessage("RealBorderlessGaming_ShowMe");
		public static readonly uint TaskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");

		[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
		[DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
		[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
		[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
		[DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
		[DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
		[DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
		[DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr hwnd);
		[DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);
		[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rc);
		[DllImport("user32.dll", EntryPoint = "GetWindowLongW")] public static extern int GetWindowLong(IntPtr hwnd, int index);
		[DllImport("user32.dll", EntryPoint = "SetWindowLongW")] public static extern int SetWindowLong(IntPtr hwnd, int index, int value);
		[DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
		[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int cmd);
		[DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
		[DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
		[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
		[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
		[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterWindowMessage(string name);
		[DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint mods, uint vk);
		[DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd, int id);
	}
}
