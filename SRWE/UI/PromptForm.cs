using System;
using System.Drawing;
using System.Windows.Forms;

namespace SRWE.UI
{
	/// <summary>Small themed "enter a name" dialog.</summary>
	internal class PromptForm : ThemedForm
	{
		private readonly TextField _box;

		public string Value { get { return _box.Text.Trim(); } }

		public PromptForm(string title, string label, string initial)
		{
			Text = title;
			FormBorderStyle = FormBorderStyle.FixedDialog;
			StartPosition = FormStartPosition.CenterParent;
			MaximizeBox = false;
			MinimizeBox = false;
			ShowInTaskbar = false;
			KeyPreview = true;
			ClientSize = new Size(400, 168);

			Label head = new Label();
			head.Text = title;
			head.AutoSize = true;
			head.Location = new Point(20, 16);
			head.Font = Theme.Face(13f, FontStyle.Bold);
			Controls.Add(Theme.Role(head, "f:text"));

			Label l = new Label();
			l.Text = label;
			l.AutoSize = true;
			l.Location = new Point(20, 52);
			l.Font = Theme.Face(8.5f, FontStyle.Regular);
			Controls.Add(Theme.Role(l, "f:muted"));

			_box = new TextField();
			_box.Location = new Point(20, 72);
			_box.Size = new Size(360, 36);
			_box.Text = initial;
			Controls.Add(_box);

			ModernButton ok = new ModernButton();
			ok.Text = "Save";
			ok.Kind = ButtonKind.Primary;
			ok.Location = new Point(20, 120);
			ok.Size = new Size(120, 36);
			ok.Click += delegate { Accept(); };
			Controls.Add(ok);

			ModernButton cancel = new ModernButton();
			cancel.Text = "Cancel";
			cancel.Location = new Point(150, 120);
			cancel.Size = new Size(100, 36);
			cancel.Click += delegate { DialogResult = DialogResult.Cancel; };
			Controls.Add(cancel);
		}

		private void Accept()
		{
			if (Value.Length == 0) return;
			DialogResult = DialogResult.OK;
		}

		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);
			_box.Focus();
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Enter) { Accept(); e.Handled = true; e.SuppressKeyPress = true; }
			else if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; e.Handled = true; }
			base.OnKeyDown(e);
		}
	}
}
