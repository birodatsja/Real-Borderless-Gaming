using System;
using System.Drawing;
using System.Windows.Forms;
using SRWE.Core;

namespace SRWE.UI
{
	/// <summary>Small window to bind the global keyboard shortcuts.</summary>
	internal class ShortcutsForm : ThemedForm
	{
		private readonly HotkeyBox _apply = new HotkeyBox();
		private readonly HotkeyBox _taskbar = new HotkeyBox();
		private readonly HotkeyBox _restore = new HotkeyBox();
		private readonly Label _error = new Label();

		public HotkeyBinding ApplyRestore { get { return _apply.Binding; } }
		public HotkeyBinding Taskbar { get { return _taskbar.Binding; } }
		public HotkeyBinding RestoreDesktop { get { return _restore.Binding; } }

		public ShortcutsForm(LauncherSettings s)
		{
			Text = "Keyboard shortcuts";
			FormBorderStyle = FormBorderStyle.FixedDialog;
			StartPosition = FormStartPosition.CenterParent;
			MaximizeBox = false;
			MinimizeBox = false;
			ShowInTaskbar = false;
			ClientSize = new Size(580, 424);

			Label title = new Label();
			title.Text = "Keyboard shortcuts";
			title.AutoSize = true;
			title.Location = new Point(20, 16);
			title.Font = Theme.Face(16f, FontStyle.Bold);
			Controls.Add(Theme.Role(title, "f:text"));

			Label sub = new Label();
			sub.Text = "Click a box, then press the keys you want. Backspace clears it, Esc cancels.";
			sub.AutoSize = true;
			sub.Location = new Point(22, 52);
			sub.Font = Theme.Face(9f, FontStyle.Regular);
			Controls.Add(Theme.Role(sub, "f:muted"));

			AddRow(98, "Apply / Restore", "Toggles the profile of the game you are in (or the selected profile).", _apply);
			AddRow(172, "Hide / show taskbar", "Overrides the profile's taskbar setting until you press it again.", _taskbar);
			AddRow(246, "Restore Desktop", "Emergency: puts every window and the taskbar back.", _restore);

			_apply.Binding = s.HotkeyApplyRestore;
			_taskbar.Binding = s.HotkeyTaskbar;
			_restore.Binding = s.HotkeyRestoreDesktop;

			_error.AutoSize = false;
			_error.Location = new Point(20, 322);
			_error.Size = new Size(540, 22);
			_error.Font = Theme.Face(9f, FontStyle.Regular);
			_error.ForeColor = Theme.Danger;
			Controls.Add(_error);

			ModernButton save = new ModernButton();
			save.Text = "Save";
			save.Kind = ButtonKind.Primary;
			save.Location = new Point(20, 366);
			save.Size = new Size(120, 40);
			save.Click += delegate { TrySave(); };
			Controls.Add(save);

			ModernButton cancel = new ModernButton();
			cancel.Text = "Cancel";
			cancel.Location = new Point(150, 366);
			cancel.Size = new Size(100, 40);
			cancel.Click += delegate { DialogResult = DialogResult.Cancel; };
			Controls.Add(cancel);

			ModernButton reset = new ModernButton();
			reset.Text = "Reset to defaults";
			reset.Location = new Point(580 - 20 - 160, 366);
			reset.Size = new Size(160, 40);
			reset.Click += delegate
			{
				LauncherSettings d = new LauncherSettings();
				_apply.Binding = d.HotkeyApplyRestore;
				_taskbar.Binding = d.HotkeyTaskbar;
				_restore.Binding = d.HotkeyRestoreDesktop;
				_error.Text = "";
			};
			Controls.Add(reset);
		}

		private void AddRow(int y, string name, string desc, HotkeyBox box)
		{
			Label n = new Label();
			n.Text = name;
			n.AutoSize = true;
			n.Location = new Point(20, y);
			n.Font = Theme.Face(10.5f, FontStyle.Bold);
			Controls.Add(Theme.Role(n, "f:text"));

			Label d = new Label();
			d.Text = desc;
			d.AutoSize = false;
			d.Location = new Point(20, y + 24);
			d.Size = new Size(300, 34);
			d.Font = Theme.Face(8.5f, FontStyle.Regular);
			Controls.Add(Theme.Role(d, "f:dim"));

			box.Location = new Point(340, y + 8);
			box.Size = new Size(220, 38);
			box.BindingChanged += delegate { _error.Text = ""; };
			Controls.Add(box);
		}

		private static bool Same(HotkeyBinding a, HotkeyBinding b)
		{
			return a.IsSet && b.IsSet && a.Key == b.Key && a.Modifiers == b.Modifiers;
		}

		private void TrySave()
		{
			if (Same(_apply.Binding, _taskbar.Binding) || Same(_apply.Binding, _restore.Binding) || Same(_taskbar.Binding, _restore.Binding))
			{
				_error.Text = "Two actions use the same shortcut. Give each its own keys.";
				return;
			}
			DialogResult = DialogResult.OK;
		}
	}
}
