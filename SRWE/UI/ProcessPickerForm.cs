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
	internal class ProcItem
	{
		public int Pid;
		public string Name = "";
		public string Title = "";
		public string Path = "";
		public bool HasWindow, Ignored, HasProfile;
	}

	/// <summary>
	/// Themed process list. In "select" mode it just returns a process (Advanced editor);
	/// otherwise it adds games: double-click adds, right-click offers "add and apply a saved
	/// resolution preset" and ignore / un-ignore.
	/// </summary>
	internal class ProcessPickerForm : ThemedForm
	{
		private readonly bool _selectMode;
		private readonly List<ProcItem> _all = new List<ProcItem>();
		private readonly Dictionary<string, Image> _icons = new Dictionary<string, Image>();
		private readonly Font _fontName = Theme.Face(10f, FontStyle.Bold);
		private readonly Font _fontSmall = Theme.Face(8.5f, FontStyle.Regular);
		private readonly Font _fontPill = Theme.Face(8f, FontStyle.Bold);

		private TextField _search;
		private ToggleSwitch _onlyWindows, _showIgnored;
		private ListBox _list;
		private ModernButton _btnPrimary, _btnClose, _btnRefresh, _btnUnignore;
		private Label _status;
		private int _hover = -1;

		public Process SelectedProcess { get; private set; }
		public event Action<ProcItem, ResolutionPreset> AddRequested;

		public ProcessPickerForm(bool selectMode)
		{
			_selectMode = selectMode;
			Text = selectMode ? "Select process" : "Add a game";
			StartPosition = FormStartPosition.CenterParent;
			ShowInTaskbar = false;
			KeyPreview = true;
			ClientSize = new Size(780, 640);
			MinimumSize = Size;

			Label title = new Label();
			title.Text = selectMode ? "Select a process" : "Add a game";
			title.AutoSize = true;
			title.Location = new Point(20, 14);
			title.Font = Theme.Face(16f, FontStyle.Bold);
			Controls.Add(Theme.Role(title, "f:text"));

			Label sub = new Label();
			sub.Text = selectMode ? "Choose the program to attach to."
				: "Double-click to add. Right-click for presets and to ignore a process.";
			sub.AutoSize = true;
			sub.Location = new Point(22, 50);
			sub.Font = Theme.Face(9f, FontStyle.Regular);
			Controls.Add(Theme.Role(sub, "f:muted"));

			_search = new TextField();
			_search.Location = new Point(20, 84);
			_search.Size = new Size(330, 36);
			_search.Placeholder = "Search processes";
			_search.TextChanged += delegate { RefreshView(); };
			Controls.Add(_search);

			_onlyWindows = new ToggleSwitch();
			_onlyWindows.Text = "Only apps with windows";
			_onlyWindows.Location = new Point(366, 89);
			_onlyWindows.Size = new Size(200, 26);
			_onlyWindows.Checked = true;
			_onlyWindows.CheckedChanged += delegate { RefreshView(); };
			Controls.Add(_onlyWindows);

			_showIgnored = new ToggleSwitch();
			_showIgnored.Text = "Show ignored";
			_showIgnored.Location = new Point(580, 89);
			_showIgnored.Size = new Size(170, 26);
			_showIgnored.CheckedChanged += delegate { RefreshView(); };
			Controls.Add(_showIgnored);

			_list = new ListBox();
			_list.Location = new Point(20, 134);
			_list.Size = new Size(740, 400);
			_list.BorderStyle = BorderStyle.None;
			_list.DrawMode = DrawMode.OwnerDrawFixed;
			_list.ItemHeight = 58;
			_list.IntegralHeight = false;
			Controls.Add(Theme.Role(_list, "b:bg"));
			_list.DrawItem += List_DrawItem;
			_list.SelectedIndexChanged += delegate { UpdateButtons(); };
			_list.HandleCreated += delegate { UiNative.ThemeScrollbars(_list); };
			_list.MouseMove += delegate(object s, MouseEventArgs me)
			{
				int i = _list.IndexFromPoint(me.Location);
				if (i != _hover) { _hover = i; _list.Invalidate(); }
			};
			_list.MouseLeave += delegate { if (_hover != -1) { _hover = -1; _list.Invalidate(); } };
			_list.MouseDown += List_MouseDown;
			_list.DoubleClick += delegate { PrimaryAction(); };

			_btnRefresh = new ModernButton();
			_btnRefresh.Text = "Refresh";
			_btnRefresh.Size = new Size(90, 38);
			_btnRefresh.Click += delegate { Reload(); };
			Controls.Add(_btnRefresh);

			_btnUnignore = new ModernButton();
			_btnUnignore.Size = new Size(160, 38);
			_btnUnignore.Click += delegate
			{
				AppState.Settings.IgnoredProcesses.Clear();
				AppState.SaveSettings();
				SetStatus("Un-ignored all processes.");
				RefreshView();
			};
			Controls.Add(_btnUnignore);

			_status = new Label();
			_status.AutoSize = false;
			_status.AutoEllipsis = true;
			_status.TextAlign = ContentAlignment.MiddleLeft;
			_status.Font = Theme.Face(9f, FontStyle.Regular);
			Controls.Add(Theme.Role(_status, "f:muted"));

			_btnPrimary = new ModernButton();
			_btnPrimary.Text = selectMode ? "Select" : "Add game";
			_btnPrimary.Kind = ButtonKind.Primary;
			_btnPrimary.Size = new Size(130, 38);
			_btnPrimary.Click += delegate { PrimaryAction(); };
			Controls.Add(_btnPrimary);

			_btnClose = new ModernButton();
			_btnClose.Text = selectMode ? "Cancel" : "Close";
			_btnClose.Size = new Size(100, 38);
			_btnClose.Click += delegate { DialogResult = DialogResult.Cancel; };
			Controls.Add(_btnClose);

			Resize += delegate { LayoutControls(); };
			LayoutControls();
			UpdateButtons();
		}

		private void LayoutControls()
		{
			int w = ClientSize.Width, h = ClientSize.Height;
			_list.Size = new Size(w - 40, Math.Max(100, h - 134 - 76));
			int y = h - 58;
			_btnRefresh.Location = new Point(20, y);
			_btnUnignore.Location = new Point(118, y);
			_btnClose.Location = new Point(w - 20 - 100, y);
			_btnPrimary.Location = new Point(_btnClose.Left - 10 - 130, y);
			int sx = _btnUnignore.Visible ? _btnUnignore.Right + 12 : _btnRefresh.Right + 12;
			_status.Location = new Point(sx, y);
			_status.Size = new Size(Math.Max(60, _btnPrimary.Left - sx - 10), 38);
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			if (Owner != null) Icon = Owner.Icon;
			Reload();
		}

		protected override void OnThemeApplied()
		{
			UiNative.ThemeScrollbars(_list);
			_list.Invalidate();
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; e.Handled = true; }
			else if (e.KeyCode == Keys.Enter && _list.Focused) { PrimaryAction(); e.Handled = true; e.SuppressKeyPress = true; }
			base.OnKeyDown(e);
		}

		// ------------------------------------------------------------ data

		private void SetStatus(string text) { _status.Text = text; }

		private static bool HasProfileFor(string name)
		{
			foreach (GameProfile gp in AppState.Library.Profiles)
				if (string.Equals(System.IO.Path.GetFileNameWithoutExtension(gp.ExeName), name, StringComparison.OrdinalIgnoreCase))
					return true;
			return false;
		}

		private void Reload()
		{
			Cursor = Cursors.WaitCursor;
			try
			{
				_all.Clear();
				Dictionary<int, IntPtr> windows = WindowFinder.MainWindowsByPid();
				Dictionary<string, ProcItem> byName = new Dictionary<string, ProcItem>(StringComparer.OrdinalIgnoreCase);
				int self = Process.GetCurrentProcess().Id;

				foreach (Process p in Process.GetProcesses())
				{
					try
					{
						if (p.Id == self || p.Id == 0 || p.Id == 4) continue;
						string name = p.ProcessName;
						bool hasWindow = windows.ContainsKey(p.Id);
						ProcItem existing;
						if (byName.TryGetValue(name, out existing))
						{
							if (existing.HasWindow || !hasWindow) continue;   // prefer the instance that has a window
						}
						ProcItem it = new ProcItem();
						it.Pid = p.Id;
						it.Name = name;
						it.HasWindow = hasWindow;
						try { it.Title = hasWindow ? p.MainWindowTitle : ""; } catch { }
						try { it.Path = p.MainModule.FileName; } catch { }
						byName[name] = it;
					}
					catch { }
					finally { p.Dispose(); }
				}

				_all.AddRange(byName.Values);
				_all.Sort(delegate(ProcItem a, ProcItem b)
				{
					if (a.HasWindow != b.HasWindow) return a.HasWindow ? -1 : 1;
					return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
				});
			}
			finally { Cursor = Cursors.Default; }
			RefreshView();
		}

		private void RefreshView()
		{
			string q = _search.Text.Trim().ToLowerInvariant();
			int keepPid = _list.SelectedItem is ProcItem ? ((ProcItem)_list.SelectedItem).Pid : -1;

			_list.BeginUpdate();
			_list.Items.Clear();
			foreach (ProcItem p in _all)
			{
				p.Ignored = AppState.IsIgnored(p.Name);
				p.HasProfile = HasProfileFor(p.Name);
				if (p.Ignored && !_showIgnored.Checked) continue;
				if (_onlyWindows.Checked && !p.HasWindow && !p.HasProfile) continue;
				if (q.Length > 0 && p.Name.ToLowerInvariant().IndexOf(q) < 0 && p.Title.ToLowerInvariant().IndexOf(q) < 0) continue;
				_list.Items.Add(p);
				if (p.Pid == keepPid) _list.SelectedItem = p;
			}
			_list.EndUpdate();

			int ignored = AppState.Settings.IgnoredProcesses.Count;
			_btnUnignore.Visible = ignored > 0;
			_btnUnignore.Text = "Un-ignore all (" + ignored + ")";
			LayoutControls();
			UpdateButtons();
		}

		private ProcItem Current { get { return _list.SelectedItem as ProcItem; } }

		private void UpdateButtons()
		{
			_btnPrimary.Enabled = Current != null;
		}

		private Image IconFor(ProcItem p)
		{
			if (string.IsNullOrEmpty(p.Path)) return null;
			Image img;
			if (_icons.TryGetValue(p.Path, out img)) return img;
			img = null;
			try
			{
				if (File.Exists(p.Path))
					using (Icon ic = Icon.ExtractAssociatedIcon(p.Path)) img = ic.ToBitmap();
			}
			catch { }
			_icons[p.Path] = img;
			return img;
		}

		// ------------------------------------------------------------ actions

		private void PrimaryAction()
		{
			ProcItem p = Current;
			if (p == null) return;
			if (_selectMode) Accept(p);
			else RaiseAdd(p, null);
		}

		private void Accept(ProcItem p)
		{
			try
			{
				SelectedProcess = Process.GetProcessById(p.Pid);
				DialogResult = DialogResult.OK;
			}
			catch
			{
				SetStatus(p.Name + " is no longer running.");
				Reload();
			}
		}

		private void RaiseAdd(ProcItem p, ResolutionPreset preset)
		{
			if (AddRequested != null) AddRequested(p, preset);
			SetStatus(preset == null ? "Added " + p.Name + "." : "Added " + p.Name + " with " + preset + ".");
			RefreshView();
		}

		private void SetIgnored(ProcItem p, bool ignored)
		{
			AppState.SetIgnored(p.Name, ignored);
			SetStatus((ignored ? "Ignoring " : "Un-ignored ") + p.Name + ".");
			RefreshView();
		}

		private void List_MouseDown(object sender, MouseEventArgs e)
		{
			if (e.Button != MouseButtons.Right) return;
			int i = _list.IndexFromPoint(e.Location);
			if (i < 0) return;
			_list.SelectedIndex = i;
			ShowMenu((ProcItem)_list.Items[i], e.Location);
		}

		private void ShowMenu(ProcItem p, Point where)
		{
			ContextMenuStrip m = new ContextMenuStrip();

			if (_selectMode)
			{
				m.Items.Add("Select " + p.Name, null, delegate { Accept(p); });
			}
			else
			{
				if (!p.HasProfile) m.Items.Add("Add game", null, delegate { RaiseAdd(p, null); });

				ToolStripMenuItem sub = new ToolStripMenuItem(p.HasProfile ? "Apply resolution preset" : "Add game and apply preset");
				foreach (ResolutionPreset rp in AppState.Settings.Presets)
				{
					ResolutionPreset cap = rp;
					sub.DropDownItems.Add(cap.ToString(), null, delegate { RaiseAdd(p, cap); });
				}
				if (sub.DropDownItems.Count == 0)
				{
					ToolStripItem none = sub.DropDownItems.Add("No saved presets");
					none.Enabled = false;
				}
				m.Items.Add(sub);
			}

			m.Items.Add(new ToolStripSeparator());
			if (p.Ignored) m.Items.Add("Un-ignore " + p.Name, null, delegate { SetIgnored(p, false); });
			else m.Items.Add("Ignore " + p.Name, null, delegate { SetIgnored(p, true); });

			Theme.StyleMenu(m);
			m.Show(_list, where);
		}

		// ------------------------------------------------------------ drawing

		private void List_DrawItem(object sender, DrawItemEventArgs e)
		{
			Graphics g = e.Graphics;
			using (SolidBrush bg = new SolidBrush(Theme.Bg))
				g.FillRectangle(bg, e.Bounds);
			if (e.Index < 0) return;

			ProcItem p = (ProcItem)_list.Items[e.Index];
			bool sel = (e.State & DrawItemState.Selected) != 0;
			Rectangle card = new Rectangle(e.Bounds.X, e.Bounds.Y + 3, e.Bounds.Width - 2, e.Bounds.Height - 6);
			g.SmoothingMode = SmoothingMode.AntiAlias;
			if (sel) Theme.FillRound(g, card, 10, Theme.Selected);
			else if (e.Index == _hover) Theme.FillRound(g, card, 10, Theme.Card);

			Color nameColor = p.Ignored ? Theme.Dim : Theme.Text;
			Color subColor = p.Ignored ? Theme.Dim : Theme.Muted;

			Rectangle av = new Rectangle(card.X + 12, card.Y + (card.Height - 32) / 2, 32, 32);
			Image icon = IconFor(p);
			if (icon != null)
			{
				g.InterpolationMode = InterpolationMode.HighQualityBicubic;
				g.DrawImage(icon, av);
			}
			else
			{
				Theme.FillRound(g, av, 8, Theme.Field);
				string letter = p.Name.Length > 0 ? p.Name.Substring(0, 1).ToUpperInvariant() : "?";
				TextRenderer.DrawText(g, letter, _fontName, av, subColor,
					TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
			}

			// status pills on the right
			int right = card.Right - 12;
			if (p.Ignored) right = DrawPill(g, "Ignored", Theme.Dim, right, card);
			if (p.HasProfile) right = DrawPill(g, "Has profile", Theme.Good, right, card);

			int tx = av.Right + 12;
			int tw = Math.Max(20, right - tx - 8);
			TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
				TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
			TextRenderer.DrawText(g, p.Name, _fontName, new Rectangle(tx, card.Y + 8, tw, 22), nameColor, flags);
			string subtitle = (p.HasWindow && p.Title.Length > 0 ? p.Title : p.HasWindow ? "Window" : "Background process") +
				"  \u00B7  PID " + p.Pid;
			TextRenderer.DrawText(g, subtitle, _fontSmall, new Rectangle(tx, card.Y + 30, tw, 18), subColor, flags);
		}

		/// <summary>Draws a pill ending at 'right'; returns the new right edge for the next one.</summary>
		private int DrawPill(Graphics g, string text, Color c, int right, Rectangle card)
		{
			int pw = TextRenderer.MeasureText(text, _fontPill).Width + 22;
			Rectangle pr = new Rectangle(right - pw, card.Y + (card.Height - 20) / 2, pw, 20);
			Theme.FillRound(g, pr, 10, Color.FromArgb(44, c));
			using (SolidBrush b = new SolidBrush(c))
				g.FillEllipse(b, pr.X + 8, pr.Y + 7, 6, 6);
			TextRenderer.DrawText(g, text, _fontPill, new Rectangle(pr.X + 16, pr.Y, pr.Width - 16, pr.Height), c,
				TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
			return pr.X - 8;
		}
	}
}
