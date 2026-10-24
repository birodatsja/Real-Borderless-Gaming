using System;
using System.Drawing;
using System.Windows.Forms;

namespace SRWE.UI
{
	/// <summary>Full-desktop translucent overlay: drag a rectangle, release to accept, Esc / right-click to cancel.</summary>
	internal class AreaSelectForm : Form
	{
		private Point _start;
		private Point _cur;
		private bool _drag;
		public Rectangle Selected;

		public AreaSelectForm()
		{
			FormBorderStyle = FormBorderStyle.None;
			StartPosition = FormStartPosition.Manual;
			ShowInTaskbar = false;
			TopMost = true;
			BackColor = Color.Black;
			Opacity = 0.45;
			Cursor = Cursors.Cross;
			DoubleBuffered = true;
			Bounds = SystemInformation.VirtualScreen;
		}

		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);
			Bounds = SystemInformation.VirtualScreen;
			Activate();
		}

		private Rectangle ClientSelection()
		{
			return Rectangle.FromLTRB(Math.Min(_start.X, _cur.X), Math.Min(_start.Y, _cur.Y),
				Math.Max(_start.X, _cur.X), Math.Max(_start.Y, _cur.Y));
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Right) { DialogResult = DialogResult.Cancel; return; }
			_start = _cur = e.Location;
			_drag = true;
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (!_drag) return;
			_cur = e.Location;
			Invalidate();
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			if (!_drag) return;
			_drag = false;
			Rectangle r = ClientSelection();
			if (r.Width < 100 || r.Height < 100) { Invalidate(); return; }   // too small: let the user try again
			Selected = new Rectangle(r.X + Left, r.Y + Top, r.Width, r.Height);
			DialogResult = DialogResult.OK;
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel;
			base.OnKeyDown(e);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			using (Font f = Theme.Face(11f, FontStyle.Bold))
			{
				if (!_drag)
				{
					TextRenderer.DrawText(e.Graphics, "Drag to select an area  -  Esc to cancel", f, new Point(24, 24), Color.White);
					return;
				}
				Rectangle r = ClientSelection();
				using (SolidBrush fill = new SolidBrush(Color.FromArgb(99, 102, 241)))
					e.Graphics.FillRectangle(fill, r);
				using (Pen pen = new Pen(Color.White, 2f))
					e.Graphics.DrawRectangle(pen, r);
				string label = r.Width + " \u00D7 " + r.Height + " at X " + (r.X + Left) + ", Y " + (r.Y + Top);
				TextRenderer.DrawText(e.Graphics, label, f, new Point(r.X + 10, r.Y + 8), Color.White);
			}
		}
	}
}
