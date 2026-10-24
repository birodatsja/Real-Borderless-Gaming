using System;
using System.Collections.Generic;
using System.Globalization;

namespace SRWE.Core
{
	/// <summary>The one shared copy of the profile library and settings.</summary>
	internal static class AppState
	{
		public static readonly ProfileLibrary Library = Storage.Load<ProfileLibrary>("profiles.xml");
		public static readonly LauncherSettings Settings = Storage.Load<LauncherSettings>("launcher.xml");

		static AppState()
		{
			if (PresetService.EnsureSeeded(Settings)) SaveSettings();
		}

		public static void SaveSettings() { Storage.Save("launcher.xml", Settings); }
		public static void SaveLibrary() { Storage.Save("profiles.xml", Library); }

		public static bool IsIgnored(string processName)
		{
			foreach (string n in Settings.IgnoredProcesses)
				if (string.Equals(n, processName, StringComparison.OrdinalIgnoreCase)) return true;
			return false;
		}

		public static void SetIgnored(string processName, bool ignored)
		{
			Settings.IgnoredProcesses.RemoveAll(delegate(string n)
			{
				return string.Equals(n, processName, StringComparison.OrdinalIgnoreCase);
			});
			if (ignored) Settings.IgnoredProcesses.Add(processName);
			SaveSettings();
		}
	}

	internal static class PresetService
	{
		public static List<ResolutionPreset> Defaults()
		{
			int[][] sizes = new int[][] {
				new int[] {1280,720}, new int[] {1600,900}, new int[] {1536,864}, new int[] {1920,1080}, new int[] {2560,1440}, new int[] {3840,2160},
				new int[] {1920,1200}, new int[] {2560,1600}, new int[] {2880,1800}, new int[] {3840,2400}, new int[] {5760,3600},
				new int[] {1920,822}, new int[] {2560,1080}, new int[] {2880,1200}, new int[] {3440,1440}, new int[] {3840,1645}, new int[] {5760,2468},
				new int[] {5120,1440},
				new int[] {960,1200}, new int[] {1920,2400}, new int[] {2880,3600},
				new int[] {1200,1200}, new int[] {1800,1800}, new int[] {2400,2400}, new int[] {3000,3000}, new int[] {5000,5000}
			};
			List<ResolutionPreset> list = new List<ResolutionPreset>();
			foreach (int[] s in sizes) list.Add(new ResolutionPreset("", s[0], s[1]));
			return list;
		}

		/// <summary>First run: put the built-in presets into the user's own (editable) list.</summary>
		public static bool EnsureSeeded(LauncherSettings s)
		{
			if (s.PresetsSeeded) return false;
			s.Presets.AddRange(Defaults());
			s.PresetsSeeded = true;
			return true;
		}

		/// <summary>Re-add any built-in preset that is missing. Returns how many were added.</summary>
		public static int RestoreDefaults(LauncherSettings s)
		{
			int added = 0;
			List<ResolutionPreset> merged = new List<ResolutionPreset>();
			foreach (ResolutionPreset d in Defaults())
			{
				if (Find(s, d.Width, d.Height) == null) { merged.Add(d); added++; }
			}
			s.Presets.AddRange(merged);
			s.Presets.Sort(delegate(ResolutionPreset a, ResolutionPreset b)
			{
				int c = a.Height.CompareTo(b.Height);
				return c != 0 ? c : a.Width.CompareTo(b.Width);
			});
			return added;
		}

		public static ResolutionPreset Find(LauncherSettings s, int w, int h)
		{
			foreach (ResolutionPreset p in s.Presets)
				if (p.Width == w && p.Height == h) return p;
			return null;
		}

		public static string Ratio(int w, int h)
		{
			if (h <= 0) return "";
			double r = (double)w / h;
			double[] known = { 16.0 / 9, 16.0 / 10, 21.0 / 9, 32.0 / 9, 4.0 / 3, 3.0 / 2, 5.0 / 4, 1.0, 4.0 / 5, 9.0 / 16, 3.0 / 4, 2.0 / 3 };
			string[] names = { "16:9", "16:10", "21:9", "32:9", "4:3", "3:2", "5:4", "1:1", "4:5", "9:16", "3:4", "2:3" };
			for (int i = 0; i < known.Length; i++)
				if (Math.Abs(r - known[i]) / known[i] < 0.03) return names[i];
			return r.ToString("0.##", CultureInfo.InvariantCulture) + ":1";
		}

		public static string Size(int w, int h) { return w + " \u00D7 " + h; }

		public static string Display(ResolutionPreset p)
		{
			string size = Size(p.Width, p.Height);
			if (string.IsNullOrEmpty(p.Name)) return size + "   (" + Ratio(p.Width, p.Height) + ")";
			return p.Name + "  \u00B7  " + size;
		}
	}
}
