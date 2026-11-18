using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace ASConfig
{
	internal static class PhraseStore
	{
		public static string UserPath(string workDir)
		{
			return Path.Combine(workDir, "search_settings.user.json");
		}

		public static string DefaultPath(string workDir)
		{
			return Path.Combine(workDir, "search_settings.json");
		}

		public static PhraseConfig Load(string workDir)
		{
			string path = ((!File.Exists(UserPath(workDir))) ? DefaultPath(workDir) : UserPath(workDir));
			try
			{
				if (File.Exists(path))
				{
					PhraseConfig phraseConfig = new JavaScriptSerializer().Deserialize<PhraseConfig>(File.ReadAllText(path, Encoding.UTF8));
					if (phraseConfig != null && phraseConfig.Groups != null)
					{
						Normalize(phraseConfig);
						return phraseConfig;
					}
				}
			}
			catch
			{
			}
			PhraseConfig phraseConfig2 = new PhraseConfig();
			phraseConfig2.Groups = new List<PhraseGroup>();
			return phraseConfig2;
		}

		private static void Normalize(PhraseConfig c)
		{
			foreach (PhraseGroup group in c.Groups)
			{
				if (group.Phrases == null)
				{
					group.Phrases = new List<PhraseItem>();
				}
				if (string.IsNullOrEmpty(group.ColorHex))
				{
					group.ColorHex = "#FFA53C";
				}
			}
		}

		public static void Save(string workDir, PhraseConfig cfg)
		{
			JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
			string s = javaScriptSerializer.Serialize(cfg);
			s = Pretty(s);
			File.WriteAllText(UserPath(workDir), s, new UTF8Encoding(false));
		}

		private static string Pretty(string s)
		{
			StringBuilder sb = new StringBuilder();
			int ind = 0;
			bool flag = false;
			Action action = delegate
			{
				sb.Append('\n');
				sb.Append(new string(' ', ind * 2));
			};
			for (int i = 0; i < s.Length; i++)
			{
				char c = s[i];
				if (flag)
				{
					sb.Append(c);
					if (c == '"' && s[i - 1] != '\\')
					{
						flag = false;
					}
					continue;
				}
				switch (c)
				{
				case '"':
					flag = true;
					sb.Append(c);
					break;
				case '[':
				case '{':
					ind++;
					sb.Append(c);
					action();
					break;
				case ']':
				case '}':
					ind--;
					action();
					sb.Append(c);
					break;
				case ',':
					sb.Append(c);
					action();
					break;
				case ':':
					sb.Append(": ");
					break;
				default:
					sb.Append(c);
					break;
				}
			}
			return sb.ToString();
		}
	}
}
