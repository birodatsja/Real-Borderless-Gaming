using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace SRWE.Core
{
	/// <summary>
	/// Hides/shows the Windows taskbar(s). Safe against: multiple monitors (Shell_SecondaryTrayWnd),
	/// Explorer restarts (handles are re-enumerated every tick and re-hidden), and SRWE dying
	/// while the taskbar is hidden (a flag file lets the next start put it back).
	/// </summary>
	internal class TaskbarController
	{
		private readonly HashSet<IntPtr> _hidden = new HashSet<IntPtr>();

		private static string FlagPath { get { return Path.Combine(Storage.Dir, "taskbar-hidden.flag"); } }

		public bool HasHidden { get { return _hidden.Count > 0; } }

		public static List<IntPtr> FindTaskbars()
		{
			List<IntPtr> list = new List<IntPtr>();
			IntPtr main = Native.FindWindow("Shell_TrayWnd", null);
			if (main != IntPtr.Zero) list.Add(main);
			IntPtr sec = IntPtr.Zero;
			while ((sec = Native.FindWindowEx(IntPtr.Zero, sec, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
				list.Add(sec);
			return list;
		}

		public static string DeviceOfWindow(IntPtr hwnd)
		{
			try { return Screen.FromHandle(hwnd).DeviceName; }
			catch { return null; }
		}

		/// <summary>Make the taskbars match the wanted state. Called every tick.</summary>
		public void Enforce(bool allMonitors, HashSet<string> devices)
		{
			bool wantAny = allMonitors || (devices != null && devices.Count > 0);
			if (!wantAny && _hidden.Count == 0) return;

			List<IntPtr> bars = FindTaskbars();
			_hidden.RemoveWhere(delegate(IntPtr h) { return !bars.Contains(h); }); // Explorer restarted

			foreach (IntPtr bar in bars)
			{
				bool want = allMonitors;
				if (!want && devices != null && devices.Count > 0)
				{
					string dev = DeviceOfWindow(bar);
					want = dev != null && devices.Contains(dev);
				}

				if (want)
				{
					if (!_hidden.Contains(bar))
					{
						if (!Native.IsWindowVisible(bar)) continue; // not ours to touch
						_hidden.Add(bar);
					}
					if (Native.IsWindowVisible(bar)) Native.ShowWindow(bar, Native.SW_HIDE);
				}
				else if (_hidden.Remove(bar))
				{
					Native.ShowWindow(bar, Native.SW_SHOWNA);
				}
			}
			UpdateFlag();
		}

		/// <summary>Show everything this instance hid.</summary>
		public void RestoreHidden()
		{
			foreach (IntPtr bar in FindTaskbars())
				if (_hidden.Contains(bar)) Native.ShowWindow(bar, Native.SW_SHOWNA);
			_hidden.Clear();
			UpdateFlag();
		}

		/// <summary>Emergency: show every taskbar no matter what we think we did.</summary>
		public static void ForceShowAll()
		{
			foreach (IntPtr bar in FindTaskbars())
				if (!Native.IsWindowVisible(bar)) Native.ShowWindow(bar, Native.SW_SHOWNA);
			try { if (File.Exists(FlagPath)) File.Delete(FlagPath); } catch { }
		}

		/// <summary>On startup: if a previous run died with the taskbar hidden, bring it back.</summary>
		public static void RecoverFromCrash()
		{
			try { if (File.Exists(FlagPath)) ForceShowAll(); } catch { }
		}

		private void UpdateFlag()
		{
			try
			{
				bool exists = File.Exists(FlagPath);
				if (_hidden.Count > 0 && !exists) File.WriteAllText(FlagPath, "hidden");
				else if (_hidden.Count == 0 && exists) File.Delete(FlagPath);
			}
			catch { }
		}
	}
}
