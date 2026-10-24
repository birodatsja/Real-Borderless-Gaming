using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SRWE.Core
{
	public class GameProfile
	{
		public string Id = Guid.NewGuid().ToString("N");
		public string Name = "";
		public string ExeName = "";          // e.g. "game.exe"
		public string ExePath = "";          // informational only
		// Target area in virtual-desktop pixels (can be negative, can span several displays).
		public int PosX = 0;
		public int PosY = 0;
		public int Width = 1920;
		public int Height = 1080;
		public string Monitor = "";          // display used by Fill/Align/presets: Screen.DeviceName, or "*" = all displays
		public bool Borderless = true;
		public bool HideTaskbar = false;
		public bool TaskbarAllMonitors = false;
		public bool AutoActivate = true;
		public bool ForceExitSizeMove = false;
		public int ApplyDelayMs = 2000;      // wait after the window first appears
	}

	[XmlRoot("SrweGameProfiles")]
	public class ProfileLibrary
	{
		public List<GameProfile> Profiles = new List<GameProfile>();
	}

	/// <summary>A global keyboard shortcut. Modifiers: 1 = Alt, 2 = Ctrl, 4 = Shift. Key = System.Windows.Forms.Keys value (0 = not set).</summary>
	public class HotkeyBinding
	{
		public int Modifiers;
		public int Key;

		public HotkeyBinding() { }
		public HotkeyBinding(int modifiers, int key) { Modifiers = modifiers; Key = key; }
		public bool IsSet { get { return Key != 0; } }
		public HotkeyBinding Clone() { return new HotkeyBinding(Modifiers, Key); }
	}

	public class ResolutionPreset
	{
		public string Name = "";      // empty = just show the size
		public int Width = 1920;
		public int Height = 1080;

		public ResolutionPreset() { }
		public ResolutionPreset(string name, int w, int h) { Name = name; Width = w; Height = h; }
		public override string ToString() { return PresetService.Display(this); }
	}

	[XmlRoot("SrweLauncherSettings")]
	public class LauncherSettings
	{
		public bool AutoActivateEnabled = true;
		public bool MinimizeToTray = true;
		public bool TrayHintShown = false;
		public int WindowX, WindowY, WindowW, WindowH;        // last window bounds (0 width = never saved)
		public string ThemeMode = "dark";                     // dark | light | system
		public bool PresetsSeeded = false;
		public List<ResolutionPreset> Presets = new List<ResolutionPreset>();
		public List<string> IgnoredProcesses = new List<string>();
		public HotkeyBinding HotkeyApplyRestore = new HotkeyBinding(2 | 1, 0x42);        // Ctrl+Alt+B
		public HotkeyBinding HotkeyTaskbar = new HotkeyBinding(2 | 1, 0x54);             // Ctrl+Alt+T
		public HotkeyBinding HotkeyRestoreDesktop = new HotkeyBinding(2 | 1 | 4, 0x52);  // Ctrl+Alt+Shift+R
	}
}
