using System;
using System.Threading;
using System.Windows.Forms;
using SRWE.Core;
using SRWE.UI;

namespace SRWE
{
	static class Program
	{
		[STAThread]
		static void Main(string[] args)
		{
			Native.SetProcessDPIAware(); // real pixels on scaled displays, before any window exists

			bool created;
			using (Mutex mutex = new Mutex(true, "RealBorderlessGaming.SingleInstance", out created))
			{
				if (!created)
				{
					// already running (probably in the tray): ask it to show itself
					Native.PostMessage(Native.HWND_BROADCAST, Native.ShowMeMessage, IntPtr.Zero, IntPtr.Zero);
					return;
				}

				Application.EnableVisualStyles();
				Application.SetCompatibleTextRenderingDefault(false);
				Theme.SetMode(AppState.Settings.ThemeMode);   // dark / light / system, remembered between runs

				// A previous run may have died with the taskbar hidden.
				TaskbarController.RecoverFromCrash();
				StartupManager.MigrateLegacy();

				Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
				Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
				{
					Storage.Log("UI exception: " + e.Exception);
				};
				AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
				{
					Storage.Log("Fatal: " + e.ExceptionObject);
					TaskbarController.ForceShowAll();
				};

				bool startHidden = Array.IndexOf(args, "--tray") >= 0;
				Application.Run(new LauncherForm(startHidden));
			}
		}
	}
}
