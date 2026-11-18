using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace ASConfig
{
	internal static class Theme
	{
		public static readonly Color Bg = Color.FromArgb(4, 7, 17);

		public static readonly Color Header = Color.FromArgb(11, 19, 41);

		public static readonly Color Card = Color.FromArgb(15, 27, 56);

		public static readonly Color CardHov = Color.FromArgb(20, 38, 74);

		public static readonly Color Border = Color.FromArgb(30, 58, 95);

		public static readonly Color White = Color.FromArgb(248, 250, 252);

		public static readonly Color Text = Color.FromArgb(203, 213, 225);

		public static readonly Color Muted = Color.FromArgb(148, 163, 184);

		public static readonly Color Dim = Color.FromArgb(100, 116, 139);

		public static readonly Color Neon = Color.FromArgb(0, 240, 255);

		public static readonly Color CyanDeep = Color.FromArgb(8, 51, 68);

		public static readonly Color CyanLine = Color.FromArgb(22, 78, 99);

		public static readonly Color Green = Color.FromArgb(16, 185, 129);

		public static readonly Color Amber = Color.FromArgb(245, 158, 11);

		public static readonly Color Red = Color.FromArgb(244, 63, 94);

		public static readonly Color Violet = Color.FromArgb(167, 139, 250);

		public static readonly Color Teal = Color.FromArgb(45, 212, 191);

		public static readonly Color Blue = Neon;

		public static readonly Color UaYel = Amber;

		public static readonly Color UaBlue = CyanDeep;

		public static float Scale = 1f;

		public static bool Calm = false;

		public static bool Lite = false;

		private static readonly SolidBrush bgBrush = new SolidBrush(Bg);

		private static readonly Pen gridPen = new Pen(Color.FromArgb(11, 19, 40));

		public static int S(float v)
		{
			return (int)Math.Round(v * Scale);
		}

		public static GraphicsPath Round(RectangleF r, float rad)
		{
			GraphicsPath graphicsPath = new GraphicsPath();
			if (rad <= 0.5f)
			{
				graphicsPath.AddRectangle(r);
				return graphicsPath;
			}
			float num = Math.Min(rad * 2f, Math.Min(r.Width, r.Height));
			graphicsPath.AddArc(r.X, r.Y, num, num, 180f, 90f);
			graphicsPath.AddArc(r.Right - num, r.Y, num, num, 270f, 90f);
			graphicsPath.AddArc(r.Right - num, r.Bottom - num, num, num, 0f, 90f);
			graphicsPath.AddArc(r.X, r.Bottom - num, num, num, 90f, 90f);
			graphicsPath.CloseFigure();
			return graphicsPath;
		}

		public static Color Mix(Color a, Color b, float t)
		{
			t = Math.Max(0f, Math.Min(1f, t));
			return Color.FromArgb((int)((float)(int)a.A + (float)(b.A - a.A) * t), (int)((float)(int)a.R + (float)(b.R - a.R) * t), (int)((float)(int)a.G + (float)(b.G - a.G) * t), (int)((float)(int)a.B + (float)(b.B - a.B) * t));
		}

		public static Color A(Color c, float alpha)
		{
			return Color.FromArgb(Math.Max(0, Math.Min(255, (int)(alpha * 255f))), c);
		}

		public static Color Light(Color c)
		{
			return Mix(c, Color.White, 0.35f);
		}

		public static void Quality(Graphics g)
		{
			g.SmoothingMode = SmoothingMode.AntiAlias;
			g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
			g.PixelOffsetMode = PixelOffsetMode.HighQuality;
		}

		public static Point FormOffset(Control c)
		{
			Form form = c.FindForm();
			if (form == null || !c.IsHandleCreated)
			{
				return Point.Empty;
			}
			return form.PointToClient(c.PointToScreen(Point.Empty));
		}

		public static Size FormSize(Control c)
		{
			return c.FindForm()?.ClientSize ?? c.Size;
		}

		public static void Backdrop(Graphics g, Control c, Rectangle clip)
		{
			Point point = FormOffset(c);
			Size size = FormSize(c);
			g.FillRectangle(bgBrush, clip);
			RectangleF rectangleF = new RectangleF(clip.X, clip.Y, clip.Width, clip.Height);
			if (!Calm)
			{
				float num = (float)size.Width * 0.55f;
				float num2 = Math.Max((float)size.Height * 0.6f, S(300f));
				RectangleF rectangleF2 = new RectangleF((float)size.Width / 2f - num - (float)point.X, 0f - num2 - (float)point.Y, num * 2f, num2 * 2f);
				if (rectangleF2.IntersectsWith(rectangleF))
				{
					using (GraphicsPath graphicsPath = new GraphicsPath())
					{
						graphicsPath.AddEllipse(rectangleF2);
						using (PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath))
						{
							pathGradientBrush.CenterColor = Color.FromArgb(26, 0, 240, 255);
							pathGradientBrush.SurroundColors = new Color[1] { Color.FromArgb(0, 0, 240, 255) };
							pathGradientBrush.FocusScales = new PointF(0.05f, 0.05f);
							SmoothingMode smoothingMode = g.SmoothingMode;
							g.SmoothingMode = SmoothingMode.None;
							g.FillRectangle(pathGradientBrush, RectangleF.Intersect(rectangleF2, rectangleF));
							g.SmoothingMode = smoothingMode;
						}
					}
				}
			}
			int num3 = S((!Calm) ? 32 : 40);
			SmoothingMode smoothingMode2 = g.SmoothingMode;
			g.SmoothingMode = SmoothingMode.None;
			using (Pen pen = ((!Calm) ? null : new Pen(Color.FromArgb(9, 120, 150, 190))))
			{
				Pen pen2 = pen ?? gridPen;
				int num4 = clip.Left - ((clip.Left + point.X) % num3 + num3) % num3;
				for (int i = num4; i <= clip.Right; i += num3)
				{
					g.DrawLine(pen2, i, clip.Top, i, clip.Bottom);
				}
				int num5 = clip.Top - ((clip.Top + point.Y) % num3 + num3) % num3;
				for (int j = num5; j <= clip.Bottom; j += num3)
				{
					g.DrawLine(pen2, clip.Left, j, clip.Right, j);
				}
			}
			g.SmoothingMode = smoothingMode2;
		}

		public static void Scanline(Graphics g, Control c)
		{
			if (Calm || Anim.ScanAlpha <= 0.01f)
			{
				return;
			}
			Point point = FormOffset(c);
			float num = Anim.ScanY - (float)point.Y;
			if (num < (float)(-S(24f)) || num > (float)(c.Height + 2))
			{
				return;
			}
			Size size = FormSize(c);
			RectangleF rect = new RectangleF(-point.X, num, size.Width, S(2f));
			ColorBlend colorBlend = new ColorBlend();
			colorBlend.Colors = new Color[3]
			{
				Color.FromArgb(0, 0, 240, 255),
				Color.FromArgb((int)(110f * Anim.ScanAlpha), 0, 240, 255),
				Color.FromArgb(0, 0, 240, 255)
			};
			colorBlend.Positions = new float[3] { 0f, 0.5f, 1f };
			ColorBlend interpolationColors = colorBlend;
			LinearGradientBrush linearGradientBrush = new LinearGradientBrush(new RectangleF(rect.X - 1f, rect.Y, rect.Width + 2f, rect.Height), Color.Black, Color.Black, 0f);
			linearGradientBrush.InterpolationColors = interpolationColors;
			using (LinearGradientBrush brush = linearGradientBrush)
			{
				g.FillRectangle(brush, rect);
			}
			RectangleF rect2 = new RectangleF(-point.X, num - (float)S(22f), size.Width, S(22f));
			using (LinearGradientBrush brush2 = new LinearGradientBrush(new RectangleF(rect2.X, rect2.Y - 1f, rect2.Width, rect2.Height + 2f), Color.FromArgb(0, 0, 240, 255), Color.FromArgb((int)(14f * Anim.ScanAlpha), 0, 240, 255), 90f))
			{
				g.FillRectangle(brush2, rect2);
			}
		}

		public static void RadarCorner(Graphics g, Control c)
		{
			if (Calm)
			{
				return;
			}
			Quality(g);
			float num = c.Width - S(12f);
			float num2 = S(8f);
			using (Pen pen = new Pen(Color.FromArgb(22, Neon), 1f))
			{
				for (int i = 1; i <= 5; i++)
				{
					float num3 = i * S(66f);
					g.DrawEllipse(pen, num - num3, num2 - num3, num3 * 2f, num3 * 2f);
				}
			}
			using (Pen pen2 = new Pen(Color.FromArgb(16, Neon), 1f))
			{
				float num4 = S(330f);
				for (int j = 0; j <= 90; j += 15)
				{
					double num5 = (double)j * Math.PI / 180.0;
					g.DrawLine(pen2, num, num2, num - (float)(Math.Sin(num5) * (double)num4), num2 + (float)(Math.Cos(num5) * (double)num4));
				}
			}
			using (GraphicsPath graphicsPath = new GraphicsPath())
			{
				float num6 = S(320f);
				graphicsPath.AddPie(num - num6, num2 - num6, num6 * 2f, num6 * 2f, 95f, 42f);
				PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath);
				pathGradientBrush.CenterColor = Color.FromArgb(26, Neon);
				pathGradientBrush.SurroundColors = new Color[1] { Color.FromArgb(0, Neon) };
				using (PathGradientBrush brush = pathGradientBrush)
				{
					g.FillPath(brush, graphicsPath);
				}
			}
		}

		public static void GlowRect(Graphics g, RectangleF r, float rad, Color c, float strength, int rings = 6)
		{
			if (strength <= 0.01f)
			{
				return;
			}
			for (int num = rings; num >= 1; num--)
			{
				RectangleF r2 = RectangleF.Inflate(r, num * S(1.6f), num * S(1.6f));
				using (GraphicsPath path = Round(r2, rad + (float)(num * S(1.6f))))
				{
					using (Pen pen = new Pen(Color.FromArgb((int)(strength * 22f * (1f - (float)(num - 1) / (float)rings)), c), S(2.2f)))
					{
						g.DrawPath(pen, path);
					}
				}
			}
		}

		private static void Spike(GraphicsPath p, float ox, float oy, float sc, float tx, float ty, float lx, float ly, float rx, float ry)
		{
			p.AddPolygon(new PointF[3]
			{
				new PointF(ox + tx * sc, oy + ty * sc),
				new PointF(ox + rx * sc, oy + ry * sc),
				new PointF(ox + lx * sc, oy + ly * sc)
			});
		}

		public static void DrawLogo(Graphics g, RectangleF box)
		{
			float sc = box.Width / 256f;
			float ox = box.X;
			float oy = box.Y;
			Func<float, float, PointF> func = (float x, float y) => new PointF(ox + x * sc, oy + y * sc);
			using (GraphicsPath graphicsPath = new GraphicsPath())
			{
				graphicsPath.AddLine(func(128f, 18f), func(222f, 54f));
				graphicsPath.AddLine(func(222f, 54f), func(222f, 132f));
				graphicsPath.AddBezier(func(222f, 132f), func(222f, 190f), func(180f, 224f), func(128f, 242f));
				graphicsPath.AddBezier(func(128f, 242f), func(76f, 224f), func(34f, 190f), func(34f, 132f));
				graphicsPath.AddLine(func(34f, 132f), func(34f, 54f));
				graphicsPath.CloseFigure();
				for (int num = 5; num >= 1; num--)
				{
					using (Pen pen = new Pen(Color.FromArgb(16, Neon), sc * 9f + (float)num * sc * 3f))
					{
						pen.LineJoin = LineJoin.Round;
						g.DrawPath(pen, graphicsPath);
					}
				}
				using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(box.X, box.Y - 1f, box.Width, box.Height + 2f), Color.FromArgb(12, 39, 64), Color.FromArgb(6, 16, 31), 90f))
				{
					g.FillPath(brush, graphicsPath);
				}
				using (Pen pen2 = new Pen(Neon, sc * 9f))
				{
					pen2.LineJoin = LineJoin.Round;
					g.DrawPath(pen2, graphicsPath);
				}
				using (Pen pen3 = new Pen(Color.FromArgb(120, 14, 127, 160), Math.Max(1f, sc * 2f)))
				{
					using (GraphicsPath graphicsPath2 = new GraphicsPath())
					{
						graphicsPath2.AddLine(func(128f, 34f), func(208f, 65f));
						graphicsPath2.AddLine(func(208f, 65f), func(208f, 130f));
						graphicsPath2.AddBezier(func(208f, 130f), func(208f, 180f), func(174f, 208f), func(128f, 224f));
						graphicsPath2.AddBezier(func(128f, 224f), func(82f, 208f), func(48f, 180f), func(48f, 130f));
						graphicsPath2.AddLine(func(48f, 130f), func(48f, 65f));
						graphicsPath2.CloseFigure();
						g.DrawPath(pen3, graphicsPath2);
					}
				}
			}
			using (GraphicsPath graphicsPath3 = new GraphicsPath())
			{
				graphicsPath3.AddRectangle(new RectangleF(ox + 86f * sc, oy + 150f * sc, 84f * sc, 12f * sc));
				graphicsPath3.AddRectangle(new RectangleF(ox + 122f * sc, oy + 150f * sc, 12f * sc, 60f * sc));
				Spike(graphicsPath3, ox, oy, sc, 128f, 58f, 120f, 150f, 136f, 150f);
				Spike(graphicsPath3, ox, oy, sc, 94f, 78f, 86f, 152f, 102f, 152f);
				Spike(graphicsPath3, ox, oy, sc, 162f, 78f, 154f, 152f, 170f, 152f);
				Spike(graphicsPath3, ox, oy, sc, 110f, 96f, 104f, 150f, 116f, 150f);
				Spike(graphicsPath3, ox, oy, sc, 146f, 96f, 140f, 150f, 152f, 150f);
				for (int num2 = 4; num2 >= 1; num2--)
				{
					using (Pen pen4 = new Pen(Color.FromArgb(14, Neon), (float)num2 * sc * 3f))
					{
						g.DrawPath(pen4, graphicsPath3);
					}
				}
				using (LinearGradientBrush brush2 = new LinearGradientBrush(new RectangleF(box.X, box.Y - 1f, box.Width, box.Height + 2f), Color.FromArgb(93, 243, 255), Color.FromArgb(0, 200, 255), 90f))
				{
					g.FillPath(brush2, graphicsPath3);
				}
			}
			using (Pen pen5 = new Pen(Color.FromArgb(93, 243, 255), sc * 7f))
			{
				g.DrawEllipse(pen5, ox + 117f * sc, oy + 203f * sc, 22f * sc, 22f * sc);
			}
		}

		public static void GlowDot(Graphics g, float cx, float cy, float r, Color c, float alpha)
		{
			for (int num = 4; num >= 1; num--)
			{
				using (SolidBrush brush = new SolidBrush(Color.FromArgb((int)(alpha * 30f / (float)num), c)))
				{
					g.FillEllipse(brush, cx - r - (float)(num * S(2f)), cy - r - (float)(num * S(2f)), (r + (float)(num * S(2f))) * 2f, (r + (float)(num * S(2f))) * 2f);
				}
			}
			using (SolidBrush brush2 = new SolidBrush(Color.FromArgb((int)(255f * Math.Max(0.35f, alpha)), c)))
			{
				g.FillEllipse(brush2, cx - r, cy - r, r * 2f, r * 2f);
			}
		}
	}
}
