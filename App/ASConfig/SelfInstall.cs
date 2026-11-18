using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ASConfig
{
	internal static class SelfInstall
	{
		public const string AppName = "AS Shield";

		public const string AppVersion = "2.0.0";

		private const string UninstKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\ASShield";

		public static string InstallDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "AS Shield");

		public static string InstalledExe => Path.Combine(InstallDir, "AS Shield.exe");

		private static string StartMenuLnk => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "AS Shield.lnk");

		private static string DesktopLnk => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "AS Shield.lnk");

		private static string Norm(string p)
		{
			try
			{
				return Path.GetFullPath(p).TrimEnd('\\', '/');
			}
			catch
			{
				return p ?? "";
			}
		}

		public static bool IsInstalledCopy()
		{
			return string.Equals(Norm(Application.ExecutablePath), Norm(InstalledExe), StringComparison.OrdinalIgnoreCase);
		}

		public static string InstalledVersion()
		{
			if (!File.Exists(InstalledExe))
			{
				return null;
			}
			try
			{
				using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\ASShield"))
				{
					if (registryKey != null)
					{
						return (registryKey.GetValue("DisplayVersion") as string) ?? "?";
					}
				}
			}
			catch
			{
			}
			return "?";
		}

		public static void Install(bool desktop, Action<string> log)
		{
			log("Copying the program to '" + InstallDir + "'");
			Directory.CreateDirectory(InstallDir);
			try
			{
				File.Copy(Application.ExecutablePath, InstalledExe, true);
			}
			catch (IOException)
			{
				throw new Exception("The installed program is currently running. Close AS Shield and try again.");
			}
			log("Creating shortcuts");
			CreateShortcut(StartMenuLnk);
			if (desktop)
			{
				CreateShortcut(DesktopLnk);
			}
			else
			{
				TryDelete(DesktopLnk);
			}
			log("Registering in 'Programs and Features'");
			using (RegistryKey registryKey = Registry.LocalMachine.CreateSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\ASShield"))
			{
				registryKey.SetValue("DisplayName", "AS Shield");
				registryKey.SetValue("DisplayVersion", AppVersion);
				registryKey.SetValue("Publisher", "AS Shield");
				registryKey.SetValue("DisplayIcon", InstalledExe + ",0");
				registryKey.SetValue("InstallLocation", InstallDir);
				registryKey.SetValue("UninstallString", "\"" + InstalledExe + "\" /uninstall");
				registryKey.SetValue("NoModify", 1, RegistryValueKind.DWord);
				registryKey.SetValue("NoRepair", 1, RegistryValueKind.DWord);
				registryKey.SetValue("EstimatedSize", (int)(new FileInfo(InstalledExe).Length / 1024), RegistryValueKind.DWord);
			}
			log("Installation complete");
		}

		public static void Uninstall()
		{
			if (MessageBox.Show("Remove AS Shield from this PC?\n\nThe action log, reports and phrase settings in %ProgramData% and %LocalAppData% will be kept.", "AS Shield", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
			{
				return;
			}
			TryDelete(StartMenuLnk);
			TryDelete(DesktopLnk);
			try
			{
				Registry.LocalMachine.DeleteSubKeyTree("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\ASShield", false);
			}
			catch
			{
			}
			try
			{
				string[] files = Directory.GetFiles(InstallDir);
				foreach (string p in files)
				{
					if (!string.Equals(Norm(p), Norm(Application.ExecutablePath), StringComparison.OrdinalIgnoreCase))
					{
						TryDelete(p);
					}
				}
			}
			catch
			{
			}
			MessageBox.Show("AS Shield has been removed.\n\nThe folder '" + InstallDir + "' can be deleted manually after this window is closed.", "AS Shield", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}

		private static void CreateShortcut(string lnkPath)
		{
			object obj = null;
			try
			{
				Type typeFromProgID = Type.GetTypeFromProgID("WScript.Shell");
				if (!(typeFromProgID == null))
				{
					obj = Activator.CreateInstance(typeFromProgID);
					object obj2 = typeFromProgID.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, obj, new object[1] { lnkPath });
					Type type = obj2.GetType();
					type.InvokeMember("TargetPath", BindingFlags.SetProperty, null, obj2, new object[1] { InstalledExe });
					type.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, obj2, new object[1] { InstallDir });
					type.InvokeMember("IconLocation", BindingFlags.SetProperty, null, obj2, new object[1] { InstalledExe + ",0" });
					type.InvokeMember("Description", BindingFlags.SetProperty, null, obj2, new object[1] { "AS Shield" });
					type.InvokeMember("Save", BindingFlags.InvokeMethod, null, obj2, null);
				}
			}
			catch
			{
			}
			finally
			{
				if (obj != null)
				{
					try
					{
						Marshal.ReleaseComObject(obj);
					}
					catch
					{
					}
				}
			}
		}

		private static void TryDelete(string p)
		{
			try
			{
				if (File.Exists(p))
				{
					File.Delete(p);
				}
			}
			catch
			{
			}
		}
	}
}
