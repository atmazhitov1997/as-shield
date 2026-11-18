using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

namespace ASConfig
{
	internal static class ShotCatalog
	{

		private static List<ShotItem> items;

		/// <summary>v2: items from Catalog/shots.json in the payload; an empty list when the file is missing or damaged.</summary>
		public static List<ShotItem> Items => items ?? (items = LoadFromPayload() ?? Builtin);

		private static List<ShotItem> LoadFromPayload()
		{
			try
			{
				using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
				{
					if (s == null) return null;
					using (ZipArchive z = new ZipArchive(s, ZipArchiveMode.Read))
					{
						ZipArchiveEntry e = z.GetEntry("Catalog/shots.json") ?? z.GetEntry("Catalog\\shots.json");
						if (e == null) return null;
						string text;
						using (StreamReader r = new StreamReader(e.Open(), Encoding.UTF8)) text = r.ReadToEnd();
						Dictionary<string, object> root = new JavaScriptSerializer().DeserializeObject(text) as Dictionary<string, object>;
						object raw;
						if (root == null || !root.TryGetValue("items", out raw) || !(raw is IEnumerable)) return null;
						List<ShotItem> list = new List<ShotItem>();
						foreach (object o in (IEnumerable)raw)
						{
							Dictionary<string, object> d = o as Dictionary<string, object>;
							if (d == null) continue;
							ShotItem it = new ShotItem();
							object v;
							if (d.TryGetValue("N", out v)) it.N = Convert.ToInt32(v);
							if (d.TryGetValue("Title", out v)) it.Title = Convert.ToString(v);
							if (d.TryGetValue("Hint", out v)) it.Hint = Convert.ToString(v);
							if (d.TryGetValue("Expect", out v)) it.Expect = Convert.ToInt32(v);
							if (d.TryGetValue("NoCapture", out v)) it.NoCapture = Convert.ToBoolean(v);
							if (d.TryGetValue("Prereq", out v)) it.Prereq = Convert.ToString(v);
							if (d.TryGetValue("Seq", out v)) it.Seq = Convert.ToInt32(v);
							if (d.TryGetValue("Apply", out v)) it.Apply = Convert.ToString(v);
							if (d.TryGetValue("Node", out v)) it.Node = Convert.ToString(v);
							if (d.TryGetValue("Tree", out v)) it.Tree = Convert.ToBoolean(v);
							if (d.TryGetValue("Open", out v) && v is IEnumerable)
							{
								List<string> open = new List<string>();
								foreach (object x in (IEnumerable)v) open.Add(Convert.ToString(x));
								it.Open = open.ToArray();
							}
							if (it.N > 0 && !string.IsNullOrEmpty(it.Title)) list.Add(it);
						}
						return list.Count > 0 ? list : null;
					}
				}
			}
			catch
			{
				return null;
			}
		}

		/// <summary>No built-in catalog: evidence items come from Catalog/shots.json in the payload.</summary>
		public static readonly List<ShotItem> Builtin = new List<ShotItem>();
	}
}
