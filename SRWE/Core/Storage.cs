using System;
using System.IO;
using System.Xml.Serialization;

namespace SRWE.Core
{
	internal static class Storage
	{
		public static readonly string Dir = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppInfo.DataDir);

		static Storage()
		{
			try
			{
				Directory.CreateDirectory(Dir);
				// carry profiles/settings over from the earlier "SRWE" build
				string old = Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppInfo.LegacyDataDir);
				foreach (string f in new string[] { "profiles.xml", "launcher.xml" })
				{
					string from = Path.Combine(old, f), to = Path.Combine(Dir, f);
					if (File.Exists(from) && !File.Exists(to)) File.Copy(from, to);
				}
			}
			catch { }
		}

		public static T Load<T>(string file) where T : new()
		{
			string path = Path.Combine(Dir, file);
			try
			{
				if (File.Exists(path))
				{
					using (FileStream fs = File.OpenRead(path))
						return (T)new XmlSerializer(typeof(T)).Deserialize(fs);
				}
			}
			catch (Exception ex)
			{
				Log("Could not read " + file + ": " + ex.Message);
				try { File.Copy(path, path + ".bad", true); } catch { }
			}
			return new T();
		}

		public static void Save<T>(string file, T obj)
		{
			string path = Path.Combine(Dir, file);
			string tmp = path + ".tmp";
			try
			{
				using (FileStream fs = File.Create(tmp))
					new XmlSerializer(typeof(T)).Serialize(fs, obj);
				if (File.Exists(path)) File.Replace(tmp, path, null);
				else File.Move(tmp, path);
			}
			catch (Exception ex)
			{
				Log("Could not save " + file + ": " + ex.Message);
			}
		}

		public static void Log(string message)
		{
			try
			{
				File.AppendAllText(Path.Combine(Dir, "rbg.log"),
					DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
			}
			catch { }
		}
	}
}
