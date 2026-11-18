using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ASConfig
{
	internal static class ShotLauncher
	{
		private delegate bool EnumWinProc(IntPtr h, IntPtr l);

		private const int SW_SHOWMAXIMIZED = 3;

		private const int WM_KEYDOWN = 256;

		private const int WM_KEYUP = 257;

		private const int VK_MULTIPLY = 106;

		[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
		private static extern bool SHObjectProperties(IntPtr hwnd, uint type, string name, string page);

		[DllImport("user32.dll")]
		private static extern bool ShowWindow(IntPtr h, int cmd);

		[DllImport("user32.dll")]
		private static extern bool SetForegroundWindow(IntPtr h);

		[DllImport("user32.dll")]
		private static extern bool EnumChildWindows(IntPtr h, EnumWinProc cb, IntPtr l);

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		private static extern int GetClassName(IntPtr h, StringBuilder s, int max);

		[DllImport("user32.dll")]
		private static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

		public static void Open(ShotItem item, string workDir)
		{
			if (Environment.GetEnvironmentVariable("ASPREVIEW") != null)
			{
				return;
			}
			Process process = null;
			string[] open = item.Open;
			foreach (string text in open)
			{
				try
				{
					int num = text.IndexOf(':');
					string text2 = text.Substring(0, num);
					string text3 = text.Substring(num + 1);
					switch (text2)
					{
					case "run":
					{
						ProcessStartInfo processStartInfo = new ProcessStartInfo(text3);
						processStartInfo.UseShellExecute = true;
						process = Process.Start(processStartInfo);
						break;
					}
					case "cmd":
					{
						string[] array2 = text3.Split(new char[1] { '|' }, 2);
						ProcessStartInfo processStartInfo = new ProcessStartInfo(array2[0], (array2.Length <= 1) ? "" : array2[1]);
						processStartInfo.UseShellExecute = true;
						process = Process.Start(processStartInfo);
						break;
					}
					case "props":
					{
						string name = Environment.ExpandEnvironmentVariables(text3);
						string page = ((!(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "uk")) ? "Security" : "\u0411\u0435\u0437\u043f\u0435\u043a\u0430");
						SHObjectProperties(IntPtr.Zero, 2u, name, page);
						break;
					}
					case "ev":
					{
						string[] array = text3.Split(new char[1] { ':' }, 2);
						ProcessStartInfo processStartInfo = new ProcessStartInfo("eventvwr.exe", "/v:\"" + EventView(workDir, item.N, array[0], array[1]) + "\"");
						processStartInfo.UseShellExecute = true;
						process = Process.Start(processStartInfo);
						break;
					}
					}
				}
				catch (Exception ex)
				{
					MessageBox.Show("Could not open: " + text + "\n" + ex.Message, "Evidence screenshots");
				}
			}
			if (process != null)
			{
				BringUp(process, item.Tree);
			}
		}

		private static void BringUp(Process proc, bool expandTree)
		{
			Thread thread = new Thread((ThreadStart)delegate
			{
				IntPtr intPtr = IntPtr.Zero;
				for (int i = 0; i < 40; i++)
				{
					if (!(intPtr == IntPtr.Zero))
					{
						break;
					}
					try
					{
						proc.Refresh();
						if (!proc.HasExited)
						{
							intPtr = proc.MainWindowHandle;
						}
					}
					catch
					{
					}
					if (intPtr == IntPtr.Zero)
					{
						Thread.Sleep(200);
					}
				}
				if (!(intPtr == IntPtr.Zero))
				{
					try
					{
						ShowWindow(intPtr, 3);
						SetForegroundWindow(intPtr);
					}
					catch
					{
					}
					if (expandTree)
					{
						Thread.Sleep(900);
						IntPtr intPtr2 = FindTree(intPtr);
						if (intPtr2 != IntPtr.Zero)
						{
							try
							{
								PostMessage(intPtr2, 256, (IntPtr)106, IntPtr.Zero);
								PostMessage(intPtr2, 257, (IntPtr)106, IntPtr.Zero);
							}
							catch
							{
							}
						}
					}
				}
			});
			thread.IsBackground = true;
			Thread thread2 = thread;
			thread2.Start();
		}

		private static IntPtr FindTree(IntPtr root)
		{
			IntPtr found = IntPtr.Zero;
			StringBuilder sb = new StringBuilder(64);
			EnumWinProc enumWinProc = null;
			enumWinProc = delegate(IntPtr h, IntPtr l)
			{
				sb.Length = 0;
				GetClassName(h, sb, sb.Capacity);
				if (sb.ToString() == "SysTreeView32")
				{
					found = h;
					return false;
				}
				return true;
			};
			try
			{
				EnumChildWindows(root, enumWinProc, IntPtr.Zero);
			}
			catch
			{
			}
			return found;
		}

		private static string EventView(string workDir, int n, string channel, string ids)
		{
			string text = Path.Combine(workDir, "views");
			Directory.CreateDirectory(text);
			string text2 = Path.Combine(text, "shot" + n + ".xml");
			string text3 = string.Join(" or ", from i in ids.Split(new char[1] { ',' })
				select "EventID=" + i.Trim());
			string contents = "<ViewerConfig><QueryConfig><QueryParams><UserQuery /></QueryParams><QueryNode><Name>Screenshot " + n + " — " + SecurityElement.Escape(channel) + "</Name><QueryList><Query Id=\"0\" Path=\"" + channel + "\"><Select Path=\"" + channel + "\">*[System[(" + text3 + ")]]</Select></Query></QueryList></QueryNode></QueryConfig></ViewerConfig>";
			File.WriteAllText(text2, contents, new UTF8Encoding(true));
			return text2;
		}
	}
}
