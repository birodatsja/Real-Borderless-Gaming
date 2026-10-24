using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SRWE.UI
{
	/// <summary>Dark / light palette. Controls read these at paint time; call SetMode to switch.</summary>
	internal static class Theme
	{
		public static event EventHandler Changed;

		public static bool IsDark { get; private set; }
		public static string Mode { get; private set; }

		public static Color Bg { get; private set; }
		public static Color Sidebar { get; private set; }
		public static Color Card { get; private set; }
		public static Color CardBorder { get; private set; }
		public static Color Field { get; private set; }
		public static Color FieldHover { get; private set; }
		public static Color FieldBorder { get; private set; }
		public static Color Accent { get; private set; }
		public static Color AccentHover { get; private set; }
		public static Color AccentDown { get; private set; }
		public static Color Selected { get; private set; }
		public static Color Text { get; private set; }
		public static Color Muted { get; private set; }
		public static Color Dim { get; private set; }
		public static Color Danger { get; private set; }
		public static Color DangerHover { get; private set; }
		public static Color Good { get; private set; }
		public static Color Warn { get; private set; }
		public static Color Toggle { get; private set; }

		private static string _family;

		static Theme()
		{
			Mode = "dark";
			Use(true);
		}

		private static void Use(bool dark)
		{
			IsDark = dark;
			if (dark)
			{
				Bg = Color.FromArgb(17, 17, 21);
				Sidebar = Color.FromArgb(23, 23, 28);
				Card = Color.FromArgb(29, 29, 35);
				CardBorder = Color.FromArgb(43, 43, 51);
				Field = Color.FromArgb(38, 38, 46);
				FieldHover = Color.FromArgb(46, 46, 56);
				FieldBorder = Color.FromArgb(56, 56, 67);
				Accent = Color.FromArgb(99, 102, 241);
				AccentHover = Color.FromArgb(120, 123, 250);
				AccentDown = Color.FromArgb(84, 87, 214);
				Selected = Color.FromArgb(38, 40, 72);
				Text = Color.FromArgb(236, 236, 241);
				Muted = Color.FromArgb(150, 150, 166);
				Dim = Color.FromArgb(98, 98, 112);
				Danger = Color.FromArgb(214, 68, 68);
				DangerHover = Color.FromArgb(232, 90, 90);
				Good = Color.FromArgb(74, 222, 128);
				Warn = Color.FromArgb(250, 204, 21);
				Toggle = Color.FromArgb(72, 72, 86);
			}
			else
			{
				Bg = Color.FromArgb(243, 244, 248);
				Sidebar = Color.FromArgb(252, 252, 254);
				Card = Color.FromArgb(255, 255, 255);
				CardBorder = Color.FromArgb(224, 226, 234);
				Field = Color.FromArgb(240, 241, 246);
				FieldHover = Color.FromArgb(230, 232, 241);
				FieldBorder = Color.FromArgb(206, 209, 221);
				Accent = Color.FromArgb(79, 70, 229);
				AccentHover = Color.FromArgb(99, 102, 241);
				AccentDown = Color.FromArgb(67, 56, 202);
				Selected = Color.FromArgb(229, 231, 252);
				Text = Color.FromArgb(24, 24, 32);
				Muted = Color.FromArgb(92, 96, 112);
				Dim = Color.FromArgb(146, 150, 166);
				Danger = Color.FromArgb(205, 50, 50);
				DangerHover = Color.FromArgb(222, 72, 72);
				Good = Color.FromArgb(22, 163, 74);
				Warn = Color.FromArgb(180, 120, 4);
				Toggle = Color.FromArgb(190, 193, 206);
			}
		}

		private static bool SystemPrefersDark()
		{
			try
			{
				object v = Registry.GetValue(
					@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
					"AppsUseLightTheme", 1);
				return v is int && (int)v == 0;
			}
			catch { return true; }
		}

		/// <summary>"dark", "light" or "system".</summary>
		public static void SetMode(string mode)
		{
			if (mode != "light" && mode != "system") mode = "dark";
			Mode = mode;
			Use(mode == "dark" || (mode == "system" && SystemPrefersDark()));
			if (Changed != null) Changed(null, EventArgs.Empty);
		}

		// ---- role tags: "b:card" = BackColor, "f:muted" = ForeColor; re-applied on every theme change ----

		public static Color ByName(string name)
		{
			switch (name)
			{
				case "sidebar": return Sidebar;
				case "card": return Card;
				case "field": return Field;
				case "text": return Text;
				case "muted": return Muted;
				case "dim": return Dim;
				case "accent": return Accent;
				default: return Bg;
			}
		}

		public static void ApplyTag(Control c)
		{
			string tag = c.Tag as string;
			if (tag == null || tag.Length < 3) return;
			Color col = ByName(tag.Substring(2));
			if (tag.StartsWith("b:")) c.BackColor = col;
			else if (tag.StartsWith("f:")) c.ForeColor = col;
		}

		public static void ApplyTags(Control root)
		{
			ApplyTag(root);
			foreach (Control child in root.Controls) ApplyTags(child);
			root.Invalidate();
		}

		public static T Role<T>(T c, string tag) where T : Control
		{
			c.Tag = tag;
			ApplyTag(c);
			return c;
		}

		/// <summary>Segoe UI Variable on Windows 11, Segoe UI elsewhere.</summary>
		public static Font Face(float size, FontStyle style)
		{
			if (_family == null)
			{
				_family = "Segoe UI";
				foreach (FontFamily f in FontFamily.Families)
					if (f.Name == "Segoe UI Variable Text") { _family = f.Name; break; }
			}
			return new Font(_family, size, style, GraphicsUnit.Point);
		}

		public static GraphicsPath Round(Rectangle r, int radius)
		{
			GraphicsPath p = new GraphicsPath();
			if (radius <= 0) { p.AddRectangle(r); return p; }
			int d = radius * 2;
			p.AddArc(r.X, r.Y, d, d, 180, 90);
			p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
			p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
			p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
			p.CloseFigure();
			return p;
		}

		public static void FillRound(Graphics g, Rectangle r, int radius, Color color)
		{
			using (GraphicsPath gp = Round(r, radius))
			using (SolidBrush b = new SolidBrush(color))
				g.FillPath(b, gp);
		}

		public static void StrokeRound(Graphics g, Rectangle r, int radius, Color color)
		{
			using (GraphicsPath gp = Round(r, radius))
			using (Pen p = new Pen(color))
				g.DrawPath(p, gp);
		}

		/// <summary>Style a context menu (themed renderer + text colours), including sub-menus.</summary>
		public static void StyleMenu(ContextMenuStrip menu)
		{
			menu.Renderer = new DarkRenderer();
			menu.ShowImageMargin = false;
			menu.Font = Face(9.5f, FontStyle.Regular);
			StyleItems(menu.Items);
		}

		public static void StyleItems(ToolStripItemCollection items)
		{
			foreach (ToolStripItem it in items)
			{
				it.ForeColor = Text;
				ToolStripDropDownItem dd = it as ToolStripDropDownItem;
				if (dd != null && dd.HasDropDownItems)
				{
					dd.DropDown.Renderer = new DarkRenderer();
					ToolStripDropDownMenu dm = dd.DropDown as ToolStripDropDownMenu;
					if (dm != null) { dm.ShowImageMargin = false; dm.Font = Face(9.5f, FontStyle.Regular); }
					StyleItems(dd.DropDownItems);
				}
			}
		}
	}

	internal class DarkColors : ProfessionalColorTable
	{
		public override Color ToolStripDropDownBackground { get { return Theme.Card; } }
		public override Color ImageMarginGradientBegin { get { return Theme.Card; } }
		public override Color ImageMarginGradientMiddle { get { return Theme.Card; } }
		public override Color ImageMarginGradientEnd { get { return Theme.Card; } }
		public override Color MenuBorder { get { return Theme.FieldBorder; } }
		public override Color MenuItemBorder { get { return Theme.Selected; } }
		public override Color MenuItemSelected { get { return Theme.Selected; } }
		public override Color MenuItemSelectedGradientBegin { get { return Theme.Selected; } }
		public override Color MenuItemSelectedGradientEnd { get { return Theme.Selected; } }
		public override Color MenuItemPressedGradientBegin { get { return Theme.Selected; } }
		public override Color MenuItemPressedGradientMiddle { get { return Theme.Selected; } }
		public override Color MenuItemPressedGradientEnd { get { return Theme.Selected; } }
		public override Color MenuStripGradientBegin { get { return Theme.Sidebar; } }
		public override Color MenuStripGradientEnd { get { return Theme.Sidebar; } }
		public override Color ToolStripGradientBegin { get { return Theme.Sidebar; } }
		public override Color ToolStripGradientMiddle { get { return Theme.Sidebar; } }
		public override Color ToolStripGradientEnd { get { return Theme.Sidebar; } }
		public override Color ToolStripBorder { get { return Theme.CardBorder; } }
		public override Color StatusStripGradientBegin { get { return Theme.Sidebar; } }
		public override Color StatusStripGradientEnd { get { return Theme.Sidebar; } }
		public override Color ButtonSelectedHighlight { get { return Theme.Selected; } }
		public override Color ButtonSelectedGradientBegin { get { return Theme.Selected; } }
		public override Color ButtonSelectedGradientMiddle { get { return Theme.Selected; } }
		public override Color ButtonSelectedGradientEnd { get { return Theme.Selected; } }
		public override Color ButtonPressedGradientBegin { get { return Theme.Selected; } }
		public override Color ButtonPressedGradientMiddle { get { return Theme.Selected; } }
		public override Color ButtonPressedGradientEnd { get { return Theme.Selected; } }
		public override Color ButtonSelectedBorder { get { return Theme.Accent; } }
		public override Color ButtonPressedBorder { get { return Theme.Accent; } }
		public override Color CheckBackground { get { return Theme.Selected; } }
		public override Color CheckSelectedBackground { get { return Theme.Selected; } }
		public override Color CheckPressedBackground { get { return Theme.Selected; } }
		public override Color GripDark { get { return Theme.Dim; } }
		public override Color GripLight { get { return Theme.Dim; } }
		public override Color SeparatorDark { get { return Theme.FieldBorder; } }
		public override Color SeparatorLight { get { return Theme.FieldBorder; } }
	}

	internal class DarkRenderer : ToolStripProfessionalRenderer
	{
		public DarkRenderer() : base(new DarkColors()) { RoundedEdges = false; }

		protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
		{
			Color c = e.Item.ForeColor;
			if (!e.Item.Enabled) c = Theme.Dim;
			else if (c != Theme.Accent && c != Theme.Danger) c = Theme.Text;   // items added later keep the system colour otherwise
			e.TextColor = c;
			base.OnRenderItemText(e);
		}

		protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
		{
			e.ArrowColor = Theme.Muted;
			base.OnRenderArrow(e);
		}
	}

	internal static class UiNative
	{
		[DllImport("dwmapi.dll")]
		public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

		[DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
		public static extern int SetWindowTheme(IntPtr hwnd, string appName, string idList);

		[DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
		public static extern IntPtr SendMessageString(IntPtr hwnd, int msg, IntPtr wParam, string lParam);

		/// <summary>Dark/light title bar and caption colour (Windows 10 1809+ / Windows 11; ignored elsewhere).</summary>
		public static void TitleBar(IntPtr hwnd)
		{
			try
			{
				int dark = Theme.IsDark ? 1 : 0;
				DwmSetWindowAttribute(hwnd, 20, ref dark, 4);   // DWMWA_USE_IMMERSIVE_DARK_MODE
				DwmSetWindowAttribute(hwnd, 19, ref dark, 4);   // same flag on early Windows 10 builds
				int bgr = Theme.Sidebar.R | (Theme.Sidebar.G << 8) | (Theme.Sidebar.B << 16);
				DwmSetWindowAttribute(hwnd, 35, ref bgr, 4);    // DWMWA_CAPTION_COLOR (Windows 11)
				int txt = Theme.Text.R | (Theme.Text.G << 8) | (Theme.Text.B << 16);
				DwmSetWindowAttribute(hwnd, 36, ref txt, 4);    // DWMWA_TEXT_COLOR
			}
			catch { }
		}

		public static void ThemeScrollbars(Control c)
		{
			try
			{
				if (c.IsHandleCreated)
					SetWindowTheme(c.Handle, Theme.IsDark ? "DarkMode_Explorer" : "Explorer", null);
			}
			catch { }
		}
	}
}
