using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace SRWE.UI
{
	/// <summary>Rounded panel that groups related settings.</summary>
	internal class Card : Panel
	{
		public Card()
		{
			DoubleBuffered = true;
			Tag = "b:card";
			BackColor = Theme.Card;
			SetStyle(ControlStyles.ResizeRedraw, true);
		}

		protected override void OnPaintBackground(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
			g.SmoothingMode = SmoothingMode.AntiAlias;
			Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
			Theme.FillRound(g, r, 12, BackColor);
			Theme.StrokeRound(g, r, 12, Theme.CardBorder);
		}
	}

	internal enum ButtonKind { Primary, Secondary, Danger }

	internal class ModernButton : Control
	{
		public ButtonKind Kind = ButtonKind.Secondary;
		private bool _hover, _down;

		public ModernButton()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
				ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
			Cursor = Cursors.Hand;
			Font = Theme.Face(9.5f, FontStyle.Regular);
			Size = new Size(120, 36);
		}

		protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
		protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
		protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
		protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
		protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
		protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			g.Clear(BackColor);
			g.SmoothingMode = SmoothingMode.AntiAlias;

			Color fill, fore = Color.White;
			switch (Kind)
			{
				case ButtonKind.Primary:
					fill = _down ? Theme.AccentDown : _hover ? Theme.AccentHover : Theme.Accent; break;
				case ButtonKind.Danger:
					fill = _down ? Theme.Danger : _hover ? Theme.DangerHover : Theme.Danger; break;
				default:
					fill = _down ? Theme.Field : _hover ? Theme.FieldHover : Theme.Field;
					fore = Theme.Text; break;
			}
			if (!Enabled) { fill = Theme.Field; fore = Theme.Dim; }

			Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
			Theme.FillRound(g, r, 8, fill);
			if (Kind == ButtonKind.Secondary) Theme.StrokeRound(g, r, 8, Theme.FieldBorder);
			TextRenderer.DrawText(g, Text, Font, ClientRectangle, fore,
				TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
				TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
		}
	}

	/// <summary>Pill-shaped on/off switch with a label; API mirrors CheckBox (Checked, CheckedChanged).</summary>
	internal class ToggleSwitch : Control
	{
		private bool _checked;
		public event EventHandler CheckedChanged;

		public ToggleSwitch()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
				ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
			Cursor = Cursors.Hand;
			Font = Theme.Face(9.5f, FontStyle.Regular);
			Size = new Size(240, 26);
		}

		public bool Checked
		{
			get { return _checked; }
			set
			{
				if (_checked == value) return;
				_checked = value;
				Invalidate();
				if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
			}
		}

		protected override void OnClick(EventArgs e)
		{
			if (Enabled) Checked = !Checked;   // flip first so Click handlers see the new state
			base.OnClick(e);
		}

		protected override void OnKeyUp(KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Space && Enabled) Checked = !Checked;
			base.OnKeyUp(e);
		}

		protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
		protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			g.Clear(BackColor);
			g.SmoothingMode = SmoothingMode.AntiAlias;

			const int w = 38, h = 20, k = 14;
			Rectangle track = new Rectangle(0, (Height - h) / 2, w - 1, h - 1);
			Color trackColor = _checked ? Theme.Accent : Theme.Toggle;
			if (!Enabled) trackColor = Theme.Field;
			Theme.FillRound(g, track, 10, trackColor);
			int kx = _checked ? w - k - 4 : 3;
			using (SolidBrush b = new SolidBrush(Enabled ? Color.White : Theme.Dim))
				g.FillEllipse(b, kx, track.Y + 3, k, k);

			TextRenderer.DrawText(g, Text, Font, new Rectangle(w + 10, 0, Width - w - 10, Height),
				Enabled ? Theme.Text : Theme.Dim,
				TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
				TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
		}
	}

	/// <summary>Rounded single-line text input with optional placeholder.</summary>
	internal class TextField : Control
	{
		private readonly TextBox _tb = new TextBox();
		private bool _focus, _hover;
		private string _placeholder;

		public TextField()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
				ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
			Size = new Size(200, 34);
			Font = Theme.Face(9.5f, FontStyle.Regular);

			_tb.BorderStyle = BorderStyle.None;
			_tb.BackColor = Theme.Field;
			_tb.ForeColor = Theme.Text;
			_tb.Font = Font;
			_tb.GotFocus += delegate { _focus = true; Invalidate(); };
			_tb.LostFocus += delegate { _focus = false; Invalidate(); };
			_tb.TextChanged += delegate { OnTextChanged(EventArgs.Empty); };
			_tb.HandleCreated += delegate { ApplyPlaceholder(); };
			Controls.Add(_tb);
			PositionBox();
		}

		public override string Text
		{
			get { return _tb.Text; }
			set { _tb.Text = value; }
		}

		public string Placeholder
		{
			get { return _placeholder; }
			set { _placeholder = value; ApplyPlaceholder(); }
		}

		private void ApplyPlaceholder()
		{
			if (_tb.IsHandleCreated && _placeholder != null)
				UiNative.SendMessageString(_tb.Handle, 0x1501 /* EM_SETCUEBANNER */, new IntPtr(1), _placeholder);
		}

		private void PositionBox()
		{
			_tb.Width = Math.Max(10, Width - 24);
			_tb.Location = new Point(12, Math.Max(0, (Height - _tb.Height) / 2));
		}

		protected override void OnResize(EventArgs e) { base.OnResize(e); PositionBox(); }
		protected override void OnFontChanged(EventArgs e) { _tb.Font = Font; PositionBox(); base.OnFontChanged(e); }
		protected override void OnEnabledChanged(EventArgs e) { _tb.Enabled = Enabled; Invalidate(); base.OnEnabledChanged(e); }
		protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
		protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
		protected override void OnMouseDown(MouseEventArgs e) { _tb.Focus(); base.OnMouseDown(e); }

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			g.Clear(BackColor);
			g.SmoothingMode = SmoothingMode.AntiAlias;
			Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
			Theme.FillRound(g, r, 8, _hover || _focus ? Theme.FieldHover : Theme.Field);
			Theme.StrokeRound(g, r, 8, _focus ? Theme.Accent : Theme.FieldBorder);
			_tb.BackColor = _hover || _focus ? Theme.FieldHover : Theme.Field;
			_tb.ForeColor = Theme.Text;
		}
	}

	/// <summary>Rounded integer input with up/down arrows; API mirrors NumericUpDown (Value, Minimum, Maximum, ValueChanged).</summary>
	internal class NumberField : Control
	{
		private readonly TextBox _tb = new TextBox();
		private decimal _value, _min, _max = 100;
		private bool _focus, _hover, _updating;
		public decimal Step = 1;
		public event EventHandler ValueChanged;

		public NumberField()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
				ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
			Size = new Size(100, 34);
			Font = Theme.Face(9.5f, FontStyle.Regular);

			_tb.BorderStyle = BorderStyle.None;
			_tb.BackColor = Theme.Field;
			_tb.ForeColor = Theme.Text;
			_tb.Font = Font;
			_tb.GotFocus += delegate { _focus = true; Invalidate(); };
			_tb.LostFocus += delegate { _focus = false; RefreshText(); Invalidate(); };
			_tb.TextChanged += Tb_TextChanged;
			_tb.KeyPress += Tb_KeyPress;
			_tb.KeyDown += Tb_KeyDown;
			_tb.MouseWheel += delegate(object s, MouseEventArgs me) { Nudge(me.Delta > 0 ? 1 : -1); };
			Controls.Add(_tb);
			PositionBox();
			RefreshText();
		}

		public decimal Minimum { get { return _min; } set { _min = value; } }
		public decimal Maximum { get { return _max; } set { _max = value; } }

		public decimal Value
		{
			get { return _value; }
			set
			{
				if (value < _min) value = _min;
				if (value > _max) value = _max;
				bool changed = value != _value;
				_value = value;
				RefreshText();
				if (changed && ValueChanged != null) ValueChanged(this, EventArgs.Empty);
			}
		}

		private void RefreshText()
		{
			_updating = true;
			string t = ((long)_value).ToString(CultureInfo.InvariantCulture);
			if (_tb.Text != t) _tb.Text = t;
			_updating = false;
		}

		private void Tb_TextChanged(object sender, EventArgs e)
		{
			if (_updating) return;
			long v;
			if (long.TryParse(_tb.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out v) &&
				v >= _min && v <= _max && v != (long)_value)
			{
				_value = v;                      // commit while typing, without rewriting the text
				if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
			}
		}

		private void Tb_KeyPress(object sender, KeyPressEventArgs e)
		{
			if (char.IsControl(e.KeyChar) || char.IsDigit(e.KeyChar)) return;
			if (e.KeyChar == '-' && _min < 0) return;
			e.Handled = true;
		}

		private void Tb_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Up) { Nudge(1); e.Handled = true; }
			else if (e.KeyCode == Keys.Down) { Nudge(-1); e.Handled = true; }
		}

		private void Nudge(int dir)
		{
			decimal step = Step * (Control.ModifierKeys == Keys.Shift ? 10 : 1);
			Value = _value + dir * step;
		}

		private void PositionBox()
		{
			_tb.Width = Math.Max(10, Width - 12 - 26);
			_tb.Location = new Point(12, Math.Max(0, (Height - _tb.Height) / 2));
		}

		protected override void OnResize(EventArgs e) { base.OnResize(e); PositionBox(); }
		protected override void OnEnabledChanged(EventArgs e) { _tb.Enabled = Enabled; Invalidate(); base.OnEnabledChanged(e); }
		protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
		protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

		protected override void OnMouseDown(MouseEventArgs e)
		{
			if (e.X > Width - 26) { Nudge(e.Y < Height / 2 ? 1 : -1); _tb.Focus(); }
			else _tb.Focus();
			base.OnMouseDown(e);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			g.Clear(BackColor);
			g.SmoothingMode = SmoothingMode.AntiAlias;
			Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
			Theme.FillRound(g, r, 8, _hover || _focus ? Theme.FieldHover : Theme.Field);
			Theme.StrokeRound(g, r, 8, _focus ? Theme.Accent : Theme.FieldBorder);
			_tb.BackColor = _hover || _focus ? Theme.FieldHover : Theme.Field;
			_tb.ForeColor = Theme.Text;

			int cx = Width - 15, cy = Height / 2;
			using (Pen pen = new Pen(Enabled ? Theme.Muted : Theme.Dim, 1.6f))
			{
				g.DrawLines(pen, new Point[] { new Point(cx - 4, cy - 3), new Point(cx, cy - 7), new Point(cx + 4, cy - 3) });
				g.DrawLines(pen, new Point[] { new Point(cx - 4, cy + 3), new Point(cx, cy + 7), new Point(cx + 4, cy + 3) });
			}
		}
	}

	/// <summary>Rounded dropdown; API mirrors the parts of ComboBox the app uses.</summary>
	internal class DropDownField : Control
	{
		private readonly List<object> _items = new List<object>();
		private int _sel = -1;
		private bool _hover;
		public event EventHandler SelectedIndexChanged;

		public DropDownField()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
				ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
			Cursor = Cursors.Hand;
			Size = new Size(200, 34);
			Font = Theme.Face(9.5f, FontStyle.Regular);
		}

		public List<object> Items { get { return _items; } }

		public int SelectedIndex
		{
			get { return _sel; }
			set
			{
				if (value < -1 || value >= _items.Count) value = -1;
				if (value == _sel) return;
				_sel = value;
				Invalidate();
				if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
			}
		}

		public object SelectedItem
		{
			get { return _sel >= 0 && _sel < _items.Count ? _items[_sel] : null; }
		}

		protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
		protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
		protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

		protected override void OnClick(EventArgs e)
		{
			base.OnClick(e);
			if (Enabled && _items.Count > 0) ShowMenu();
		}

		private void ShowMenu()
		{
			// widest entry decides the popup width (never narrower than the field)
			int width = Math.Max(Width, 240);
			foreach (object o in _items)
				width = Math.Max(width, TextRenderer.MeasureText(o.ToString(), Font).Width + 56);
			width = Math.Min(width, 460);

			DropDownPopup pop = new DropDownPopup(_items, _sel, width, Font);
			Point pt = PointToScreen(new Point(0, Height + 4));
			Rectangle wa = Screen.FromControl(this).WorkingArea;
			if (pt.Y + pop.Height > wa.Bottom) pt.Y = PointToScreen(new Point(0, 0)).Y - pop.Height - 4;   // not enough room below: open upwards
			if (pt.X + width > wa.Right) pt.X = wa.Right - width;
			pop.Location = pt;
			pop.FormClosed += delegate
			{
				int chosen = pop.Chosen;
				pop.Dispose();
				if (chosen >= 0) SelectedIndex = chosen;
			};
			pop.Show(FindForm());
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			g.Clear(BackColor);
			g.SmoothingMode = SmoothingMode.AntiAlias;
			Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
			Theme.FillRound(g, r, 8, _hover && Enabled ? Theme.FieldHover : Theme.Field);
			Theme.StrokeRound(g, r, 8, Theme.FieldBorder);

			object cur = SelectedItem;
			TextRenderer.DrawText(g, cur == null ? "" : cur.ToString(), Font,
				new Rectangle(12, 0, Width - 40, Height), Enabled ? Theme.Text : Theme.Dim,
				TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
				TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

			int cx = Width - 16, cy = Height / 2;
			using (Pen pen = new Pen(Enabled ? Theme.Muted : Theme.Dim, 1.6f))
				g.DrawLines(pen, new Point[] { new Point(cx - 4, cy - 2), new Point(cx, cy + 2), new Point(cx + 4, cy - 2) });
		}
	}

	/// <summary>Small rounded status badge ("Active", "Running").</summary>
	internal class Pill : Control
	{
		private Color _color = Theme.Muted;

		public Pill()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
				ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
			Font = Theme.Face(8.5f, FontStyle.Bold);
			Height = 24;
		}

		public Color PillColor { get { return _color; } set { _color = value; Invalidate(); } }

		public void SetState(string text, Color color)
		{
			Text = text;
			_color = color;
			Visible = text.Length > 0;
			Width = TextRenderer.MeasureText(text, Font).Width + 26;
			Invalidate();
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			g.Clear(BackColor);
			g.SmoothingMode = SmoothingMode.AntiAlias;
			Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
			Theme.FillRound(g, r, Height / 2, Color.FromArgb(40, _color));
			using (SolidBrush b = new SolidBrush(_color))
				g.FillEllipse(b, 9, Height / 2 - 3, 6, 6);
			TextRenderer.DrawText(g, Text, Font, new Rectangle(18, 0, Width - 18, Height), _color,
				TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
		}
	}

	/// <summary>The list that opens under a DropDownField: scrollbar, mouse wheel and keyboard all work.</summary>
	internal class DropDownPopup : Form
	{
		private const int RowHeight = 30;
		private readonly ListBox _lb = new ListBox();
		private readonly Font _font;
		private int _hover = -1;

		public int Chosen = -1;

		public DropDownPopup(List<object> items, int selected, int width, Font font)
		{
			_font = font;
			FormBorderStyle = FormBorderStyle.None;
			ShowInTaskbar = false;
			StartPosition = FormStartPosition.Manual;
			KeyPreview = true;
			BackColor = Theme.FieldBorder;          // 1px frame
			Padding = new Padding(1);
			int rows = Math.Min(items.Count, 9);
			ClientSize = new Size(width, rows * RowHeight + 2);

			_lb.Dock = DockStyle.Fill;
			_lb.BorderStyle = BorderStyle.None;
			_lb.BackColor = Theme.Card;
			_lb.DrawMode = DrawMode.OwnerDrawFixed;
			_lb.ItemHeight = RowHeight;
			_lb.IntegralHeight = false;
			foreach (object o in items) _lb.Items.Add(o);
			_lb.DrawItem += DrawRow;
			_lb.HandleCreated += delegate { UiNative.ThemeScrollbars(_lb); };
			_lb.MouseMove += delegate(object s, MouseEventArgs me)
			{
				int i = _lb.IndexFromPoint(me.Location);
				if (i != _hover) { _hover = i; _lb.Invalidate(); }
			};
			_lb.MouseLeave += delegate { _hover = -1; _lb.Invalidate(); };
			_lb.MouseUp += delegate(object s, MouseEventArgs me)
			{
				if (me.Button != MouseButtons.Left) return;
				int i = _lb.IndexFromPoint(me.Location);
				if (i >= 0) { Chosen = i; Close(); }
			};
			Controls.Add(_lb);

			if (selected >= 0 && selected < items.Count)
			{
				_lb.SelectedIndex = selected;
				_lb.TopIndex = Math.Max(0, selected - 3);   // start with the current choice in view
			}
		}

		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);
			_lb.Focus();
		}

		private bool _closing;

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			_closing = true;
			base.OnFormClosing(e);
		}

		protected override void OnDeactivate(EventArgs e)
		{
			base.OnDeactivate(e);
			if (!_closing) Close();
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Escape) { Close(); e.Handled = true; }
			else if (e.KeyCode == Keys.Enter && _lb.SelectedIndex >= 0) { Chosen = _lb.SelectedIndex; Close(); e.Handled = true; }
			base.OnKeyDown(e);
		}

		private void DrawRow(object sender, DrawItemEventArgs e)
		{
			Graphics g = e.Graphics;
			bool sel = e.Index >= 0 && e.Index == _lb.SelectedIndex;
			Color back = e.Index == _hover ? Theme.FieldHover : sel ? Theme.Selected : Theme.Card;
			using (SolidBrush b = new SolidBrush(back))
				g.FillRectangle(b, e.Bounds);
			if (e.Index < 0) return;
			TextRenderer.DrawText(g, _lb.Items[e.Index].ToString(), _font,
				new Rectangle(e.Bounds.X + 12, e.Bounds.Y, e.Bounds.Width - 20, e.Bounds.Height),
				sel ? Theme.Accent : Theme.Text,
				TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
				TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
		}
	}

	internal static class HotkeyText
	{
		public static string KeyName(Keys k)
		{
			if (k >= Keys.D0 && k <= Keys.D9) return ((int)k - (int)Keys.D0).ToString();
			switch (k)
			{
				case Keys.OemMinus: return "-";
				case Keys.Oemplus: return "=";
				case Keys.Oemcomma: return ",";
				case Keys.OemPeriod: return ".";
				case Keys.Return: return "Enter";
				case Keys.Next: return "Page Down";
				case Keys.Prior: return "Page Up";
				default: return k.ToString();
			}
		}

		public static string Format(SRWE.Core.HotkeyBinding b)
		{
			if (b == null || !b.IsSet) return "Not set";
			List<string> parts = new List<string>();
			if ((b.Modifiers & 2) != 0) parts.Add("Ctrl");
			if ((b.Modifiers & 1) != 0) parts.Add("Alt");
			if ((b.Modifiers & 4) != 0) parts.Add("Shift");
			parts.Add(KeyName((Keys)b.Key));
			return string.Join(" + ", parts.ToArray());
		}
	}

	/// <summary>Click it, press the key combination you want. Esc cancels, Backspace/Delete clears.</summary>
	internal class HotkeyBox : Control
	{
		private SRWE.Core.HotkeyBinding _binding = new SRWE.Core.HotkeyBinding();
		private bool _capturing, _hover;
		private string _note = "";
		public event EventHandler BindingChanged;

		public HotkeyBox()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
				ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
			Cursor = Cursors.Hand;
			Size = new Size(170, 36);
			Font = Theme.Face(9.5f, FontStyle.Bold);
		}

		public SRWE.Core.HotkeyBinding Binding
		{
			get { return _binding; }
			set { _binding = value == null ? new SRWE.Core.HotkeyBinding() : value.Clone(); Invalidate(); }
		}

		protected override bool IsInputKey(Keys keyData) { return true; }

		protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
		protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
		protected override void OnMouseDown(MouseEventArgs e) { _capturing = true; _note = ""; Focus(); Invalidate(); base.OnMouseDown(e); }
		protected override void OnLostFocus(EventArgs e) { _capturing = false; _note = ""; Invalidate(); base.OnLostFocus(e); }

		protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
		{
			if (!_capturing) return base.ProcessCmdKey(ref msg, keyData);

			Keys key = keyData & Keys.KeyCode;
			Keys mods = keyData & Keys.Modifiers;

			if (key == Keys.Escape && mods == Keys.None) { _capturing = false; Invalidate(); return true; }
			if ((key == Keys.Back || key == Keys.Delete) && mods == Keys.None)
			{
				_binding = new SRWE.Core.HotkeyBinding();
				Finish();
				return true;
			}
			// only modifiers held so far: keep waiting for the real key
			if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu ||
				key == Keys.LWin || key == Keys.RWin || key == Keys.None) return true;

			int m = 0;
			if ((mods & Keys.Alt) != 0) m |= 1;
			if ((mods & Keys.Control) != 0) m |= 2;
			if ((mods & Keys.Shift) != 0) m |= 4;
			bool fKey = key >= Keys.F1 && key <= Keys.F24;
			if (m == 0 && !fKey)
			{
				_note = "Add Ctrl, Alt or Shift";
				Invalidate();
				return true;
			}
			_binding = new SRWE.Core.HotkeyBinding(m, (int)key);
			Finish();
			return true;
		}

		private void Finish()
		{
			_capturing = false;
			_note = "";
			Invalidate();
			if (BindingChanged != null) BindingChanged(this, EventArgs.Empty);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			g.Clear(BackColor);
			g.SmoothingMode = SmoothingMode.AntiAlias;
			Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
			Theme.FillRound(g, r, 8, _capturing || _hover ? Theme.FieldHover : Theme.Field);
			Theme.StrokeRound(g, r, 8, _capturing ? Theme.Accent : Theme.FieldBorder);

			string text; Color color;
			if (_capturing)
			{
				text = _note.Length > 0 ? _note : "Press keys...";
				color = _note.Length > 0 ? Theme.Danger : Theme.Accent;
			}
			else if (_binding.IsSet) { text = HotkeyText.Format(_binding); color = Theme.Text; }
			else { text = "Not set"; color = Theme.Dim; }

			TextRenderer.DrawText(g, text, Font, ClientRectangle, color,
				TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
				TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
		}
	}
}
