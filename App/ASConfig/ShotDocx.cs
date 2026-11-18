using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace ASConfig
{
	internal static class ShotDocx
	{
		private const long MaxW = 6120765L;

		private static string Run(string t)
		{
			return "<w:r><w:rPr><w:rFonts w:ascii=\"Times New Roman\" w:hAnsi=\"Times New Roman\" w:cs=\"Times New Roman\"/><w:sz w:val=\"28\"/><w:szCs w:val=\"28\"/></w:rPr>" + ((!(t == "\t")) ? ("<w:t xml:space=\"preserve\">" + SecurityElement.Escape(t) + "</w:t>") : "<w:tab/>") + "</w:r>";
		}

		private static string Para(string jc, string runs)
		{
			return "<w:p><w:pPr>" + ((jc == null) ? "" : ("<w:jc w:val=\"" + jc + "\"/>")) + "<w:rPr><w:rFonts w:ascii=\"Times New Roman\" w:hAnsi=\"Times New Roman\" w:cs=\"Times New Roman\"/><w:sz w:val=\"28\"/><w:szCs w:val=\"28\"/></w:rPr></w:pPr>" + runs + "</w:p>";
		}

		public static void Write(string path, ShotItem item, List<string> images, string admin)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(Para("right", Run("Screenshot " + item.N)));
			stringBuilder.Append(Para("center", ""));
			stringBuilder.Append(Para("center", Run("“" + item.Title + "”")));
			long num = ((images.Count <= 1) ? 8200000 : 4000000);
			for (int i = 0; i < images.Count; i++)
			{
				int width;
				int height;
				using (Image image = Image.FromFile(images[i]))
				{
					width = image.Width;
					height = image.Height;
				}
				long num2 = 6120765L;
				long num3 = (long)(6120765.0 * (double)height / (double)width);
				if (num3 > num)
				{
					num2 = (long)((double)num2 * (double)num / (double)num3);
					num3 = num;
				}
				int num4 = i + 1;
				string runs = "<w:r><w:drawing><wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\"><wp:extent cx=\"" + num2 + "\" cy=\"" + num3 + "\"/><wp:docPr id=\"" + num4 + "\" name=\"Picture " + num4 + "\"/><wp:cNvGraphicFramePr><a:graphicFrameLocks noChangeAspect=\"1\"/></wp:cNvGraphicFramePr><a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><pic:pic><pic:nvPicPr><pic:cNvPr id=\"" + num4 + "\" name=\"image" + num4 + "\"/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill><a:blip r:embed=\"rImg" + num4 + "\"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill><pic:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"" + num2 + "\" cy=\"" + num3 + "\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r>";
				stringBuilder.Append(Para("center", runs));
			}
			stringBuilder.Append(Para(null, ""));
			StringBuilder stringBuilder2 = new StringBuilder(Run("System administrator"));
			for (int j = 0; j < 6; j++)
			{
				stringBuilder2.Append(Run("\t"));
			}
			stringBuilder2.Append(Run((!string.IsNullOrWhiteSpace(admin)) ? admin.Trim() : "____________________"));
			stringBuilder.Append(Para(null, stringBuilder2.ToString()));
			string text = string.Concat("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><w:body>", stringBuilder, "<w:sectPr><w:pgSz w:w=\"11906\" w:h=\"16838\"/><w:pgMar w:top=\"850\" w:right=\"850\" w:bottom=\"850\" w:left=\"1417\" w:header=\"708\" w:footer=\"708\" w:gutter=\"0\"/></w:sectPr></w:body></w:document>");
			StringBuilder stringBuilder3 = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
			for (int k = 0; k < images.Count; k++)
			{
				stringBuilder3.Append("<Relationship Id=\"rImg" + (k + 1) + "\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\" Target=\"media/image" + (k + 1) + Ext(images[k]) + "\"/>");
			}
			stringBuilder3.Append("</Relationships>");
			string text2 = path + ".tmp";
			if (File.Exists(text2))
			{
				File.Delete(text2);
			}
			using (FileStream stream = File.Create(text2))
			{
				using (ZipArchive zipArchive = new ZipArchive(stream, ZipArchiveMode.Create))
				{
					Entry(zipArchive, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Default Extension=\"png\" ContentType=\"image/png\"/><Default Extension=\"jpg\" ContentType=\"image/jpeg\"/><Default Extension=\"jpeg\" ContentType=\"image/jpeg\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
					Entry(zipArchive, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
					Entry(zipArchive, "word/document.xml", text);
					Entry(zipArchive, "word/_rels/document.xml.rels", stringBuilder3.ToString());
					for (int l = 0; l < images.Count; l++)
					{
						ZipArchiveEntry zipArchiveEntry = zipArchive.CreateEntry("word/media/image" + (l + 1) + Ext(images[l]), CompressionLevel.NoCompression);
						using (Stream destination = zipArchiveEntry.Open())
						{
							using (FileStream fileStream = File.OpenRead(images[l]))
							{
								fileStream.CopyTo(destination);
							}
						}
					}
				}
			}
			if (File.Exists(path))
			{
				File.Delete(path);
			}
			File.Move(text2, path);
		}

		private static string Ext(string p)
		{
			string text = Path.GetExtension(p).ToLowerInvariant();
			return (!(text == ".jpeg")) ? text : ".jpg";
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
