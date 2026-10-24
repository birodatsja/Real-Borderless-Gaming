using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SRWE.Core
{
	/// <summary>Everything needed to put a window back exactly as it was.</summary>
	internal class WindowSnapshot
	{
		public IntPtr Hwnd;
		public NativeRect Rect;
		public uint Style;
		public uint ExStyle;
		public bool WasMaximized;

		public static WindowSnapshot Capture(IntPtr hwnd)
		{
			WindowSnapshot s = new WindowSnapshot();
			s.Hwnd = hwnd;
			s.WasMaximized = Native.IsZoomed(hwnd);
			if (s.WasMaximized || Native.IsIconic(hwnd))
				Native.ShowWindow(hwnd, Native.SW_RESTORE); // so we capture the normal rectangle
			Native.GetWindowRect(hwnd, out s.Rect);
			s.Style = (uint)Native.GetWindowLong(hwnd, Native.GWL_STYLE);
			s.ExStyle = (uint)Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE);
			return s;
		}

		public void Restore()
		{
			if (!Native.IsWindow(Hwnd)) return;
			Native.SetWindowLong(Hwnd, Native.GWL_STYLE, (int)Style);
			Native.SetWindowLong(Hwnd, Native.GWL_EXSTYLE, (int)ExStyle);
			Native.SetWindowPos(Hwnd, IntPtr.Zero, Rect.Left, Rect.Top, Rect.Width, Rect.Height,
				Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_FRAMECHANGED);
			if (WasMaximized) Native.ShowWindow(Hwnd, Native.SW_MAXIMIZE);
		}
	}

	internal static class WindowFinder
	{
		private static bool TryCandidate(IntPtr h, out int pid, out long area)
		{
			pid = 0; area = 0;
			uint p;
			Native.GetWindowThreadProcessId(h, out p);
			pid = (int)p;
			if (!Native.IsWindowVisible(h)) return false;
			if (Native.GetWindow(h, Native.GW_OWNER) != IntPtr.Zero) return false;
			if (((uint)Native.GetWindowLong(h, Native.GWL_EXSTYLE) & Native.WS_EX_TOOLWINDOW) != 0) return false;
			NativeRect rc;
			if (!Native.GetWindowRect(h, out rc)) return false;
			if (rc.Width < 200 || rc.Height < 150) return false;
			area = (long)rc.Width * rc.Height;
			return true;
		}

		/// <summary>The biggest visible, un-owned, non-tool top-level window of a process.</summary>
		public static IntPtr FindMainWindow(int pid)
		{
			IntPtr best = IntPtr.Zero;
			long bestArea = 0;
			EnumWindowsProc cb = delegate(IntPtr h, IntPtr l)
			{
				int p; long a;
				if (!TryCandidate(h, out p, out a) || p != pid) return true;
				if (a > bestArea) { bestArea = a; best = h; }
				return true;
			};
			Native.EnumWindows(cb, IntPtr.Zero);
			GC.KeepAlive(cb);
			return best;
		}

		/// <summary>Main window of every process in a single pass (used by the process picker).</summary>
		public static Dictionary<int, IntPtr> MainWindowsByPid()
		{
			Dictionary<int, IntPtr> best = new Dictionary<int, IntPtr>();
			Dictionary<int, long> areas = new Dictionary<int, long>();
			EnumWindowsProc cb = delegate(IntPtr h, IntPtr l)
			{
				int p; long a;
				if (!TryCandidate(h, out p, out a)) return true;
				long old;
				if (!areas.TryGetValue(p, out old) || a > old) { areas[p] = a; best[p] = h; }
				return true;
			};
			Native.EnumWindows(cb, IntPtr.Zero);
			GC.KeepAlive(cb);
			return best;
		}
	}

	internal static class ProfileApplier
	{
		/// <summary>Move/resize the window to the profile's absolute desktop rectangle.</summary>
		public static void Apply(GameProfile p, IntPtr hwnd)
		{
			if (Native.IsZoomed(hwnd) || Native.IsIconic(hwnd))
				Native.ShowWindow(hwnd, Native.SW_RESTORE);

			if (p.Borderless)
			{
				uint style = (uint)Native.GetWindowLong(hwnd, Native.GWL_STYLE);
				uint ex = (uint)Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE);
				style &= ~(Native.WS_CAPTION | Native.WS_THICKFRAME);
				ex &= ~(Native.WS_EX_DLGMODALFRAME | Native.WS_EX_WINDOWEDGE |
					Native.WS_EX_CLIENTEDGE | Native.WS_EX_STATICEDGE);
				Native.SetWindowLong(hwnd, Native.GWL_STYLE, (int)style);
				Native.SetWindowLong(hwnd, Native.GWL_EXSTYLE, (int)ex);
			}

			Native.SetWindowPos(hwnd, IntPtr.Zero, p.PosX, p.PosY, p.Width, p.Height,
				Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_FRAMECHANGED);

			if (p.ForceExitSizeMove)
				Native.SendMessage(hwnd, Native.WM_EXITSIZEMOVE, IntPtr.Zero, IntPtr.Zero);
		}
	}
}
