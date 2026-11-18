using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ASConfig
{
	internal static class Fonts
	{
		private static readonly PrivateFontCollection pfc = new PrivateFontCollection();

		private static FontFamily mono;

		private static bool gdiOk;

		private static readonly Dictionary<string, Font> cache = new Dictionary<string, Font>();

		private static string fallback = "Consolas";

		private static bool inited;

		private static bool? hasIcons;

		public static string MonoName
		{
			get
			{
				Init();
				return (mono == null || !gdiOk) ? fallback : mono.Name;
			}
		}

		[DllImport("gdi32.dll")]
		private static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, out uint pcFonts);

		[DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
		private static extern int AddFontResourceEx(string name, uint fl, IntPtr pdv);

		private static void Init()
		{
			if (mono != null || inited)
			{
				return;
			}
			inited = true;
			try
			{
				using (Font font = new Font("JetBrains Mono", 10f))
				{
					if (font.Name == "JetBrains Mono")
					{
						fallback = "JetBrains Mono";
						gdiOk = false;
					}
				}
				if (Environment.GetEnvironmentVariable("ASNOPFC") != null)
				{
					return;
				}
				string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ASShield", "fonts");
				Directory.CreateDirectory(text);
				Assembly executingAssembly = Assembly.GetExecutingAssembly();
				string[] array = new string[2] { "JetBrainsMono-Regular.ttf", "JetBrainsMono-Bold.ttf" };
				foreach (string text2 in array)
				{
					string text3 = Path.Combine(text, text2);
					using (Stream stream = executingAssembly.GetManifestResourceStream(text2))
					{
						if (stream == null)
						{
							continue;
						}
						if (!File.Exists(text3) || new FileInfo(text3).Length != stream.Length)
						{
							try
							{
								using (FileStream destination = File.Create(text3))
								{
									stream.CopyTo(destination);
								}
							}
							catch
							{
							}
						}
						goto IL_0147;
					}
					IL_0147:
					if (!File.Exists(text3))
					{
						continue;
					}
					pfc.AddFontFile(text3);
					try
					{
						if (AddFontResourceEx(text3, 16u, IntPtr.Zero) > 0)
						{
							gdiOk = true;
						}
					}
					catch
					{
					}
				}
				if (pfc.Families.Length > 0)
				{
					mono = pfc.Families[0];
				}
			}
			catch
			{
				mono = null;
			}
		}

		public static Font Mono(float size, bool bold = false)
		{
			Init();
			string key = "m" + size + bold;
			if (cache.TryGetValue(key, out var value))
			{
				return value;
			}
			try
			{
				value = ((mono == null) ? new Font(fallback, size, bold ? FontStyle.Bold : FontStyle.Regular) : new Font(mono, size, bold ? FontStyle.Bold : FontStyle.Regular));
			}
			catch
			{
				value = new Font(fallback, size, bold ? FontStyle.Bold : FontStyle.Regular);
			}
			cache[key] = value;
			return value;
		}

		public static Font MonoGdi(float size, bool bold = false)
		{
			string key = "g" + size + bold;
			if (cache.TryGetValue(key, out var value))
			{
				return value;
			}
			Init();
			try
			{
				value = ((mono == null || !gdiOk) ? new Font(fallback, size, bold ? FontStyle.Bold : FontStyle.Regular) : new Font(mono, size, bold ? FontStyle.Bold : FontStyle.Regular));
			}
			catch
			{
				value = new Font("Consolas", size, bold ? FontStyle.Bold : FontStyle.Regular);
			}
			cache[key] = value;
			return value;
		}

		public static Font NewMono(float size, bool bold = false)
		{
			Font font = Mono(size, bold);
			return new Font(font.FontFamily, size, bold ? FontStyle.Bold : FontStyle.Regular);
		}

		public static Font Sans(float size, bool bold = false)
		{
			string key = "s" + size + bold;
			if (cache.TryGetValue(key, out var value))
			{
				return value;
			}
			value = new Font((!bold) ? "Segoe UI" : "Segoe UI Semibold", size, bold ? FontStyle.Bold : FontStyle.Regular);
			cache[key] = value;
			return value;
		}

		public static Font Icons(float size)
		{
			string key = "i" + size;
			if (cache.TryGetValue(key, out var value))
			{
				return value;
			}
			value = new Font("Segoe MDL2 Assets", size);
			if (value.Name != "Segoe MDL2 Assets")
			{
				value = new Font("Segoe Fluent Icons", size);
			}
			hasIcons = value.Name == "Segoe MDL2 Assets" || value.Name == "Segoe Fluent Icons";
			if (hasIcons != true)
			{
				value = Mono(size * 0.95f, true);
			}
			cache[key] = value;
			return value;
		}

		public static string Glyph(string icon, string title)
		{
			bool? flag = hasIcons;
			if (!flag.HasValue)
			{
				Icons(10f);
			}
			if (hasIcons == true)
			{
				return icon;
			}
			return (!string.IsNullOrEmpty(title)) ? title.Substring(0, 1).ToUpper() : "•";
		}
	}
}
