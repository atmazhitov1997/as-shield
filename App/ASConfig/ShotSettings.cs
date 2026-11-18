using System;
using System.IO;
using System.Text;

namespace ASConfig
{
	internal class ShotSettings
	{
		public string Admin = "";

		public string Folder;

		private readonly string file;

		public string PngDir => Path.Combine(Folder, "PNG");

		public ShotSettings(string workDir)
		{
			file = Path.Combine(workDir, "shots.ini");
			Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Evidence screenshots");
			try
			{
				if (!File.Exists(file))
				{
					return;
				}
				string[] array = File.ReadAllLines(file, Encoding.UTF8);
				foreach (string text in array)
				{
					int num = text.IndexOf('=');
					if (num > 0)
					{
						string text2 = text.Substring(0, num);
						string text3 = text.Substring(num + 1);
						if (text2 == "Admin")
						{
							Admin = text3;
						}
						else if (text2 == "Folder" && text3.Trim().Length > 0)
						{
							Folder = text3;
						}
					}
				}
			}
			catch
			{
			}
		}

		public void Save()
		{
			try
			{
				File.WriteAllText(file, "Admin=" + Admin + "\r\nFolder=" + Folder + "\r\n", new UTF8Encoding(true));
			}
			catch
			{
			}
		}

		public string DocPath(int n)
		{
			return Path.Combine(Folder, "Screenshot " + n + ".docx");
		}
	}
}
