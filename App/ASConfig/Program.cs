using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace ASConfig
{
	internal static class Program
	{
		public static string LogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ASShield", "error.log");

		[DllImport("user32.dll")]
		private static extern bool SetProcessDPIAware();

		public static void ReportError(Exception ex)
		{
			string text = ((ex != null) ? ex.ToString() : "Unknown error");
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
				File.AppendAllText(LogPath, "==== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ====\r\n" + text + "\r\n\r\n");
			}
			catch
			{
			}
			try
			{
				Clipboard.SetText(text);
			}
			catch
			{
			}
			MessageBox.Show("An error occurred (the text has already been copied to the clipboard):\n\n" + ((text.Length <= 1500) ? text : (text.Substring(0, 1500) + "…")) + "\n\nLog: " + LogPath, "AS Shield — error", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}

		[STAThread]
		private static void Main(string[] args)
		{
			if (Environment.GetEnvironmentVariable("ASPREVIEW") == null && !new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
			{
				{
					try
					{
						ProcessStartInfo processStartInfo = new ProcessStartInfo(Application.ExecutablePath);
						processStartInfo.UseShellExecute = true;
						processStartInfo.Verb = "runas";
						processStartInfo.Arguments = string.Join(" ", args);
						Process.Start(processStartInfo);
					}
					catch
					{
						MessageBox.Show("Administrator rights are required.", "AS Shield");
					}
				}
			}
			else
			{
				try
				{
					SetProcessDPIAware();
				}
				catch
				{
				}
				Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
				Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
				{
					ReportError(e.Exception);
				};
				AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
				{
					ReportError(e.ExceptionObject as Exception);
				};
				Application.EnableVisualStyles();
				Application.SetCompatibleTextRenderingDefault(false);
				Application.Run(new MainForm());
			}
		}

	}
}
