using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;

namespace SRWE.Core
{
	/// <summary>
	/// "Start with Windows" via a logon Scheduled Task. A plain Run-key entry can't start an
	/// app that requires administrator rights without a UAC prompt; a task with highest
	/// privileges can. Battery and 72h-timeout defaults are switched off.
	/// </summary>
	internal static class StartupManager
	{
		private const string TaskName = AppInfo.StartupTask;

		private static int RunSchtasks(string args)
		{
			ProcessStartInfo psi = new ProcessStartInfo("schtasks.exe", args);
			psi.CreateNoWindow = true;
			psi.UseShellExecute = false;
			psi.RedirectStandardOutput = true;
			psi.RedirectStandardError = true;
			using (Process p = Process.Start(psi))
			{
				p.StandardOutput.ReadToEnd();
				p.StandardError.ReadToEnd();
				p.WaitForExit(15000);
				return p.HasExited ? p.ExitCode : -1;
			}
		}

		public static bool IsEnabled()
		{
			try { return RunSchtasks("/Query /TN \"" + TaskName + "\"") == 0; }
			catch { return false; }
		}

		/// <summary>Replace the startup task of the earlier "SRWE" build (it points at a file that no longer exists).</summary>
		public static void MigrateLegacy()
		{
			try
			{
				if (RunSchtasks("/Query /TN \"" + AppInfo.LegacyStartupTask + "\"") != 0) return;
				RunSchtasks("/Delete /TN \"" + AppInfo.LegacyStartupTask + "\" /F");
				Set(true);
			}
			catch { }
		}

		public static bool Set(bool enable)
		{
			try
			{
				if (!enable) return RunSchtasks("/Delete /TN \"" + TaskName + "\" /F") == 0 || !IsEnabled();

				string user = SecurityElement.Escape(WindowsIdentity.GetCurrent().Name);
				string exe = SecurityElement.Escape(Application.ExecutablePath);
				string xml =
					"<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n" +
					"<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">\r\n" +
					" <Triggers><LogonTrigger><Enabled>true</Enabled><UserId>" + user + "</UserId></LogonTrigger></Triggers>\r\n" +
					" <Principals><Principal id=\"Author\"><UserId>" + user + "</UserId>" +
					"<LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>\r\n" +
					" <Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>" +
					"<DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>" +
					"<StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>" +
					"<ExecutionTimeLimit>PT0S</ExecutionTimeLimit><Enabled>true</Enabled></Settings>\r\n" +
					" <Actions Context=\"Author\"><Exec><Command>" + exe + "</Command><Arguments>--tray</Arguments></Exec></Actions>\r\n" +
					"</Task>";
				string tmp = Path.Combine(Path.GetTempPath(), "rbg-task.xml");
				File.WriteAllText(tmp, xml, Encoding.Unicode);
				try { return RunSchtasks("/Create /TN \"" + TaskName + "\" /XML \"" + tmp + "\" /F") == 0; }
				finally { try { File.Delete(tmp); } catch { } }
			}
			catch (Exception ex)
			{
				Storage.Log("Startup task failed: " + ex.Message);
				return false;
			}
		}
	}
}
