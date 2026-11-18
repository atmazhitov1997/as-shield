using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ASConfig
{
	internal static class ActLog
	{
		public class Row
		{
			public string Seq = "";

			public string Timestamp = "";

			public string User = "";

			public string Machine = "";

			public string Action = "";

			public string Details = "";

			public string Result = "";

			public string PrevHash = "";

			public string Hash = "";
		}

		public static string LogPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ASShield", "action-log.csv");

		private static List<string> ParseCsvLine(string line)
		{
			List<string> list = new List<string>();
			StringBuilder stringBuilder = new StringBuilder();
			bool flag = false;
			for (int i = 0; i < line.Length; i++)
			{
				char c = line[i];
				if (flag)
				{
					if (c == '"')
					{
						if (i + 1 < line.Length && line[i + 1] == '"')
						{
							stringBuilder.Append('"');
							i++;
						}
						else
						{
							flag = false;
						}
					}
					else
					{
						stringBuilder.Append(c);
					}
					continue;
				}
				switch (c)
				{
				case '"':
					flag = true;
					break;
				case ',':
					list.Add(stringBuilder.ToString());
					stringBuilder.Clear();
					break;
				default:
					stringBuilder.Append(c);
					break;
				}
			}
			list.Add(stringBuilder.ToString());
			return list;
		}

		public static List<Row> Read()
		{
			List<Row> list = new List<Row>();
			string logPath = LogPath;
			if (!File.Exists(logPath))
			{
				return list;
			}
			string[] array;
			try
			{
				array = File.ReadAllLines(logPath, Encoding.UTF8);
			}
			catch
			{
				return list;
			}
			if (array.Length < 2)
			{
				return list;
			}
			List<string> list2 = ParseCsvLine(array[0]);
			int arg = list2.IndexOf("Seq");
			int arg2 = list2.IndexOf("Timestamp");
			int arg3 = list2.IndexOf("User");
			int arg4 = list2.IndexOf("Machine");
			int arg5 = list2.IndexOf("Action");
			int arg6 = list2.IndexOf("Details");
			int arg7 = list2.IndexOf("Result");
			int arg8 = list2.IndexOf("PrevHash");
			int arg9 = list2.IndexOf("Hash");
			for (int i = 1; i < array.Length; i++)
			{
				if (!string.IsNullOrWhiteSpace(array[i]))
				{
					List<string> f = ParseCsvLine(array[i]);
					Func<int, string> func = (int idx) => (idx < 0 || idx >= f.Count) ? "" : f[idx];
					list.Add(new Row
					{
						Seq = func(arg),
						Timestamp = func(arg2),
						User = func(arg3),
						Machine = func(arg4),
						Action = func(arg5),
						Details = func(arg6),
						Result = func(arg7),
						PrevHash = func(arg8),
						Hash = func(arg9)
					});
				}
			}
			return list;
		}

		private static string Sha256(string s)
		{
			using (SHA256 sHA = SHA256.Create())
			{
				return BitConverter.ToString(sHA.ComputeHash(Encoding.UTF8.GetBytes(s))).Replace("-", "");
			}
		}

		public static bool VerifyChain(List<Row> rows, out int brokenAt)
		{
			brokenAt = -1;
			string text = "GENESIS";
			for (int i = 0; i < rows.Count; i++)
			{
				Row row = rows[i];
				string s = text + "|" + row.Timestamp + "|" + row.User + "|" + row.Action + "|" + row.Details + "|" + row.Result;
				if (!string.Equals(Sha256(s), row.Hash, StringComparison.OrdinalIgnoreCase) || !string.Equals(text, row.PrevHash, StringComparison.OrdinalIgnoreCase))
				{
					brokenAt = i + 1;
					return false;
				}
				text = row.Hash;
			}
			return true;
		}
	}
}
