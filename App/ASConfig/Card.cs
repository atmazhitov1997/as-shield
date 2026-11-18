using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ASConfig
{
	internal class Card : Control
	{
		public string Title;

		public string Desc;

		public string Icon;

		public string Badge;

		public Color Accent = Theme.Neon;

		public bool Selected;

		private bool hover;

		private bool down;

		private float h;

		private float sel;

		public bool Glow => !string.IsNullOrEmpty(Desc);

		public int GlowPad => Glow ? Theme.S(10f) : 0;

		public Card()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
			Cursor = Cursors.Hand;
			Anim.Tick += OnTick;
		}

		protected override void Dispose(bool disposing)
		{
			Anim.Tick -= OnTick;
			base.Dispose(disposing);
		}

		public void Place(Rectangle r)
		{
			int glowPad = GlowPad;
			SetBounds(r.X - glowPad, r.Y - glowPad, r.Width + glowPad * 2, r.Height + glowPad * 2);
		}

		public void Place(int x, int y, int w, int hgt)
		{
			Place(new Rectangle(x, y, w, hgt));
		}

		private void OnTick()
		{
			if (base.Visible && !base.IsDisposed)
			{
				float num = ((!hover || !base.Enabled) ? 0f : 1f);
				float num2 = ((!Selected) ? 0f : 1f);
				bool flag = false;
				if (Math.Abs(h - num) > 0.001f)
				{
					h += (float)Math.Sign(num - h) * Math.Min(0.18f, Math.Abs(num - h));
					flag = true;
				}
				if (Math.Abs(sel - num2) > 0.001f)
				{
					sel += (float)Math.Sign(num2 - sel) * Math.Min(0.2f, Math.Abs(num2 - sel));
					flag = true;
				}
				if (flag)
				{
					Invalidate();
				}
			}
		}

		protected override void OnMouseEnter(EventArgs e)
		{
			hover = true;
			base.OnMouseEnter(e);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			hover = false;
			down = false;
			Invalidate();
			base.OnMouseLeave(e);
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			down = true;
			Invalidate();
			base.OnMouseDown(e);
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			down = false;
			Invalidate();
			base.OnMouseUp(e);
		}

		protected override void OnEnabledChanged(EventArgs e)
		{
			Cursor = ((!base.Enabled) ? Cursors.Default : Cursors.Hand);
			Invalidate();
			base.OnEnabledChanged(e);
		}

		protected override void OnPaintBackground(PaintEventArgs e)
		{
			Theme.Backdrop(e.Graphics, this, e.ClipRectangle);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			Theme.Quality(graphics);
			if (Glow)
			{
				PaintCard(graphics);
			}
			else
			{
				PaintButton(graphics);
			}
			Theme.Scanline(graphics, this);
		}

		private void PaintCard(Graphics g)
		{
			bool enabled = base.Enabled;
			float num = ((!enabled) ? 0f : h);
			float num2 = GlowPad;
			float num3 = (float)Theme.S(2f) * num - (float)(down ? Theme.S(1f) : 0);
			RectangleF rectangleF = new RectangleF(num2, num2 - num3, (float)base.Width - num2 * 2f - 1f, (float)base.Height - num2 * 2f - 1f);
			float rad = Theme.S(8f);
			Theme.GlowRect(g, rectangleF, rad, Theme.Neon, Math.Max(num, sel * 0.8f));
			using (GraphicsPath path = Theme.Round(rectangleF, rad))
			{
				using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(rectangleF.X - 1f, rectangleF.Y - 1f, rectangleF.Width + 2f, rectangleF.Height + 2f), Color.FromArgb(188, 17, 30, 62), Color.FromArgb(224, 8, 14, 32), 90f))
				{
					g.FillPath(brush, path);
				}
				if (num > 0f || sel > 0f)
				{
					using (SolidBrush brush2 = new SolidBrush(Color.FromArgb((int)Math.Max(num * 10f, sel * 40f), Theme.Neon)))
					{
						g.FillPath(brush2, path);
					}
				}
				Color color = Theme.Mix(Color.FromArgb(153, Theme.Border), Color.FromArgb(128, Theme.Neon), Math.Max(num, sel));
				using (Pen pen = new Pen(color, (!(sel > 0.5f)) ? 1f : ((float)Theme.S(2f))))
				{
					g.DrawPath(pen, path);
				}
			}
			if (!Theme.Calm)
			{
				float num4 = (float)Theme.S(10f) + (float)Theme.S(6f) * Math.Max(num, sel);
				using (Pen pen2 = new Pen(Color.FromArgb((int)(140f + 110f * Math.Max(num, sel)), Theme.Neon), Theme.S(1.7f)))
				{
					LineCap startCap = (pen2.EndCap = LineCap.Round);
					pen2.StartCap = startCap;
					float x = rectangleF.X;
					float y = rectangleF.Y;
					float right = rectangleF.Right;
					float bottom = rectangleF.Bottom;
					g.DrawLine(pen2, x, y + num4, x, y);
					g.DrawLine(pen2, x, y, x + num4, y);
					g.DrawLine(pen2, right - num4, y, right, y);
					g.DrawLine(pen2, right, y, right, y + num4);
					g.DrawLine(pen2, x, bottom - num4, x, bottom);
					g.DrawLine(pen2, x, bottom, x + num4, bottom);
					g.DrawLine(pen2, right - num4, bottom, right, bottom);
					g.DrawLine(pen2, right, bottom, right, bottom - num4);
				}
			}
			Color accent = Accent;
			float num5 = rectangleF.X + (float)Theme.S(20f);
			if (!string.IsNullOrEmpty(Icon))
			{
				float num6 = Theme.S(44f);
				RectangleF rectangleF2 = new RectangleF(num5, rectangleF.Y + Math.Min(Theme.S(20f), (rectangleF.Height - num6) / 2f), num6, num6);
				if (!Theme.Calm)
				{
					Theme.GlowRect(g, rectangleF2, Theme.S(5f), accent, 0.35f + 0.5f * num, 3);
				}
				else
				{
					Theme.GlowRect(g, rectangleF2, Theme.S(4f), accent, 0.18f * num, 2);
				}
				using (GraphicsPath path2 = Theme.Round(rectangleF2, Theme.S(5f)))
				{
					using (LinearGradientBrush brush3 = new LinearGradientBrush(new RectangleF(rectangleF2.X, rectangleF2.Y - 1f, rectangleF2.Width, rectangleF2.Height + 2f), Color.FromArgb(205, Theme.Mix(Theme.Bg, accent, 0.3f)), Color.FromArgb(150, Theme.Mix(Theme.Bg, accent, 0.08f)), 90f))
					{
						g.FillPath(brush3, path2);
					}
					using (Pen pen3 = new Pen(Color.FromArgb((int)(140f + 110f * num), accent), 1.2f))
					{
						g.DrawPath(pen3, path2);
					}
				}
				using (SolidBrush brush4 = new SolidBrush((!(num > 0f)) ? accent : Theme.Mix(accent, Theme.Light(accent), num)))
				{
					StringFormat stringFormat = new StringFormat();
					stringFormat.Alignment = StringAlignment.Center;
					stringFormat.LineAlignment = StringAlignment.Center;
					using (StringFormat format = stringFormat)
					{
						g.DrawString(Fonts.Glyph(Icon, Title), Fonts.Icons(15f), brush4, rectangleF2, format);
					}
				}
				num5 += num6 + (float)Theme.S(16f);
			}
			float num7 = rectangleF.Right - (float)Theme.S(18f);
			RectangleF rectangleF3 = RectangleF.Empty;
			if (!string.IsNullOrEmpty(Badge))
			{
				Font font = Fonts.Mono(8.5f);
				SizeF sizeF = g.MeasureString(Badge, font);
				RectangleF rectangleF4 = new RectangleF(num7 - sizeF.Width - (float)Theme.S(14f), rectangleF.Y + (float)Theme.S(16f), sizeF.Width + (float)Theme.S(14f), sizeF.Height + (float)Theme.S(4f));
				using (GraphicsPath path3 = Theme.Round(rectangleF4, Theme.S(4f)))
				{
					using (SolidBrush brush5 = new SolidBrush(Color.FromArgb(26, Theme.Amber)))
					{
						g.FillPath(brush5, path3);
					}
					using (Pen pen4 = new Pen(Color.FromArgb(77, Theme.Amber)))
					{
						g.DrawPath(pen4, path3);
					}
				}
				using (SolidBrush brush6 = new SolidBrush(Color.FromArgb(251, 191, 36)))
				{
					StringFormat stringFormat = new StringFormat();
					stringFormat.Alignment = StringAlignment.Center;
					stringFormat.LineAlignment = StringAlignment.Center;
					using (StringFormat format2 = stringFormat)
					{
						g.DrawString(Badge, font, brush6, rectangleF4, format2);
					}
				}
				rectangleF3 = rectangleF4;
			}
			Font font2 = Fonts.Mono(12f, true);
			Font font3 = Fonts.Sans(9.5f);
			float height = g.MeasureString("Ag", font2).Height;
			float num8 = num7 - num5;
			float num9 = Math.Min(g.MeasureString(Desc, font3, (int)Math.Max(10f, num8)).Height, rectangleF.Height - height - (float)Theme.S(26f));
			float num10 = rectangleF.Y + Math.Max(Theme.S(16f), (rectangleF.Height - height - (float)Theme.S(4f) - num9) / 2f);
			float num11 = num8;
			if (!rectangleF3.IsEmpty && num10 < rectangleF3.Bottom)
			{
				num11 = rectangleF3.Left - num5 - (float)Theme.S(8f);
				if (g.MeasureString(Title, font2).Width > num11)
				{
					num10 = rectangleF3.Bottom + (float)Theme.S(4f);
					num11 = num8;
					num9 = Math.Min(num9, rectangleF.Bottom - num10 - height - (float)Theme.S(10f));
				}
			}
			Font font4 = font2;
			if (!string.IsNullOrEmpty(Title) && num11 > 10f)
			{
				float width = g.MeasureString(Title, font2).Width;
				if (width > num11)
				{
					float num12 = Math.Max(8.5f, 12f * (num11 / width));
					num12 = (float)(Math.Floor(num12 * 2f) / 2.0);
					font4 = Fonts.Mono(num12, true);
				}
			}
			float y2 = num10 + (height - g.MeasureString("Ag", font4).Height) / 2f;
			using (SolidBrush brush7 = new SolidBrush(Theme.Mix(Theme.White, Theme.Light(accent), num)))
			{
				using (SolidBrush brush8 = new SolidBrush(Theme.Muted))
				{
					StringFormat stringFormat = new StringFormat();
					stringFormat.Trimming = StringTrimming.EllipsisCharacter;
					using (StringFormat stringFormat2 = stringFormat)
					{
						stringFormat2.FormatFlags = StringFormatFlags.NoWrap;
						g.DrawString(Title, font4, brush7, new RectangleF(num5, y2, Math.Max(10f, num11) + (float)Theme.S(2f), height), stringFormat2);
						stringFormat2.FormatFlags = (StringFormatFlags)0;
						g.DrawString(Desc, font3, brush8, new RectangleF(num5, num10 + height + (float)Theme.S(4f), Math.Max(10f, num8), Math.Max(1f, num9)), stringFormat2);
					}
				}
			}
			if (enabled)
			{
				return;
			}
			using (GraphicsPath path4 = Theme.Round(RectangleF.Inflate(rectangleF, 1f, 1f), rad))
			{
				using (SolidBrush brush9 = new SolidBrush(Color.FromArgb(150, Theme.Bg)))
				{
					g.FillPath(brush9, path4);
				}
			}
		}

		private void PaintButton(Graphics g)
		{
			bool enabled = base.Enabled;
			float val = ((!enabled) ? 0f : h);
			RectangleF r = new RectangleF(0.5f, 0.5f, (float)base.Width - 1.5f, (float)base.Height - 1.5f);
			float rad = Theme.S(4f);
			Color accent = Accent;
			float num = Math.Max(val, sel);
			using (GraphicsPath path = Theme.Round(r, rad))
			{
				Color color = Theme.Mix(Color.FromArgb(220, 10, 16, 32), Color.FromArgb(220, Theme.Mix(Theme.Bg, accent, 0.22f)), num);
				if (down && enabled)
				{
					color = Color.FromArgb(230, Theme.Mix(Theme.Bg, accent, 0.3f));
				}
				using (SolidBrush brush = new SolidBrush(color))
				{
					g.FillPath(brush, path);
				}
				using (Pen pen = new Pen(Color.FromArgb((int)((!enabled) ? 50f : (110f + 140f * num)), accent), (!(sel > 0.5f)) ? 1f : ((float)Theme.S(1.5f))))
				{
					g.DrawPath(pen, path);
				}
			}
			if (sel > 0.01f)
			{
				using (SolidBrush brush2 = new SolidBrush(Color.FromArgb((int)(255f * sel), accent)))
				{
					g.FillRectangle(brush2, r.X + 1f, r.Y + (float)Theme.S(8f), Theme.S(3f), r.Height - (float)Theme.S(16f));
				}
			}
			Color color2 = ((!enabled) ? Color.FromArgb(90, Theme.Muted) : Theme.Mix(Theme.Light(accent), Color.White, num * 0.5f));
			float num2 = Theme.S(16f);
			bool flag = string.IsNullOrEmpty(Title);
			if (!string.IsNullOrEmpty(Icon))
			{
				float num3 = Theme.S(18f);
				RectangleF layoutRectangle = ((!flag) ? new RectangleF(num2, ((float)base.Height - num3) / 2f, num3, num3) : new RectangleF(((float)base.Width - num3) / 2f, ((float)base.Height - num3) / 2f, num3, num3));
				using (SolidBrush brush3 = new SolidBrush((!enabled) ? color2 : accent))
				{
					StringFormat stringFormat = new StringFormat();
					stringFormat.Alignment = StringAlignment.Center;
					stringFormat.LineAlignment = StringAlignment.Center;
					using (StringFormat format = stringFormat)
					{
						g.DrawString(Fonts.Glyph(Icon, Text), Fonts.Icons(11f), brush3, layoutRectangle, format);
					}
				}
				num2 += num3 + (float)Theme.S(10f);
			}
			float num4 = base.Width - Theme.S(14f);
			if (!string.IsNullOrEmpty(Badge))
			{
				Font font = Fonts.Mono(8f);
				SizeF sizeF = g.MeasureString(Badge, font);
				RectangleF rectangleF = new RectangleF(num4 - sizeF.Width - (float)Theme.S(10f), ((float)base.Height - sizeF.Height - (float)Theme.S(4f)) / 2f, sizeF.Width + (float)Theme.S(10f), sizeF.Height + (float)Theme.S(4f));
				using (GraphicsPath path2 = Theme.Round(rectangleF, Theme.S(3f)))
				{
					using (SolidBrush brush4 = new SolidBrush(Color.FromArgb(30, Theme.Amber)))
					{
						g.FillPath(brush4, path2);
					}
				}
				using (SolidBrush brush5 = new SolidBrush(Theme.Amber))
				{
					StringFormat stringFormat = new StringFormat();
					stringFormat.Alignment = StringAlignment.Center;
					stringFormat.LineAlignment = StringAlignment.Center;
					using (StringFormat format2 = stringFormat)
					{
						g.DrawString(Badge, font, brush5, rectangleF, format2);
					}
				}
				num4 = rectangleF.Left - (float)Theme.S(6f);
			}
			if (flag)
			{
				return;
			}
			using (SolidBrush brush6 = new SolidBrush(color2))
			{
				StringFormat stringFormat = new StringFormat();
				stringFormat.LineAlignment = StringAlignment.Center;
				stringFormat.Trimming = StringTrimming.EllipsisCharacter;
				stringFormat.FormatFlags = StringFormatFlags.NoWrap;
				using (StringFormat format3 = stringFormat)
				{
					g.DrawString(Title, Fonts.Mono(9.5f, num > 0.5f || sel > 0.5f), brush6, new RectangleF(num2, 0f, Math.Max(10f, num4 - num2), base.Height), format3);
				}
			}
		}
	}
}
