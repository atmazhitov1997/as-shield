using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace ASConfig
{
	internal static class SessionDocx
	{
		public class Info
		{
			public string Machine = "";

			public string Os = "";

			public string Operator = "";

			public string Admin = "";

			public string SessionStart = "";

			public string SessionEnd = "";

			public int Total;

			public int Ok;

			public int Err;

			public bool ChainOk;

			public int BrokenAt;
		}

		private static string Esc(string t)
		{
			return SecurityElement.Escape(t ?? "");
		}

		private static string Run(string t, int sz, bool bold, string color)
		{
			return "<w:r><w:rPr><w:rFonts w:ascii=\"Times New Roman\" w:hAnsi=\"Times New Roman\" w:cs=\"Times New Roman\"/>" + ((!bold) ? "" : "<w:b/>") + ((color == null) ? "" : ("<w:color w:val=\"" + color + "\"/>")) + "<w:sz w:val=\"" + sz + "\"/><w:szCs w:val=\"" + sz + "\"/></w:rPr><w:t xml:space=\"preserve\">" + Esc(t) + "</w:t></w:r>";
		}

		private static string Run(string t, int sz, bool bold)
		{
			return Run(t, sz, bold, null);
		}

		private static string Para(string jc, string runs, int sz)
		{
			return "<w:p><w:pPr>" + ((jc == null) ? "" : ("<w:jc w:val=\"" + jc + "\"/>")) + "<w:rPr><w:rFonts w:ascii=\"Times New Roman\" w:hAnsi=\"Times New Roman\" w:cs=\"Times New Roman\"/><w:sz w:val=\"" + sz + "\"/><w:szCs w:val=\"" + sz + "\"/></w:rPr></w:pPr>" + runs + "</w:p>";
		}

		private static string Cell(string runs, int wdt, string jc)
		{
			return "<w:tc><w:tcPr><w:tcW w:w=\"" + wdt + "\" w:type=\"dxa\"/><w:vAlign w:val=\"center\"/></w:tcPr>" + Para(jc, runs, 20) + "</w:tc>";
		}

		public static void Write(string path, List<ActLog.Row> rows, Info info)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(Para("center", Run("SESSION REPORT", 32, true), 32));
			stringBuilder.Append(Para("center", Run("of the AS Shield security tool for one session", 26, true), 26));
			stringBuilder.Append(Para(null, "", 20));
			stringBuilder.Append(Para(null, Run("Workstation: ", 24, false) + Run(info.Machine, 24, true), 24));
			if (!string.IsNullOrEmpty(info.Os))
			{
				stringBuilder.Append(Para(null, Run("Operating system: ", 24, false) + Run(info.Os, 24, false), 24));
			}
			stringBuilder.Append(Para(null, Run("Operator (account): ", 24, false) + Run(info.Operator, 24, false), 24));
			stringBuilder.Append(Para(null, Run("Session start: ", 24, false) + Run(info.SessionStart, 24, false) + Run("     End: ", 24, false) + Run(info.SessionEnd, 24, false), 24));
			stringBuilder.Append(Para(null, Run("Actions in this session: ", 24, false) + Run(info.Total.ToString(), 24, true) + Run("   (succeeded: ", 24, false) + Run(info.Ok.ToString(), 24, false, "006100") + Run(", failed: ", 24, false) + Run(info.Err.ToString(), 24, false, (info.Err <= 0) ? null : "C00000") + Run(")", 24, false), 24));
			stringBuilder.Append(Para(null, "", 14));
			int[] array = new int[4] { 700, 2100, 5300, 2500 };
			StringBuilder stringBuilder2 = new StringBuilder();
			stringBuilder2.Append("<w:tbl><w:tblPr><w:tblW w:w=\"0\" w:type=\"auto\"/><w:tblBorders><w:top w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/><w:left w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/><w:bottom w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/><w:right w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/><w:insideH w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/><w:insideV w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/></w:tblBorders></w:tblPr><w:tblGrid>");
			int[] array2 = array;
			foreach (int num in array2)
			{
				stringBuilder2.Append("<w:gridCol w:w=\"" + num + "\"/>");
			}
			stringBuilder2.Append("</w:tblGrid>");
			stringBuilder2.Append("<w:tr><w:trPr><w:tblHeader/></w:trPr>" + Cell(Run("No.", 20, true), array[0], "center") + Cell(Run("Time", 20, true), array[1], "center") + Cell(Run("Action performed", 20, true), array[2], "center") + Cell(Run("Result", 20, true), array[3], "center") + "</w:tr>");
			int num2 = 0;
			foreach (ActLog.Row row in rows)
			{
				num2++;
				bool flag = row.Result != null && (row.Result.StartsWith("ERROR") || row.Result.StartsWith("STOPPED") || row.Result.StartsWith("WITH ERRORS") || row.Result.StartsWith("FAIL"));
				string color = ((!flag) ? "006100" : "C00000");
				stringBuilder2.Append("<w:tr>" + Cell(Run(num2.ToString(), 20, false), array[0], "center") + Cell(Run(row.Timestamp, 20, false), array[1], "center") + Cell(Run((!string.IsNullOrEmpty(row.Details)) ? row.Details : row.Action, 20, false), array[2], "left") + Cell(Run(row.Result, 20, flag, color), array[3], "left") + "</w:tr>");
			}
			stringBuilder2.Append("</w:tbl>");
			stringBuilder.Append((object)stringBuilder2);
			stringBuilder.Append(Para(null, "", 18));
			string t = ((!info.ChainOk) ? ("WARNING: the log integrity check found a break in the hash chain at record no. " + info.BrokenAt + ". The log may have been modified.") : "Log integrity check: the hash chain is intact - no records were changed.");
			stringBuilder.Append(Para(null, Run("Integrity check: ", 24, true) + Run(t, 24, false, (!info.ChainOk) ? "C00000" : null), 24));
			stringBuilder.Append(Para(null, "", 28));
			StringBuilder stringBuilder3 = new StringBuilder(Run("System administrator", 24, false));
			for (int j = 0; j < 8; j++)
			{
				stringBuilder3.Append(Run("     ", 24, false));
			}
			stringBuilder3.Append(Run((!string.IsNullOrWhiteSpace(info.Admin)) ? info.Admin.Trim() : "_____________________", 24, false));
			stringBuilder.Append(Para(null, stringBuilder3.ToString(), 24));
			stringBuilder.Append(Para(null, "", 20));
			stringBuilder.Append(Para(null, Run("Date: ____ / ____ / ________", 24, false), 24));
			string text = string.Concat("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>", stringBuilder, "<w:sectPr><w:pgSz w:w=\"11906\" w:h=\"16838\"/><w:pgMar w:top=\"1134\" w:right=\"850\" w:bottom=\"1134\" w:left=\"1417\" w:header=\"708\" w:footer=\"708\" w:gutter=\"0\"/></w:sectPr></w:body></w:document>");
			string text2 = path + ".tmp";
			if (File.Exists(text2))
			{
				File.Delete(text2);
			}
			using (FileStream stream = File.Create(text2))
			{
				using (ZipArchive z = new ZipArchive(stream, ZipArchiveMode.Create))
				{
					Entry(z, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
					Entry(z, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
					Entry(z, "word/document.xml", text);
				}
			}
			if (File.Exists(path))
			{
				File.Delete(path);
			}
			File.Move(text2, path);
		}

		private static void Entry(ZipArchive z, string name, string text)
		{
			ZipArchiveEntry zipArchiveEntry = z.CreateEntry(name);
			using (Stream stream = zipArchiveEntry.Open())
			{
				byte[] bytes = new UTF8Encoding(false).GetBytes(text);
				stream.Write(bytes, 0, bytes.Length);
			}
		}
	}
}
