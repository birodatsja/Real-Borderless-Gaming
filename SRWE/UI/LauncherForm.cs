using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using SRWE.Core;

namespace SRWE.UI
{
	internal class LauncherForm : ThemedForm
	{
		private const int HkApplyRestore = 0x5301;
		private const int HkTaskbar = 0x5302;
		private const int HkRestoreDesktop = 0x5303;

		private class Surface : Panel
		{
			public Surface() { DoubleBuffered = true; }
		}

		private const string RestoreDefaultsText = "Restore default presets...";

		private class MonitorItem
		{
			public string Text, Device;
			public override string ToString() { return Text; }
		}

		private readonly ProfileLibrary _library;
		private readonly LauncherSettings _settings;
		private readonly DesktopManager _manager;
		private readonly NotifyIcon _tray = new NotifyIcon();
		private readonly Timer _saveTimer = new Timer();
		private readonly Dictionary<string, Image> _icons = new Dictionary<string, Image>();
		private readonly Font _fontName = Theme.Face(10f, FontStyle.Bold);
		private readonly Font _fontSmall = Theme.Face(8.5f, FontStyle.Regular);
		private readonly Font _fontPill = Theme.Face(8f, FontStyle.Bold);
		private readonly Font _fontLogo = Theme.Face(11f, FontStyle.Bold);

		private GameProfile _current;
		private bool _loading;
		private bool _allowVisible = true;
		private bool _reallyExit;
		private int _hoverIndex = -1;
		private int _primaryIndex;
		private Image _logo;

		// sidebar
		private TextField _search;
		private ListBox _list;
		private ModernButton _btnAdd, _btnDelete;
		private ContextMenuStrip _addMenu, _alignMenu;
		private ModernButton _btnSavePreset, _btnDeletePreset, _settingsBtn, _panicBtn;
		// editor
		private Panel _editor, _content, _left, _bottom;
		private Card _cardGame, _cardWin, _cardTaskbar, _cardAuto;
		private ModernButton _btnApply, _btnRestore;
		private bool _inLayout, _startupBusy;
		private Label _title, _subtitle, _areaLabel, _status;
		private Pill _pill;
		private Control[] _gated;
		private TextField _nameBox, _exeBox;
		private DropDownField _presetBox, _monitorBox;
		private NumberField _wNum, _hNum, _xNum, _yNum, _delayNum;
		private ToggleSwitch _borderlessChk, _hideTbChk, _allTbChk, _autoChk, _exitSizeChk;
		private ModernButton _alignBtn;
		// bottom bar
		private ToggleSwitch _startupChk, _trayChk, _autoAllChk;
		private ToolStripMenuItem _trayAutoItem;

		public LauncherForm(bool startHidden)
		{
			_library = AppState.Library;
			_settings = AppState.Settings;
			_manager = new DesktopManager(_library, _settings);

			BuildUi();
			BuildTray();

			_saveTimer.Interval = 600;
			_saveTimer.Tick += delegate { _saveTimer.Stop(); SaveNow(); };

			_manager.StateChanged += delegate { _list.Invalidate(); UpdateHeader(); };
			_manager.Notice += OnNotice;

			RefreshList();
			if (_list.Items.Count > 0) _list.SelectedIndex = 0; else LoadEditor();

			if (startHidden) { _allowVisible = false; CreateHandle(); }
			_manager.Start();
		}

		// ------------------------------------------------------------ construction helpers

		private Label MakeLabel(Control parent, string text, int x, int y, bool muted)
		{
			Label l = new Label();
			l.Text = text;
			l.AutoSize = true;
			l.Location = new Point(x, y);
			l.Font = Theme.Face(8.5f, FontStyle.Regular);
			parent.Controls.Add(l);
			return Theme.Role(l, muted ? "f:muted" : "f:text");
		}

		private Label MakeTitle(Control parent, string text, int x, int y)
		{
			Label l = new Label();
			l.Text = text;
			l.AutoSize = true;
			l.Location = new Point(x, y);
			l.Font = Theme.Face(10.5f, FontStyle.Bold);
			parent.Controls.Add(l);
			return Theme.Role(l, "f:text");
		}

		private Label MakeHint(Control parent, string text, int x, int y, int w, int h)
		{
			Label l = new Label();
			l.Text = text;
			l.AutoSize = false;
			l.Size = new Size(w, h);
			l.Location = new Point(x, y);
			l.Font = Theme.Face(8.5f, FontStyle.Regular);
			parent.Controls.Add(l);
			return Theme.Role(l, "f:dim");
		}

		private TextField MakeText(Control parent, int x, int y, int w)
		{
			TextField t = new TextField();
			t.Location = new Point(x, y);
			t.Width = w;
			t.TextChanged += OnEditorChanged;
			parent.Controls.Add(t);
			return t;
		}

		private NumberField MakeNum(Control parent, int x, int y, int w, int min, int max)
		{
			NumberField n = new NumberField();
			n.Location = new Point(x, y);
			n.Width = w;
			n.Minimum = min;
			n.Maximum = max;
			n.ValueChanged += OnEditorChanged;
			parent.Controls.Add(n);
			return n;
		}

		private ToggleSwitch MakeToggle(Control parent, string text, int x, int y, int w)
		{
			ToggleSwitch t = new ToggleSwitch();
			t.Text = text;
			t.Location = new Point(x, y);
			t.Width = w;
			parent.Controls.Add(t);
			return t;
		}

		private DropDownField MakeDrop(Control parent, int x, int y, int w)
		{
			DropDownField d = new DropDownField();
			d.Location = new Point(x, y);
			d.Width = w;
			parent.Controls.Add(d);
			return d;
		}

		private ModernButton MakeButton(Control parent, string text, int x, int y, int w, int h, ButtonKind kind)
		{
			ModernButton b = new ModernButton();
			b.Text = text;
			b.Kind = kind;
			b.Location = new Point(x, y);
			b.Size = new Size(w, h);
			parent.Controls.Add(b);
			return b;
		}

		private Card MakeCard(Control parent, int x, int y, int w, int h)
		{
			Card c = new Card();
			c.Location = new Point(x, y);
			c.Size = new Size(w, h);
			parent.Controls.Add(c);
			return c;
		}

		// ------------------------------------------------------------ UI construction

		private void BuildUi()
		{
			Text = AppInfo.Name;
			ClientSize = new Size(1100, 720);
			Rectangle wa = Screen.PrimaryScreen.WorkingArea;      // small laptop screens: fit, and scroll the editor
			if (Height > wa.Height - 20) Height = wa.Height - 20;
			MinimumSize = new Size(740, 520);
			StartPosition = FormStartPosition.CenterScreen;
			RestoreSavedBounds();
			try
			{
				Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
				_logo = new Icon(Icon, new Size(32, 32)).ToBitmap();
			}
			catch { }

			// Fill control first, then the docked edges (WinForms lays out the last-added first).
			_editor = new Surface();
			_editor.Dock = DockStyle.Fill;
			Theme.Role(_editor, "b:bg");
			Controls.Add(_editor);

			_left = new Surface();
			_left.Dock = DockStyle.Left;
			_left.Width = 300;
			Theme.Role(_left, "b:sidebar");
			Controls.Add(_left);

			_bottom = new Surface();
			_bottom.Dock = DockStyle.Bottom;
			_bottom.Height = 64;
			Theme.Role(_bottom, "b:sidebar");
			Controls.Add(_bottom);

			BuildSidebar(_left);
			BuildEditor(_editor);
			BuildBottomBar(_bottom);
			DoLayout();
		}

		private void RestoreSavedBounds()
		{
			if (_settings.WindowW < 740 || _settings.WindowH < 520) return;
			Rectangle saved = new Rectangle(_settings.WindowX, _settings.WindowY, _settings.WindowW, _settings.WindowH);
			foreach (Screen sc in Screen.AllScreens)
			{
				if (!sc.WorkingArea.IntersectsWith(saved)) continue;   // the monitor it was on may be gone
				StartPosition = FormStartPosition.Manual;
				Bounds = saved;
				return;
			}
		}

		private void SaveWindowBounds()
		{
			if (WindowState != FormWindowState.Normal) return;
			_settings.WindowX = Left;
			_settings.WindowY = Top;
			_settings.WindowW = Width;
			_settings.WindowH = Height;
		}

		// ------------------------------------------------------------ responsive layout

		/// <summary>
		/// Positions everything from the window's client size (not from panel resize events, which don't
		/// reliably fire on startup). Called on load, on every resize and when the window is shown again.
		/// </summary>
		private void DoLayout()
		{
			if (_inLayout || _left == null || _bottom == null || _content == null || _settingsBtn == null) return;
			_inLayout = true;
			try
			{
				int cw = ClientSize.Width, ch = ClientSize.Height;
				int leftW = cw >= 1000 ? 300 : 240;
				int leftH = Math.Max(100, ch - _bottom.Height);
				if (_left.Width != leftW) _left.Width = leftW;

				// sidebar
				_search.Width = leftW - 32;
				_list.Width = leftW;
				_list.Height = Math.Max(60, leftH - 116 - 68);
				_btnDelete.Width = 64;
				_btnDelete.Left = leftW - 16 - 64;
				_btnAdd.Width = leftW - 32 - 64 - 8;
				_btnAdd.Top = leftH - 56;
				_btnDelete.Top = leftH - 56;
				_left.Invalidate();

				// bottom bar: the three switches also live in Settings, so drop them when space runs out
				bool roomy = cw >= 1040;
				_autoAllChk.Visible = roomy;
				_trayChk.Visible = roomy;
				_startupChk.Visible = roomy;
				_settingsBtn.Left = cw - _settingsBtn.Width - 16;

				// editor: two columns when there is room, one stacked column otherwise
				int ew = cw - leftW;
				LayoutContent(ew >= 790);
				_content.Left = Math.Max(8, (ew - _content.Width) / 2);
				_content.Top = Math.Max(0, (leftH - _content.Height) / 3);
				_editor.AutoScrollMinSize = new Size(_content.Width + 16, _content.Height + 8);
				UpdateHeader();
			}
			finally { _inLayout = false; }
		}

		private void LayoutContent(bool wide)
		{
			if (wide)
			{
				_content.Size = new Size(726, 630);
				_cardGame.Location = new Point(0, 88);
				_cardWin.Location = new Point(0, 218);
				_cardTaskbar.SetBounds(446, 88, 280, 148);
				_cardAuto.SetBounds(446, 248, 280, 306);
				_btnApply.Location = new Point(0, 574);
				_btnRestore.Location = new Point(160, 574);
				_status.SetBounds(330, 574, 396, 40);
			}
			else
			{
				_content.Size = new Size(430, 1066);
				_cardGame.Location = new Point(0, 88);
				_cardWin.Location = new Point(0, 218);
				_cardTaskbar.SetBounds(0, 566, 430, 148);
				_cardAuto.SetBounds(0, 726, 430, 240);
				_btnApply.Location = new Point(0, 978);
				_btnRestore.Location = new Point(160, 978);
				_status.SetBounds(0, 1026, 430, 28);
			}
			_title.Width = Math.Max(160, _content.Width - 130);
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			DoLayout();
		}

		protected override void OnSizeChanged(EventArgs e)
		{
			base.OnSizeChanged(e);
			DoLayout();
		}

		private void BuildSidebar(Panel left)
		{
			left.Paint += delegate(object s, PaintEventArgs e)
			{
				e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
				e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
				if (_logo != null) e.Graphics.DrawImage(_logo, new Rectangle(18, 16, 32, 32));
				TextFormatFlags cut = TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
				TextRenderer.DrawText(e.Graphics, AppInfo.Name, _fontLogo,
					new Rectangle(60, 14, Math.Max(40, left.Width - 70), 22), Theme.Text, cut);
				TextRenderer.DrawText(e.Graphics, "Window profiles for games", _fontSmall,
					new Rectangle(61, 35, Math.Max(40, left.Width - 70), 18), Theme.Muted, cut);
			};

			_search = new TextField();
			_search.Location = new Point(16, 68);
			_search.Size = new Size(268, 36);
			_search.Placeholder = "Search games";
			_search.TextChanged += delegate { RefreshList(); };
			left.Controls.Add(_search);

			_list = new ListBox();
			_list.Location = new Point(0, 116);
			_list.Size = new Size(300, 440);
			Theme.Role(_list, "b:sidebar");
			_list.BorderStyle = BorderStyle.None;
			_list.DrawMode = DrawMode.OwnerDrawFixed;
			_list.ItemHeight = 64;
			_list.IntegralHeight = false;
			_list.DrawItem += List_DrawItem;
			_list.SelectedIndexChanged += List_SelectedIndexChanged;
			_list.HandleCreated += delegate { UiNative.ThemeScrollbars(_list); };
			_list.MouseDown += List_MouseDown;
			_list.MouseMove += delegate(object s, MouseEventArgs me)
			{
				int i = _list.IndexFromPoint(me.Location);
				if (i != _hoverIndex) { _hoverIndex = i; _list.Invalidate(); }
			};
			_list.MouseLeave += delegate { if (_hoverIndex != -1) { _hoverIndex = -1; _list.Invalidate(); } };
			left.Controls.Add(_list);

			_btnAdd = MakeButton(left, "+  Add game", 16, 0, 196, 40, ButtonKind.Primary);
			_btnDelete = MakeButton(left, "Delete", 220, 0, 64, 40, ButtonKind.Secondary);

			_addMenu = new ContextMenuStrip();
			_addMenu.Items.Add("From a running process...", null, delegate { AddFromRunningProcess(); });
			_addMenu.Items.Add("Browse for an executable...", null, delegate { AddFromFile(); });
			Theme.StyleMenu(_addMenu);
			_btnAdd.Click += delegate { _addMenu.Show(_btnAdd.PointToScreen(new Point(0, 0)), ToolStripDropDownDirection.AboveRight); };
			_btnDelete.Click += delegate { DeleteCurrent(); };
		}

		private void BuildEditor(Panel p)
		{
			_content = new Panel();
			_content.Size = new Size(726, 630);
			Theme.Role(_content, "b:bg");
			p.Controls.Add(_content);
			p.AutoScroll = true;
			p.AutoScrollMinSize = new Size(_content.Width + 16, _content.Height + 8);

			// header
			_title = new Label();
			_title.AutoSize = false;
			_title.AutoEllipsis = true;
			_title.Location = new Point(0, 6);
			_title.Size = new Size(560, 38);
			_title.TextAlign = ContentAlignment.MiddleLeft;
			_title.Font = Theme.Face(20f, FontStyle.Bold);
			_content.Controls.Add(Theme.Role(_title, "f:text"));
			_subtitle = MakeLabel(_content, "", 2, 50, true);
			_subtitle.Font = Theme.Face(9.5f, FontStyle.Regular);
			_pill = new Pill();
			_pill.Location = new Point(600, 14);
			_content.Controls.Add(_pill);

			// ---- Game card (left column) ----
			Card game = MakeCard(_content, 0, 88, 430, 118);
			MakeTitle(game, "Game", 20, 12);
			MakeLabel(game, "Name", 20, 38, true);
			_nameBox = MakeText(game, 20, 56, 190);
			MakeLabel(game, "Executable", 218, 38, true);
			_exeBox = MakeText(game, 218, 56, 138);
			ModernButton browse = MakeButton(game, "...", 362, 56, 48, 34, ButtonKind.Secondary);
			browse.Click += delegate { BrowseExe(); };

			// ---- Window area card ----
			Card win = MakeCard(_content, 0, 218, 430, 336);
			MakeTitle(win, "Window area", 20, 12);
			_areaLabel = new Label();
			_areaLabel.AutoSize = true;
			_areaLabel.Location = new Point(20, 40);
			_areaLabel.Font = Theme.Face(10f, FontStyle.Bold);
			win.Controls.Add(Theme.Role(_areaLabel, "f:accent"));

			_monitorBox = MakeDrop(win, 20, 76, 176);
			MakeMonitorItems();
			_alignBtn = MakeButton(win, "Align", 204, 76, 70, 34, ButtonKind.Secondary);
			ModernButton fill = MakeButton(win, "Fill display", 282, 76, 128, 34, ButtonKind.Secondary);
			ModernButton pick = MakeButton(win, "Select area on screen...", 20, 120, 390, 34, ButtonKind.Secondary);
			BuildAlignMenu();
			_alignBtn.Click += delegate { _alignMenu.Show(_alignBtn, new Point(0, _alignBtn.Height + 4)); };
			fill.Click += delegate { Rectangle b = SelectedBounds(); SetArea(b.X, b.Y, b.Width, b.Height); };
			pick.Click += delegate { SelectAreaOnScreen(); };

			MakeLabel(win, "X", 20, 166, true);
			_xNum = MakeNum(win, 20, 184, 90, -20000, 20000);
			MakeLabel(win, "Y", 118, 166, true);
			_yNum = MakeNum(win, 118, 184, 90, -20000, 20000);
			MakeLabel(win, "Width", 216, 166, true);
			_wNum = MakeNum(win, 216, 184, 90, 100, 20000);
			MakeLabel(win, "Height", 314, 166, true);
			_hNum = MakeNum(win, 314, 184, 90, 100, 20000);

			MakeLabel(win, "Resolution preset (centred on the display)", 20, 228, true);
			_presetBox = MakeDrop(win, 20, 246, 176);
			RebuildPresetItems();
			_presetBox.SelectedIndexChanged += PresetChanged;
			_btnSavePreset = MakeButton(win, "Save resolution", 204, 246, 126, 34, ButtonKind.Secondary);
			_btnDeletePreset = MakeButton(win, "Delete", 338, 246, 72, 34, ButtonKind.Secondary);
			_btnSavePreset.Click += delegate { SaveResolution(); };
			_btnDeletePreset.Click += delegate { DeleteSelectedPreset(); };
			_borderlessChk = MakeToggle(win, "Borderless", 20, 296, 390);

			// ---- Taskbar card (right column) ----
			Card tb = MakeCard(_content, 446, 88, 280, 148);
			MakeTitle(tb, "Taskbar", 20, 12);
			_hideTbChk = MakeToggle(tb, "Hide taskbar while active", 20, 46, 240);
			_allTbChk = MakeToggle(tb, "On all displays", 20, 80, 240);
			MakeHint(tb, "Off: only displays the game covers.", 20, 112, 240, 24);

			// ---- Automation card ----
			Card auto = MakeCard(_content, 446, 248, 280, 306);
			MakeTitle(auto, "Automation", 20, 12);
			_autoChk = MakeToggle(auto, "Apply when the game starts", 20, 46, 240);
			_exitSizeChk = MakeToggle(auto, "Send WM_EXITSIZEMOVE", 20, 80, 240);
			MakeHint(auto, "Some games only react to a resize after this message.", 20, 110, 240, 34);
			MakeLabel(auto, "Wait after the window appears (ms)", 20, 160, true);
			_delayNum = MakeNum(auto, 20, 180, 140, 0, 60000);
			_delayNum.Step = 100;

			// ---- actions ----
			ModernButton apply = MakeButton(_content, "Apply", 0, 574, 150, 40, ButtonKind.Primary);
			ModernButton restore = MakeButton(_content, "Restore", 160, 574, 150, 40, ButtonKind.Secondary);
			apply.Click += delegate { ApplyCurrent(); };
			restore.Click += delegate { RestoreCurrent(); };

			_status = new Label();
			_status.AutoSize = false;
			_status.AutoEllipsis = true;
			_status.Location = new Point(330, 574);
			_status.Size = new Size(396, 40);
			_status.TextAlign = ContentAlignment.MiddleLeft;
			_content.Controls.Add(Theme.Role(_status, "f:muted"));

			_cardGame = game; _cardWin = win; _cardTaskbar = tb; _cardAuto = auto;
			_btnApply = apply; _btnRestore = restore;
			_gated = new Control[] { game, win, tb, auto, apply, restore };

			_borderlessChk.CheckedChanged += OnEditorChanged;
			_hideTbChk.CheckedChanged += OnEditorChanged;
			_allTbChk.CheckedChanged += OnEditorChanged;
			_autoChk.CheckedChanged += OnEditorChanged;
			_exitSizeChk.CheckedChanged += OnEditorChanged;
			_monitorBox.SelectedIndexChanged += OnEditorChanged;
		}

		private void BuildAlignMenu()
		{
			_alignMenu = new ContextMenuStrip();
			AddAlignItem("Left edge", "left");
			AddAlignItem("Center horizontally", "hcenter");
			AddAlignItem("Right edge", "right");
			_alignMenu.Items.Add(new ToolStripSeparator());
			AddAlignItem("Top edge", "top");
			AddAlignItem("Center vertically", "vcenter");
			AddAlignItem("Bottom edge", "bottom");
			Theme.StyleMenu(_alignMenu);
		}

		private void AddAlignItem(string text, string how)
		{
			_alignMenu.Items.Add(text, null, delegate { AlignArea(how); });
		}

		private void BuildBottomBar(Panel bar)
		{
			bar.Paint += delegate(object s, PaintEventArgs e)
			{
				using (Pen pen = new Pen(Theme.CardBorder))
					e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
			};

			_panicBtn = MakeButton(bar, "Restore Desktop", 16, 14, 290, 36, ButtonKind.Danger);
			_panicBtn.Click += delegate { RestoreDesktopNow(); };
			UpdatePanicText();

			_autoAllChk = MakeToggle(bar, "Auto-activate profiles", 330, 19, 186);
			_autoAllChk.Checked = _settings.AutoActivateEnabled;
			_trayChk = MakeToggle(bar, "Close to tray", 536, 19, 130);
			_trayChk.Checked = _settings.MinimizeToTray;
			_startupChk = MakeToggle(bar, "Start with Windows", 686, 19, 170);
			_startupChk.Checked = StartupManager.IsEnabled();

			_settingsBtn = MakeButton(bar, "\u2699  Settings", 0, 14, 124, 36, ButtonKind.Secondary);
			_settingsBtn.Click += delegate { ShowSettingsMenu(); };

			_autoAllChk.CheckedChanged += delegate
			{
				_settings.AutoActivateEnabled = _autoAllChk.Checked;
				if (_trayAutoItem != null) _trayAutoItem.Checked = _autoAllChk.Checked;
				AppState.SaveSettings();
			};
			_trayChk.CheckedChanged += delegate
			{
				_settings.MinimizeToTray = _trayChk.Checked;
				AppState.SaveSettings();
			};
			_startupChk.CheckedChanged += delegate
			{
				if (_startupBusy) return;
				bool want = _startupChk.Checked;
				if (!StartupManager.Set(want))
				{
					_startupBusy = true;
					_startupChk.Checked = !want;
					_startupBusy = false;
					SetStatus("Could not change the startup task (see %AppData%\\" + AppInfo.DataDir + "\\rbg.log).", true);
				}
			};
		}

		private void BuildTray()
		{
			try { _tray.Icon = Icon; } catch { }
			_tray.Text = AppInfo.Name;
			ContextMenuStrip menu = new ContextMenuStrip();
			menu.Items.Add("Open " + AppInfo.Name, null, delegate { ShowMe(); });
			menu.Items.Add("Restore Desktop", null, delegate { RestoreDesktopNow(); });
			_trayAutoItem = new ToolStripMenuItem("Auto-activate profiles");
			_trayAutoItem.CheckOnClick = true;
			_trayAutoItem.Checked = _settings.AutoActivateEnabled;
			_trayAutoItem.Click += delegate { _autoAllChk.Checked = _trayAutoItem.Checked; };
			menu.Items.Add(_trayAutoItem);
			menu.Items.Add(new ToolStripSeparator());
			menu.Items.Add("Exit", null, delegate { _reallyExit = true; Close(); });
			Theme.StyleMenu(menu);
			menu.ShowCheckMargin = true;
			_tray.ContextMenuStrip = menu;
			_tray.DoubleClick += delegate { ShowMe(); };
			_tray.Visible = true;
		}

		private void MakeMonitorItems()
		{
			_monitorBox.Items.Clear();
			_primaryIndex = 0;
			int fallback = 1;
			foreach (Screen sc in Screen.AllScreens)
			{
				// "\\.\DISPLAY2" -> 2, so the numbers match Windows' own display numbering
				string num = "";
				string dn = sc.DeviceName;
				for (int i = dn.Length - 1; i >= 0 && char.IsDigit(dn[i]); i--) num = dn[i] + num;
				MonitorItem mi = new MonitorItem();
				mi.Device = sc.DeviceName;
				mi.Text = "Display " + (num.Length > 0 ? num : fallback.ToString()) + " \u00B7 " + sc.Bounds.Width + " \u00D7 " + sc.Bounds.Height;
				if (sc.Primary) _primaryIndex = _monitorBox.Items.Count;
				_monitorBox.Items.Add(mi);
				fallback++;
			}
			if (Screen.AllScreens.Length > 1)
			{
				Rectangle v = SystemInformation.VirtualScreen;
				MonitorItem all = new MonitorItem();
				all.Device = "*";
				all.Text = "All displays \u00B7 " + v.Width + " \u00D7 " + v.Height;
				_monitorBox.Items.Add(all);
			}
			_monitorBox.SelectedIndex = _primaryIndex;
		}

		// ------------------------------------------------------------ library list

		private void RefreshList()
		{
			string q = _search.Text.Trim().ToLowerInvariant();
			GameProfile keep = _current;
			_list.BeginUpdate();
			_list.Items.Clear();
			foreach (GameProfile gp in _library.Profiles)
			{
				if (q.Length > 0 && gp.Name.ToLowerInvariant().IndexOf(q) < 0 && gp.ExeName.ToLowerInvariant().IndexOf(q) < 0) continue;
				_list.Items.Add(gp);
			}
			_list.EndUpdate();
			if (keep != null && _list.Items.Contains(keep)) _list.SelectedItem = keep;
		}

		private Image IconFor(GameProfile gp)
		{
			Image img;
			if (_icons.TryGetValue(gp.Id, out img)) return img;
			img = null;
			try
			{
				if (!string.IsNullOrEmpty(gp.ExePath) && File.Exists(gp.ExePath))
					using (Icon ic = Icon.ExtractAssociatedIcon(gp.ExePath)) img = ic.ToBitmap();
			}
			catch { }
			_icons[gp.Id] = img;
			return img;
		}

		private void List_DrawItem(object sender, DrawItemEventArgs e)
		{
			Graphics g = e.Graphics;
			using (SolidBrush bg = new SolidBrush(Theme.Sidebar))
				g.FillRectangle(bg, e.Bounds);
			if (e.Index < 0) return;

			GameProfile gp = (GameProfile)_list.Items[e.Index];
			bool sel = (e.State & DrawItemState.Selected) != 0;
			Rectangle card = new Rectangle(e.Bounds.X + 10, e.Bounds.Y + 3, e.Bounds.Width - 20, e.Bounds.Height - 6);
			g.SmoothingMode = SmoothingMode.AntiAlias;
			if (sel) Theme.FillRound(g, card, 10, Theme.Selected);
			else if (e.Index == _hoverIndex) Theme.FillRound(g, card, 10, Theme.Card);

			Rectangle av = new Rectangle(card.X + 12, card.Y + (card.Height - 36) / 2, 36, 36);
			Image icon = IconFor(gp);
			if (icon != null)
			{
				g.InterpolationMode = InterpolationMode.HighQualityBicubic;
				g.DrawImage(icon, av);
			}
			else
			{
				Theme.FillRound(g, av, 9, Theme.Field);
				string letter = gp.Name.Length > 0 ? gp.Name.Substring(0, 1).ToUpperInvariant() : "?";
				TextRenderer.DrawText(g, letter, _fontName, av, Theme.Text,
					TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
			}

			int reserve = 12;
			ProfileState st = _manager.GetState(gp);
			if (st != ProfileState.Idle)
			{
				string t = st == ProfileState.Active ? "Active" : "Running";
				Color c = st == ProfileState.Active ? Theme.Good : Theme.Warn;
				int pw = TextRenderer.MeasureText(t, _fontPill).Width + 22;
				Rectangle pr = new Rectangle(card.Right - pw - 10, card.Y + (card.Height - 20) / 2, pw, 20);
				Theme.FillRound(g, pr, 10, Color.FromArgb(44, c));
				using (SolidBrush b = new SolidBrush(c))
					g.FillEllipse(b, pr.X + 8, pr.Y + 7, 6, 6);
				TextRenderer.DrawText(g, t, _fontPill, new Rectangle(pr.X + 16, pr.Y, pr.Width - 16, pr.Height), c,
					TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
				reserve = pw + 20;
			}

			int tx = av.Right + 12;
			int tw = Math.Max(20, card.Right - tx - reserve);
			TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
				TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
			TextRenderer.DrawText(g, gp.Name, _fontName, new Rectangle(tx, card.Y + 9, tw, 22), Theme.Text, flags);
			TextRenderer.DrawText(g, gp.ExeName, _fontSmall, new Rectangle(tx, card.Y + 31, tw, 18), Theme.Muted, flags);
		}

		private void List_SelectedIndexChanged(object sender, EventArgs e)
		{
			_current = _list.SelectedItem as GameProfile;
			LoadEditor();
		}

		// ------------------------------------------------------------ add / delete

		private void AddFromRunningProcess()
		{
			using (ProcessPickerForm picker = new ProcessPickerForm(false))
			{
				picker.AddRequested += OnPickerAdd;
				picker.ShowDialog(this);
			}
		}

		/// <summary>Picker asked to add a process, optionally with a saved resolution preset applied right away.</summary>
		private void OnPickerAdd(ProcItem p, ResolutionPreset preset)
		{
			GameProfile gp = AddProfile(p.Name + ".exe", p.Path, preset);
			if (preset == null) { SetStatus("Added " + gp.Name + ".", false); return; }
			string err = _manager.ApplyNow(gp);
			SetStatus(err ?? "Added " + gp.Name + " and applied " + preset + ".", err != null);
		}

		private void List_MouseDown(object sender, MouseEventArgs e)
		{
			if (e.Button != MouseButtons.Right) return;
			int i = _list.IndexFromPoint(e.Location);
			if (i < 0) return;
			_list.SelectedIndex = i;
			ShowListMenu(e.Location);
		}

		private void ShowListMenu(Point where)
		{
			if (_current == null) return;
			ContextMenuStrip m = new ContextMenuStrip();
			m.Items.Add("Apply", null, delegate { ApplyCurrent(); });
			m.Items.Add("Restore", null, delegate { RestoreCurrent(); });
			ToolStripMenuItem sub = new ToolStripMenuItem("Apply resolution preset");
			foreach (ResolutionPreset rp in _settings.Presets)
			{
				ResolutionPreset cap = rp;
				sub.DropDownItems.Add(cap.ToString(), null, delegate { ApplyPresetToCurrent(cap); });
			}
			if (sub.DropDownItems.Count == 0)
			{
				ToolStripItem none = sub.DropDownItems.Add("No saved presets");
				none.Enabled = false;
			}
			m.Items.Add(sub);
			m.Items.Add(new ToolStripSeparator());
			m.Items.Add("Delete", null, delegate { DeleteCurrent(); });
			Theme.StyleMenu(m);
			m.Show(_list, where);
		}

		private void ApplyPresetToCurrent(ResolutionPreset pr)
		{
			Rectangle b = SelectedBounds();
			SetArea(b.X + (b.Width - pr.Width) / 2, b.Y + (b.Height - pr.Height) / 2, pr.Width, pr.Height);
			ApplyCurrent();
		}

		private void AddFromFile()
		{
			using (OpenFileDialog ofd = new OpenFileDialog())
			{
				ofd.Filter = "Executables (*.exe)|*.exe";
				if (ofd.ShowDialog(this) != DialogResult.OK) return;
				AddProfile(Path.GetFileName(ofd.FileName), ofd.FileName, null);
			}
		}

		/// <summary>Adds a profile (or finds the existing one). With a preset, the area becomes that size, centred.</summary>
		private GameProfile AddProfile(string exe, string path, ResolutionPreset preset)
		{
			GameProfile gp = null;
			foreach (GameProfile existing in _library.Profiles)
				if (string.Equals(existing.ExeName, exe, StringComparison.OrdinalIgnoreCase)) { gp = existing; break; }

			if (gp == null)
			{
				gp = new GameProfile();
				gp.Name = Path.GetFileNameWithoutExtension(exe);
				gp.ExeName = exe;
				gp.ExePath = path;
				Rectangle pb = Screen.PrimaryScreen.Bounds;   // start as borderless fullscreen on the primary display
				gp.PosX = pb.X; gp.PosY = pb.Y; gp.Width = pb.Width; gp.Height = pb.Height;
				gp.Monitor = Screen.PrimaryScreen.DeviceName;
				_library.Profiles.Add(gp);
			}
			else if (string.IsNullOrEmpty(gp.ExePath) && !string.IsNullOrEmpty(path)) gp.ExePath = path;

			if (preset != null)
			{
				Rectangle b = BoundsOf(gp.Monitor);
				gp.Width = preset.Width;
				gp.Height = preset.Height;
				gp.PosX = b.X + (b.Width - preset.Width) / 2;
				gp.PosY = b.Y + (b.Height - preset.Height) / 2;
			}

			SaveNow();
			_search.Text = "";
			RefreshList();
			_list.SelectedItem = gp;
			return gp;
		}

		private void BrowseExe()
		{
			using (OpenFileDialog ofd = new OpenFileDialog())
			{
				ofd.Filter = "Executables (*.exe)|*.exe";
				if (ofd.ShowDialog(this) != DialogResult.OK) return;
				_exeBox.Text = Path.GetFileName(ofd.FileName);
				if (_current != null)
				{
					_current.ExePath = ofd.FileName;
					_icons.Remove(_current.Id);
					_list.Invalidate();
				}
			}
		}

		private void DeleteCurrent()
		{
			if (_current == null) return;
			if (MessageBox.Show(this, "Delete the profile \"" + _current.Name + "\"?", AppInfo.Name,
				MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
			_manager.Restore(_current);
			_library.Profiles.Remove(_current);
			_current = null;
			SaveNow();
			RefreshList();
			if (_list.Items.Count > 0) _list.SelectedIndex = 0; else LoadEditor();
		}

		// ------------------------------------------------------------ editor <-> profile

		private void UpdateHeader()
		{
			if (_current == null)
			{
				_title.Text = "No game selected";
				_subtitle.Text = "Add a game to create its profile.";
				_pill.Visible = false;
				return;
			}
			_title.Text = string.IsNullOrEmpty(_current.Name) ? "Untitled" : _current.Name;
			_subtitle.Text = _current.ExeName;
			ProfileState st = _manager.GetState(_current);
			if (st == ProfileState.Active) _pill.SetState("Active", Theme.Good);
			else if (st == ProfileState.Running) _pill.SetState("Running", Theme.Warn);
			else _pill.SetState("Not running", Theme.Dim);
			_pill.Location = new Point(_content.Width - _pill.Width, 14);
		}

		private void LoadEditor()
		{
			_loading = true;
			try
			{
				foreach (Control c in _gated) c.Enabled = _current != null;
				if (_current == null) { _nameBox.Text = ""; _exeBox.Text = ""; _areaLabel.Text = ""; UpdateHeader(); return; }
				_nameBox.Text = _current.Name;
				_exeBox.Text = _current.ExeName;
				_xNum.Value = Clamp(_current.PosX, _xNum);
				_yNum.Value = Clamp(_current.PosY, _yNum);
				_wNum.Value = Clamp(_current.Width, _wNum);
				_hNum.Value = Clamp(_current.Height, _hNum);
				_borderlessChk.Checked = _current.Borderless;
				_hideTbChk.Checked = _current.HideTaskbar;
				_allTbChk.Checked = _current.TaskbarAllMonitors;
				_autoChk.Checked = _current.AutoActivate;
				_exitSizeChk.Checked = _current.ForceExitSizeMove;
				_delayNum.Value = Clamp(_current.ApplyDelayMs, _delayNum);

				_monitorBox.SelectedIndex = _primaryIndex;
				for (int i = 0; i < _monitorBox.Items.Count; i++)
					if (((MonitorItem)_monitorBox.Items[i]).Device == (_current.Monitor ?? ""))
						_monitorBox.SelectedIndex = i;
				SyncPresetBox();
				UpdateAreaLabel();
				UpdateEnabledStates();
				UpdateHeader();
			}
			finally { _loading = false; }
		}

		private static decimal Clamp(int v, NumberField n)
		{
			if (v < n.Minimum) return n.Minimum;
			if (v > n.Maximum) return n.Maximum;
			return v;
		}

		private void ReadEditor()
		{
			if (_current == null) return;
			_current.Name = _nameBox.Text;
			_current.ExeName = _exeBox.Text.Trim();
			_current.PosX = (int)_xNum.Value;
			_current.PosY = (int)_yNum.Value;
			_current.Width = (int)_wNum.Value;
			_current.Height = (int)_hNum.Value;
			_current.Borderless = _borderlessChk.Checked;
			_current.HideTaskbar = _hideTbChk.Checked;
			_current.TaskbarAllMonitors = _allTbChk.Checked;
			_current.AutoActivate = _autoChk.Checked;
			_current.ForceExitSizeMove = _exitSizeChk.Checked;
			_current.ApplyDelayMs = (int)_delayNum.Value;
			MonitorItem mi = _monitorBox.SelectedItem as MonitorItem;
			_current.Monitor = mi == null ? "" : mi.Device;
		}

		private void OnEditorChanged(object sender, EventArgs e)
		{
			if (_loading || _current == null) return;
			ReadEditor();
			UpdateEnabledStates();
			UpdateAreaLabel();
			UpdateHeader();
			if (sender == _wNum || sender == _hNum) SyncPresetBox();
			_list.Invalidate();
			_saveTimer.Stop();
			_saveTimer.Start();
		}

		private void UpdateEnabledStates()
		{
			_allTbChk.Enabled = _hideTbChk.Checked && _current != null;
		}

		private void UpdateAreaLabel()
		{
			_areaLabel.Text = (int)_wNum.Value + " \u00D7 " + (int)_hNum.Value + " pixels at X " +
				(int)_xNum.Value + ", Y " + (int)_yNum.Value;
		}

		private void RebuildPresetItems()
		{
			bool was = _loading;
			_loading = true;
			_presetBox.Items.Clear();
			_presetBox.Items.Add("Custom");
			foreach (ResolutionPreset rp in _settings.Presets) _presetBox.Items.Add(rp);
			_presetBox.Items.Add(RestoreDefaultsText);
			_presetBox.SelectedIndex = 0;
			_loading = was;
			if (_wNum != null) SyncPresetBox();
		}

		private void SyncPresetBox()
		{
			bool was = _loading;
			_loading = true;
			_presetBox.SelectedIndex = 0;
			for (int i = 1; i < _presetBox.Items.Count; i++)
			{
				ResolutionPreset pr = _presetBox.Items[i] as ResolutionPreset;
				if (pr != null && pr.Width == (int)_wNum.Value && pr.Height == (int)_hNum.Value) { _presetBox.SelectedIndex = i; break; }
			}
			_loading = was;
			if (_btnDeletePreset != null) _btnDeletePreset.Enabled = _presetBox.SelectedItem is ResolutionPreset && _current != null;
		}

		/// <summary>"Save resolution": remember the current width x height as a named preset.</summary>
		private void SaveResolution()
		{
			if (_current == null) return;
			int w = (int)_wNum.Value, h = (int)_hNum.Value;
			string size = PresetService.Size(w, h);
			ResolutionPreset existing = PresetService.Find(_settings, w, h);
			string initial = existing != null && !string.IsNullOrEmpty(existing.Name) ? existing.Name : size;
			string name;
			using (PromptForm pf = new PromptForm("Save resolution", "Name for " + size + " (leave as is for just the size)", initial))
			{
				if (pf.ShowDialog(this) != DialogResult.OK) return;
				name = pf.Value;
			}
			if (name == size) name = "";
			if (existing != null) existing.Name = name;
			else
			{
				existing = new ResolutionPreset(name, w, h);
				_settings.Presets.Add(existing);
			}
			AppState.SaveSettings();
			RebuildPresetItems();
			SetStatus("Saved resolution " + existing + ".", false);
		}

		private void DeleteSelectedPreset()
		{
			ResolutionPreset pr = _presetBox.SelectedItem as ResolutionPreset;
			if (pr == null) return;
			if (MessageBox.Show(this, "Delete the preset " + pr + "?\n\nProfiles that already use this size are not changed.",
				AppInfo.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
			_settings.Presets.Remove(pr);
			AppState.SaveSettings();
			RebuildPresetItems();
			SetStatus("Deleted preset " + pr + ".", false);
		}

		// ---- area helpers (match Windows' "Custom area" dialog) ----

		private static Rectangle BoundsOf(string device)
		{
			if (device == "*") return SystemInformation.VirtualScreen;
			if (!string.IsNullOrEmpty(device))
				foreach (Screen sc in Screen.AllScreens)
					if (sc.DeviceName == device) return sc.Bounds;
			return Screen.PrimaryScreen.Bounds;
		}

		private Rectangle SelectedBounds()
		{
			MonitorItem mi = _monitorBox.SelectedItem as MonitorItem;
			return BoundsOf(mi == null ? "" : mi.Device);
		}

		/// <summary>Set all four values at once and save once.</summary>
		private void SetArea(int x, int y, int w, int h)
		{
			if (_current == null) return;
			bool was = _loading;
			_loading = true;
			try
			{
				_xNum.Value = Clamp(x, _xNum);
				_yNum.Value = Clamp(y, _yNum);
				_wNum.Value = Clamp(w, _wNum);
				_hNum.Value = Clamp(h, _hNum);
			}
			finally { _loading = was; }
			ReadEditor();
			UpdateAreaLabel();
			SyncPresetBox();
			_list.Invalidate();
			_saveTimer.Stop();
			_saveTimer.Start();
		}

		private void AlignArea(string how)
		{
			Rectangle b = SelectedBounds();
			int x = (int)_xNum.Value, y = (int)_yNum.Value, w = (int)_wNum.Value, h = (int)_hNum.Value;
			switch (how)
			{
				case "left": x = b.X; break;
				case "hcenter": x = b.X + (b.Width - w) / 2; break;
				case "right": x = b.Right - w; break;
				case "top": y = b.Y; break;
				case "vcenter": y = b.Y + (b.Height - h) / 2; break;
				case "bottom": y = b.Bottom - h; break;
			}
			SetArea(x, y, w, h);
		}

		private void PresetChanged(object sender, EventArgs e)
		{
			if (_loading) return;
			if (_presetBox.SelectedItem as string == RestoreDefaultsText)
			{
				int added = PresetService.RestoreDefaults(_settings);
				AppState.SaveSettings();
				RebuildPresetItems();
				SetStatus(added > 0 ? "Restored " + added + " default presets." : "All default presets are already there.", false);
				return;
			}
			ResolutionPreset pr = _presetBox.SelectedItem as ResolutionPreset;
			if (_btnDeletePreset != null) _btnDeletePreset.Enabled = pr != null && _current != null;
			if (pr == null) return;
			Rectangle b = SelectedBounds();
			SetArea(b.X + (b.Width - pr.Width) / 2, b.Y + (b.Height - pr.Height) / 2, pr.Width, pr.Height);
		}

		private void SelectAreaOnScreen()
		{
			using (AreaSelectForm f = new AreaSelectForm())
			{
				if (f.ShowDialog(this) == DialogResult.OK)
					SetArea(f.Selected.X, f.Selected.Y, f.Selected.Width, f.Selected.Height);
			}
		}

		private void SaveNow()
		{
			_saveTimer.Stop();
			Storage.Save("profiles.xml", _library);
		}

		// ------------------------------------------------------------ actions

		private void ApplyCurrent()
		{
			if (_current == null) return;
			ReadEditor();
			SaveNow();
			string err = _manager.ApplyNow(_current);
			SetStatus(err ?? "Applied.", err != null);
		}

		private void RestoreCurrent()
		{
			if (_current == null) return;
			int n = _manager.Restore(_current);
			SetStatus(n > 0 ? "Restored." : "Nothing to restore - the profile isn't applied.", false);
		}

		private void RestoreDesktopNow()
		{
			_manager.RestoreAll();
			SetStatus("Desktop restored: windows put back, taskbar shown.", false);
		}

		private void SetThemeMode(string mode)
		{
			_settings.ThemeMode = mode;
			AppState.SaveSettings();
			Theme.SetMode(mode);   // raises Theme.Changed -> every open window restyles itself
		}

		private void AddThemeItem(ToolStripMenuItem parent, string text, string mode)
		{
			ToolStripMenuItem it = new ToolStripMenuItem(text);
			it.Checked = Theme.Mode == mode;
			it.Click += delegate { SetThemeMode(mode); };
			parent.DropDownItems.Add(it);
		}

		private void ShowSettingsMenu()
		{
			ContextMenuStrip m = new ContextMenuStrip();
			ToolStripMenuItem theme = new ToolStripMenuItem("Theme");
			AddThemeItem(theme, "Dark", "dark");
			AddThemeItem(theme, "Light", "light");
			AddThemeItem(theme, "System", "system");
			m.Items.Add(theme);
			m.Items.Add("Keyboard shortcuts...", null, delegate { ShowShortcuts(); });
			m.Items.Add(new ToolStripSeparator());
			AddSwitchItem(m, "Auto-activate profiles", _autoAllChk);
			AddSwitchItem(m, "Close to tray", _trayChk);
			AddSwitchItem(m, "Start with Windows", _startupChk);
			m.Items.Add(new ToolStripSeparator());
			m.Items.Add("Advanced editor", null, delegate { new MainForm().Show(); });
			Theme.StyleMenu(m);
			m.ShowCheckMargin = true;
			ToolStripDropDownMenu sub = theme.DropDown as ToolStripDropDownMenu;
			if (sub != null) sub.ShowCheckMargin = true;
			m.Show(_settingsBtn.PointToScreen(new Point(_settingsBtn.Width, 0)), ToolStripDropDownDirection.AboveLeft);
		}

		private void AddSwitchItem(ContextMenuStrip m, string text, ToggleSwitch sw)
		{
			ToolStripMenuItem it = new ToolStripMenuItem(text);
			it.Checked = sw.Checked;
			it.Click += delegate { sw.Checked = !sw.Checked; };
			m.Items.Add(it);
		}

		private void UpdatePanicText()
		{
			_panicBtn.Text = _settings.HotkeyRestoreDesktop.IsSet
				? "Restore Desktop   " + HotkeyText.Format(_settings.HotkeyRestoreDesktop)
				: "Restore Desktop";
		}

		private void ShowShortcuts()
		{
			using (ShortcutsForm f = new ShortcutsForm(_settings))
			{
				if (f.ShowDialog(this) != DialogResult.OK) return;
				_settings.HotkeyApplyRestore = f.ApplyRestore.Clone();
				_settings.HotkeyTaskbar = f.Taskbar.Clone();
				_settings.HotkeyRestoreDesktop = f.RestoreDesktop.Clone();
				AppState.SaveSettings();
			}
			UpdatePanicText();
			List<string> failed = RegisterHotkeys();
			if (failed.Count == 0) SetStatus("Shortcuts saved.", false);
			else SetStatus("Another program already uses: " + string.Join(", ", failed.ToArray()) + ". Pick different keys.", true);
		}

		// ---- global hotkeys ----

		private List<string> RegisterHotkeys()
		{
			UnregisterHotkeys();
			List<string> failed = new List<string>();
			TryRegister(HkApplyRestore, _settings.HotkeyApplyRestore, "Apply / Restore", failed);
			TryRegister(HkTaskbar, _settings.HotkeyTaskbar, "Hide / show taskbar", failed);
			TryRegister(HkRestoreDesktop, _settings.HotkeyRestoreDesktop, "Restore Desktop", failed);
			return failed;
		}

		private void TryRegister(int id, HotkeyBinding b, string name, List<string> failed)
		{
			if (b == null || !b.IsSet) return;
			uint mods = (uint)b.Modifiers | 0x4000;   // MOD_NOREPEAT: holding the keys doesn't toggle repeatedly
			if (!Native.RegisterHotKey(Handle, id, mods, (uint)b.Key))
				failed.Add(name + " (" + HotkeyText.Format(b) + ")");
		}

		private void UnregisterHotkeys()
		{
			Native.UnregisterHotKey(Handle, HkApplyRestore);
			Native.UnregisterHotKey(Handle, HkTaskbar);
			Native.UnregisterHotKey(Handle, HkRestoreDesktop);
		}

		private GameProfile ProfileOfForegroundWindow()
		{
			IntPtr h = Native.GetForegroundWindow();
			if (h == IntPtr.Zero) return null;
			uint pid;
			Native.GetWindowThreadProcessId(h, out pid);
			Process p = null;
			try
			{
				p = Process.GetProcessById((int)pid);
				if (p.Id == Process.GetCurrentProcess().Id) return null;   // our own window
				string name = p.ProcessName;
				foreach (GameProfile gp in _library.Profiles)
					if (string.Equals(Path.GetFileNameWithoutExtension(gp.ExeName), name, StringComparison.OrdinalIgnoreCase))
						return gp;
			}
			catch { }
			finally { if (p != null) p.Dispose(); }
			return null;
		}

		private void HotkeyApplyRestoreAction()
		{
			GameProfile gp = ProfileOfForegroundWindow() ?? _current;
			if (gp == null) { OnNotice("No game profile for the active window."); return; }
			if (_manager.GetState(gp) == ProfileState.Active)
			{
				_manager.Restore(gp);
				OnNotice("Restored " + gp.Name);
			}
			else
			{
				string err = _manager.ApplyNow(gp);
				if (err != null) OnNotice(err);   // success is announced by the manager itself
			}
		}

		protected override void OnThemeApplied()
		{
			if (_addMenu != null) Theme.StyleMenu(_addMenu);
			if (_alignMenu != null) Theme.StyleMenu(_alignMenu);
			if (_tray.ContextMenuStrip != null)
			{
				Theme.StyleMenu(_tray.ContextMenuStrip);
				_tray.ContextMenuStrip.ShowCheckMargin = true;
			}
			UiNative.ThemeScrollbars(_list);
			_list.Invalidate();
		}

		private void SetStatus(string text, bool error)
		{
			_status.Text = text;
			_status.ForeColor = error ? Theme.Danger : Theme.Muted;
		}

		private void OnNotice(string msg)
		{
			SetStatus(msg, false);
			if (!Visible)
			{
				_tray.BalloonTipTitle = AppInfo.Name;
				_tray.BalloonTipText = msg;
				_tray.ShowBalloonTip(2500);
			}
		}

		// ------------------------------------------------------------ window / tray plumbing

		protected override void SetVisibleCore(bool value)
		{
			if (!_allowVisible) value = false;   // start hidden in the tray
			base.SetVisibleCore(value);
		}

		private void ShowMe()
		{
			_allowVisible = true;
			Show();
			DoLayout();
			if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
			Activate();
			BringToFront();
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			List<string> failed = RegisterHotkeys();
			if (failed.Count > 0)
				SetStatus("Another program already uses: " + string.Join(", ", failed.ToArray()) + ". Change it in Settings > Keyboard shortcuts.", true);
		}

		protected override void OnHandleDestroyed(EventArgs e)
		{
			UnregisterHotkeys();
			base.OnHandleDestroyed(e);
		}

		protected override void WndProc(ref Message m)
		{
			if (m.Msg == Native.WM_HOTKEY && _manager != null)
			{
				switch (m.WParam.ToInt32())
				{
					case HkApplyRestore: HotkeyApplyRestoreAction(); return;
					case HkTaskbar: OnNotice(_manager.ToggleTaskbar()); return;
					case HkRestoreDesktop: RestoreDesktopNow(); return;
				}
			}
			if (m.Msg == (int)Native.ShowMeMessage) { ShowMe(); return; }
			if (m.Msg == (int)Native.TaskbarCreatedMessage && _manager != null)
				_manager.Tick();   // Explorer restarted: re-hide the new taskbar right away
			base.WndProc(ref m);
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			SaveWindowBounds();
			if (!_reallyExit && _settings.MinimizeToTray && e.CloseReason == CloseReason.UserClosing)
			{
				e.Cancel = true;
				Hide();
				AppState.SaveSettings();
				if (!_settings.TrayHintShown)
				{
					_settings.TrayHintShown = true;
					AppState.SaveSettings();
					_tray.ShowBalloonTip(3000, AppInfo.Name, "Still running in the tray and watching for your games.", ToolTipIcon.Info);
				}
				return;
			}
			ReadEditor();
			SaveNow();
			AppState.SaveSettings();
			_manager.Shutdown();
			_tray.Visible = false;
			_tray.Dispose();
			base.OnFormClosing(e);
		}
	}
}
