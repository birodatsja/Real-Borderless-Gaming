using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SRWE.Core
{
	internal class Session
	{
		public GameProfile Profile;
		public int Pid;
		public IntPtr Hwnd;
		public WindowSnapshot Snapshot;
		public bool Applied;
		public bool Suppressed;   // user pressed Restore: leave this process alone until it exits
		public DateTime WindowSeenAt = DateTime.MinValue;
	}

	internal enum ProfileState { Idle, Running, Active }

	internal enum TaskbarOverride { None, ForceHide, ForceShow }

	/// <summary>Watches for games, applies/restores profiles and owns the taskbar policy.</summary>
	internal class DesktopManager : IDisposable
	{
		private readonly Dictionary<int, Session> _sessions = new Dictionary<int, Session>();
		private readonly TaskbarController _taskbar = new TaskbarController();
		private TaskbarOverride _taskbarOverride = TaskbarOverride.None;
		private readonly Timer _timer = new Timer();
		private readonly ProfileLibrary _library;
		private readonly LauncherSettings _settings;

		public event EventHandler StateChanged;
		public event Action<string> Notice;

		public DesktopManager(ProfileLibrary library, LauncherSettings settings)
		{
			_library = library;
			_settings = settings;
			_timer.Interval = 1000;
			_timer.Tick += delegate { Tick(); };
		}

		public void Start() { _timer.Start(); Tick(); }

		private static bool NameMatches(Process p, string exeName)
		{
			string want = Path.GetFileNameWithoutExtension(exeName);
			try { return string.Equals(p.ProcessName, want, StringComparison.OrdinalIgnoreCase); }
			catch { return false; }
		}

		public void Tick()
		{
			Process[] procs;
			try { procs = Process.GetProcesses(); }
			catch { return; }

			bool changed = false;
			try
			{
				HashSet<int> alive = new HashSet<int>();
				foreach (Process p in procs) alive.Add(p.Id);

				// 1. games that exited: the window is gone, so just forget the session
				foreach (int pid in new List<int>(_sessions.Keys))
				{
					if (alive.Contains(pid)) continue;
					Session gone = _sessions[pid];
					_sessions.Remove(pid);
					changed = true;
					if (gone.Applied) Say(gone.Profile.Name + " closed - desktop restored");
				}

				// 2. discover running games (matched by executable name, so launchers don't matter)
				foreach (GameProfile gp in _library.Profiles)
				{
					if (string.IsNullOrEmpty(gp.ExeName)) continue;
					foreach (Process p in procs)
					{
						if (_sessions.ContainsKey(p.Id)) continue;
						if (!NameMatches(p, gp.ExeName)) continue;
						Session ns = new Session();
						ns.Profile = gp;
						ns.Pid = p.Id;
						_sessions[p.Id] = ns;
						changed = true;
					}
				}

				// 3. apply / re-apply
				foreach (Session s in new List<Session>(_sessions.Values))
					if (ServiceSession(s, false)) changed = true;

				// 4. taskbar policy
				UpdateTaskbar();
			}
			finally
			{
				foreach (Process p in procs) p.Dispose();
			}
			if (changed) Raise();
		}

		private bool ServiceSession(Session s, bool immediate)
		{
			if (s.Suppressed) return false;

			if (s.Applied)
			{
				if (Native.IsWindow(s.Hwnd)) return false;
				// game recreated its window (splash -> main window, mode switch): follow it
				IntPtr again = WindowFinder.FindMainWindow(s.Pid);
				if (again == IntPtr.Zero) return false;
				s.Hwnd = again;
				s.Snapshot = WindowSnapshot.Capture(again);
				ProfileApplier.Apply(s.Profile, again);
				return true;
			}

			if (!immediate && !(_settings.AutoActivateEnabled && s.Profile.AutoActivate)) return false;

			IntPtr h = WindowFinder.FindMainWindow(s.Pid);
			if (h == IntPtr.Zero) { s.WindowSeenAt = DateTime.MinValue; return false; }
			if (s.WindowSeenAt == DateTime.MinValue) s.WindowSeenAt = DateTime.UtcNow;
			if (!immediate && (DateTime.UtcNow - s.WindowSeenAt).TotalMilliseconds < s.Profile.ApplyDelayMs)
				return false;

			s.Hwnd = h;
			s.Snapshot = WindowSnapshot.Capture(h);
			ProfileApplier.Apply(s.Profile, h);
			s.Applied = true;
			_taskbarOverride = TaskbarOverride.None;   // a fresh apply goes back to the profile's own taskbar setting
			Say("Applied \"" + s.Profile.Name + "\"");
			return true;
		}

		private void UpdateTaskbar()
		{
			if (_taskbarOverride == TaskbarOverride.ForceShow)
			{
				if (_taskbar.HasHidden) _taskbar.RestoreHidden();
				return;
			}

			bool all = _taskbarOverride == TaskbarOverride.ForceHide;
			HashSet<string> devices = new HashSet<string>();
			if (!all)
			{
				foreach (Session s in _sessions.Values)
				{
					if (!s.Applied || s.Suppressed || !s.Profile.HideTaskbar) continue;
					if (s.Profile.TaskbarAllMonitors) all = true;
					else AddCoveredDevices(s.Hwnd, devices);
				}
			}
			_taskbar.Enforce(all, devices);
		}

		/// <summary>Keyboard toggle: hide the taskbar, or show it again, whatever the profiles say. Returns a message.</summary>
		public string ToggleTaskbar()
		{
			bool hiddenNow = _taskbar.HasHidden;
			_taskbarOverride = hiddenNow ? TaskbarOverride.ForceShow : TaskbarOverride.ForceHide;
			UpdateTaskbar();
			return hiddenNow ? "Taskbar shown" : "Taskbar hidden";
		}

		/// <summary>Every display the window overlaps (a spanning window covers several).</summary>
		private static void AddCoveredDevices(IntPtr hwnd, HashSet<string> devices)
		{
			NativeRect rc;
			if (!Native.GetWindowRect(hwnd, out rc)) return;
			Rectangle r = Rectangle.FromLTRB(rc.Left, rc.Top, rc.Right, rc.Bottom);
			foreach (Screen sc in Screen.AllScreens)
				if (sc.Bounds.IntersectsWith(r)) devices.Add(sc.DeviceName);
		}

		/// <summary>Manual Apply. Returns an error message, or null on success.</summary>
		public string ApplyNow(GameProfile gp)
		{
			if (string.IsNullOrEmpty(gp.ExeName)) return "Set the executable name first.";
			int pid = 0;
			Process[] procs = Process.GetProcesses();
			try
			{
				foreach (Process p in procs)
					if (NameMatches(p, gp.ExeName)) { pid = p.Id; break; }
			}
			finally { foreach (Process p in procs) p.Dispose(); }
			if (pid == 0) return gp.ExeName + " is not running.";

			Session s;
			if (!_sessions.TryGetValue(pid, out s))
			{
				s = new Session();
				s.Pid = pid;
				_sessions[pid] = s;
			}
			s.Profile = gp;
			s.Suppressed = false;
			_taskbarOverride = TaskbarOverride.None;

			if (s.Applied && Native.IsWindow(s.Hwnd))
			{
				ProfileApplier.Apply(gp, s.Hwnd);   // re-apply edited settings, keep original snapshot
			}
			else
			{
				s.Applied = false;
				if (!ServiceSession(s, true)) return "No visible game window found yet.";
			}
			UpdateTaskbar();
			Raise();
			return null;
		}

		/// <summary>Manual Restore for one profile. Returns how many windows were restored.</summary>
		public int Restore(GameProfile gp)
		{
			int n = 0;
			foreach (Session s in new List<Session>(_sessions.Values))
			{
				if (s.Profile.Id != gp.Id) continue;
				if (s.Applied) n++;
				RestoreSession(s);
			}
			UpdateTaskbar();
			Raise();
			return n;
		}

		private static void RestoreSession(Session s)
		{
			if (s.Applied && s.Snapshot != null) s.Snapshot.Restore();
			s.Applied = false;
			s.Suppressed = true;
		}

		/// <summary>Emergency button: restore every window and every taskbar.</summary>
		public void RestoreAll()
		{
			_taskbarOverride = TaskbarOverride.None;
			foreach (Session s in _sessions.Values) RestoreSession(s);
			_taskbar.RestoreHidden();
			TaskbarController.ForceShowAll();
			Raise();
			Say("Desktop restored");
		}

		public ProfileState GetState(GameProfile gp)
		{
			ProfileState st = ProfileState.Idle;
			foreach (Session s in _sessions.Values)
			{
				if (s.Profile.Id != gp.Id) continue;
				if (s.Applied && !s.Suppressed) return ProfileState.Active;
				st = ProfileState.Running;
			}
			return st;
		}

		/// <summary>App exit: put the taskbar back; leave game windows alone.</summary>
		public void Shutdown()
		{
			_timer.Stop();
			_taskbar.RestoreHidden();
		}

		private void Raise() { if (StateChanged != null) StateChanged(this, EventArgs.Empty); }
		private void Say(string msg) { if (Notice != null) Notice(msg); }

		public void Dispose() { _timer.Dispose(); }
	}
}
