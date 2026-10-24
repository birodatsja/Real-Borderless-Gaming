using System;
using System.Drawing;
using System.Windows.Forms;

namespace SRWE.UI
{
	/// <summary>Base for every window of the app: follows the dark/light theme, including the title bar.</summary>
	internal class ThemedForm : Form
	{
		public ThemedForm()
		{
			Tag = "b:bg";
			BackColor = Theme.Bg;
			ForeColor = Theme.Text;
			Font = Theme.Face(9.5f, FontStyle.Regular);
			Theme.Changed += OnThemeChanged;
		}

		/// <summary>Override to restyle things the tag system can't reach (menus, scrollbars...).</summary>
		protected virtual void OnThemeApplied() { }

		private void OnThemeChanged(object sender, EventArgs e)
		{
			ForeColor = Theme.Text;
			Theme.ApplyTags(this);
			if (IsHandleCreated) UiNative.TitleBar(Handle);
			OnThemeApplied();
			Invalidate(true);
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			UiNative.TitleBar(Handle);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing) Theme.Changed -= OnThemeChanged;
			base.Dispose(disposing);
		}
	}
}
