using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

namespace ASConfig
{
	/// <summary>Describes one card of the main screen (from Catalog/menu.json or Profiles/*/profile.json).</summary>
	internal class CardDef
	{
		public string Section;      // as | extra | tools | control
		public string Action;       // for a profile: "profile:<id>"
		public string Title;
		public string Desc;
		public string Icon;
		public Color Accent;
		public string Kind;         // script | ui
		public string[] Os;         // null = any supported version
		public int MinBuild;
		public string Note;         // caption shown when the OS is not supported
		public string Confirm;      // check | change | danger
		public string ConfirmText;
		public bool IsLocal;        // profile from %ProgramData%\ASShield\Profiles (not from the exe)

		public bool IsAllowed(int build)
		{
			if (build < 10240) return false;
			if (MinBuild > 0 && build < MinBuild) return false;
			if (Os == null || Os.Length == 0) return true;
			string v = build >= 22000 ? "11" : "10";
			return Array.IndexOf(Os, v) >= 0;
		}
	}

	/// <summary>
	/// Action catalog: replaces the hard-coded MakeAction(...) calls in MainForm.
	/// Sources: the embedded payload.zip (Catalog/menu.json, Profiles/*/profile.json)
	/// + administrator-defined local profiles in %ProgramData%\ASShield\Profiles.
	/// </summary>
	internal static class Catalog
	{
		public static string LocalProfilesDir =>
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ASShield", "Profiles");

		private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 };

		public static Color ColorByName(string name)
		{
			switch (name)
			{
			case "Green": return Theme.Green;
			case "Amber": return Theme.Amber;
			case "Red": return Theme.Red;
			case "Teal": return Theme.Teal;
			case "Violet": return Theme.Violet;
			default: return Theme.Neon;
			}
		}

		/// <summary>Returns null when the payload has no menu.json (MainForm then falls back to the built-in menu).</summary>
		public static List<CardDef> Load(List<string> warnings)
		{
			Dictionary<string, string> files = ReadEmbedded();
			string menuText;
			if (!files.TryGetValue("Catalog/menu.json", out menuText)) return null;

			List<CardDef> result = new List<CardDef>();
			Dictionary<string, object> menu = AsDict(Json.DeserializeObject(menuText));
			List<string> disabled = AsStrings(Get(menu, "disabledActions"));

			// Profiles: built-in first, then local ones (a local profile cannot take over a built-in id).
			List<KeyValuePair<Dictionary<string, object>, bool>> profiles = new List<KeyValuePair<Dictionary<string, object>, bool>>();
			HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (KeyValuePair<string, string> f in files)
			{
				if (!f.Key.StartsWith("Profiles/", StringComparison.Ordinal) || !f.Key.EndsWith("/profile.json", StringComparison.Ordinal)) continue;
				try
				{
					Dictionary<string, object> p = AsDict(Json.DeserializeObject(f.Value));
					if (ids.Add(Str(p, "id"))) profiles.Add(new KeyValuePair<Dictionary<string, object>, bool>(p, false));
				}
				catch (Exception ex) { warnings.Add(f.Key + ": " + ex.Message); }
			}
			try
			{
				if (Directory.Exists(LocalProfilesDir))
				{
					foreach (string dir in Directory.GetDirectories(LocalProfilesDir))
					{
						string pf = Path.Combine(dir, "profile.json");
						if (!File.Exists(pf)) continue;
						try
						{
							Dictionary<string, object> p = AsDict(Json.DeserializeObject(File.ReadAllText(pf, Encoding.UTF8)));
							string id = Str(p, "id");
							if (!string.Equals(id, Path.GetFileName(dir), StringComparison.OrdinalIgnoreCase)) { warnings.Add(pf + ": id does not match the folder name"); continue; }
							if (!ids.Add(id)) { warnings.Add(pf + ": id '" + id + "' is already used by a built-in profile"); continue; }
							profiles.Add(new KeyValuePair<Dictionary<string, object>, bool>(p, true));
						}
						catch (Exception ex) { warnings.Add(pf + ": " + ex.Message); }
					}
				}
			}
			catch (Exception ex) { warnings.Add("Local profiles: " + ex.Message); }

			profiles.Sort((a, b) => Int(a.Key, "order", 100).CompareTo(Int(b.Key, "order", 100)));

			foreach (object so in AsList(Get(menu, "sections")))
			{
				Dictionary<string, object> s = AsDict(so);
				string sid = Str(s, "id");
				if (Str(s, "source") == "profiles")
				{
					foreach (KeyValuePair<Dictionary<string, object>, bool> kv in profiles)
					{
						Dictionary<string, object> p = kv.Key;
						Dictionary<string, object> os = AsDict(Get(p, "os"));
						CardDef c = new CardDef
						{
							Section = sid, Action = "profile:" + Str(p, "id"), Title = Str(p, "title"), Desc = Str(p, "description"),
							Icon = Str(p, "icon") ?? "\ue72e", Accent = ColorByName(Str(p, "color")), Kind = "script",
							Os = AsStrings(Get(os, "supported")).ToArray(), MinBuild = Int(os, "minBuild", 0), Note = Str(os, "note"),
							Confirm = "change", IsLocal = kv.Value
						};
						if (c.IsLocal) c.Desc = "[local profile] " + c.Desc;
						if (!disabled.Contains(c.Action)) result.Add(c);
					}
					continue;
				}
				foreach (object io in AsList(Get(s, "items")))
				{
					Dictionary<string, object> it = AsDict(io);
					string action = Str(it, "action");
					if (string.IsNullOrEmpty(action) || disabled.Contains(action)) continue;
					result.Add(new CardDef
					{
						Section = sid, Action = action, Title = Str(it, "title"), Desc = Str(it, "description"), Icon = Str(it, "icon"),
						Accent = ColorByName(Str(it, "color")), Kind = Str(it, "kind") ?? "script", Os = AsStrings(Get(it, "os")).ToArray(),
						Note = Str(it, "note"), Confirm = Str(it, "confirm"), ConfirmText = Str(it, "confirmText")
					});
				}
			}
			return result;
		}

		/// <summary>Text files of the catalog read straight from the embedded payload.zip (nothing is extracted to disk).</summary>
		private static Dictionary<string, string> ReadEmbedded()
		{
			Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.Ordinal);
			using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
			{
				if (s == null) return d;
				using (ZipArchive z = new ZipArchive(s, ZipArchiveMode.Read))
				{
					foreach (ZipArchiveEntry e in z.Entries)
					{
						string n = e.FullName.Replace('\\', '/');
						bool want = n == "Catalog/menu.json" || (n.StartsWith("Profiles/", StringComparison.Ordinal) && n.EndsWith("/profile.json", StringComparison.Ordinal));
						if (!want) continue;
						using (StreamReader r = new StreamReader(e.Open(), Encoding.UTF8)) d[n] = r.ReadToEnd();
					}
				}
			}
			return d;
		}

		private static object Get(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) ? v : null; }
		private static string Str(Dictionary<string, object> d, string k) { object v = Get(d, k); return v == null ? null : Convert.ToString(v); }
		private static int Int(Dictionary<string, object> d, string k, int def) { object v = Get(d, k); try { return v == null ? def : Convert.ToInt32(v); } catch { return def; } }
		private static Dictionary<string, object> AsDict(object o) { return o as Dictionary<string, object> ?? new Dictionary<string, object>(); }
		private static IEnumerable AsList(object o) { return (o as IEnumerable) ?? new object[0]; }
		private static List<string> AsStrings(object o)
		{
			List<string> l = new List<string>();
			if (o is string) { l.Add((string)o); return l; }
			if (o is IEnumerable) foreach (object x in (IEnumerable)o) if (x != null) l.Add(Convert.ToString(x));
			return l;
		}
	}
}
