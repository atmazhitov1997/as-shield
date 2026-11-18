using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace ASConfig
{
	internal static class AktDocx
	{
		private static string Esc(string t)
		{
			return SecurityElement.Escape(t ?? "");
		}

		private static string Run(string t, int sz, bool bold)
		{
			return "<w:r><w:rPr><w:rFonts w:ascii=\"Times New Roman\" w:hAnsi=\"Times New Roman\" w:cs=\"Times New Roman\"/>" + ((!bold) ? "" : "<w:b/>") + "<w:sz w:val=\"" + sz + "\"/><w:szCs w:val=\"" + sz + "\"/></w:rPr><w:t xml:space=\"preserve\">" + Esc(t) + "</w:t></w:r>";
		}

		private static string Para(string jc, string runs, int sz)
		{
			return "<w:p><w:pPr>" + ((jc == null) ? "" : ("<w:jc w:val=\"" + jc + "\"/>")) + "<w:rPr><w:rFonts w:ascii=\"Times New Roman\" w:hAnsi=\"Times New Roman\" w:cs=\"Times New Roman\"/><w:sz w:val=\"" + sz + "\"/><w:szCs w:val=\"" + sz + "\"/></w:rPr></w:pPr>" + runs + "</w:p>";
		}

		private static string Cell(string text, int wdt, bool bold, string jc)
		{
			return "<w:tc><w:tcPr><w:tcW w:w=\"" + wdt + "\" w:type=\"dxa\"/><w:vAlign w:val=\"center\"/></w:tcPr>" + Para(jc, Run(text, 20, bold), 20) + "</w:tc>";
		}

		public static void Write(string path, List<ActLog.Row> rows, string admin, bool chainOk, int brokenAt)
		{
			string machineName = Environment.MachineName;
			string t = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(Para("center", Run("WORK REPORT", 32, true), 32));
			stringBuilder.Append(Para("center", Run("on the security configuration work performed", 28, true), 28));
			stringBuilder.Append(Para(null, "", 24));
			stringBuilder.Append(Para(null, Run("Workstation: ", 24, false) + Run(machineName, 24, true), 24));
			stringBuilder.Append(Para(null, Run("Report date: ", 24, false) + Run(t, 24, false), 24));
			stringBuilder.Append(Para(null, Run("Actions recorded in the log: ", 24, false) + Run(rows.Count.ToString(), 24, false), 24));
			stringBuilder.Append(Para(null, "", 20));
			stringBuilder.Append(Para(null, Run("Below is the list of actions performed with AS Shield, as recorded in the tamper-evident action log (SHA-256 hash chain).", 24, false), 24));
			stringBuilder.Append(Para(null, "", 12));
			int[] array = new int[4] { 700, 2400, 5200, 2300 };
			StringBuilder stringBuilder2 = new StringBuilder();
			stringBuilder2.Append("<w:tbl><w:tblPr><w:tblW w:w=\"0\" w:type=\"auto\"/><w:tblBorders><w:top w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/><w:left w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/><w:bottom w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/><w:right w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/><w:insideH w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/><w:insideV w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/></w:tblBorders></w:tblPr>");
			stringBuilder2.Append("<w:tblGrid>");
			int[] array2 = array;
			foreach (int num in array2)
			{
				stringBuilder2.Append("<w:gridCol w:w=\"" + num + "\"/>");
			}
			stringBuilder2.Append("</w:tblGrid>");
			stringBuilder2.Append("<w:tr><w:trPr><w:tblHeader/></w:trPr>" + Cell("No.", array[0], true, "center") + Cell("Date and time", array[1], true, "center") + Cell("Action performed", array[2], true, "center") + Cell("Result", array[3], true, "center") + "</w:tr>");
			foreach (ActLog.Row row in rows)
			{
				stringBuilder2.Append("<w:tr>" + Cell(row.Seq, array[0], false, "center") + Cell(row.Timestamp, array[1], false, "center") + Cell(row.Details + ((!(row.User != "")) ? "" : ("  (" + row.User + ")")), array[2], false, "left") + Cell(row.Result, array[3], false, "left") + "</w:tr>");
			}
			stringBuilder2.Append("</w:tbl>");
			stringBuilder.Append((object)stringBuilder2);
			stringBuilder.Append(Para(null, "", 20));
			string t2 = ((!chainOk) ? ("WARNING: the log integrity check found a break in the hash chain at record no. " + brokenAt + ". The log may have been modified.") : "Log integrity check: the hash chain is intact - no records were changed.");
			stringBuilder.Append(Para(null, Run("Integrity check: ", 24, true) + Run(t2, 24, false), 24));
			stringBuilder.Append(Para(null, "", 28));
			StringBuilder stringBuilder3 = new StringBuilder(Run("System administrator", 24, false));
			for (int j = 0; j < 8; j++)
			{
				stringBuilder3.Append(Run("\u00a0\u00a0\u00a0\u00a0\u00a0", 24, false));
			}
			stringBuilder3.Append(Run((!string.IsNullOrWhiteSpace(admin)) ? admin.Trim() : "_____________________", 24, false));
			stringBuilder.Append(Para(null, stringBuilder3.ToString(), 24));
			stringBuilder.Append(Para(null, "", 24));
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
