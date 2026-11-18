using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ASConfig
{
	internal class MainForm : Form, IMessageFilter
	{
		private struct MEMORYSTATUSEX
		{
			public uint dwLength;

			public uint dwMemoryLoad;

			public ulong ullTotalPhys;

			public ulong ullAvailPhys;

			public ulong ullTotalPageFile;

			public ulong ullAvailPageFile;

			public ulong ullTotalVirtual;

			public ulong ullAvailVirtual;

			public ulong ullAvailExtendedVirtual;
		}

		private int scrollY;

		private int contentH;

		private int viewH;

		private int scrollMax;

		private bool sbScrollable;

		private bool sbDrag;

		private int sbGrab;

		private Rectangle sbThumb;

		private Rectangle sbTrack;

		private readonly string workDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ASShield");

		private readonly int build;

		private readonly string osName;

		private readonly bool w11;

		private readonly string sessionStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

		private readonly List<Card> actions = new List<Card>();

		// v2: availability of each card (taking the OS into account) and its catalog description
		private readonly Dictionary<Card, bool> baseEnabled = new Dictionary<Card, bool>();

		private readonly Dictionary<string, CardDef> cardDefs = new Dictionary<string, CardDef>(StringComparer.Ordinal);

		private readonly List<string> catalogWarnings = new List<string>();

		private readonly List<Card> asCards = new List<Card>();

		private readonly List<Card> extraCards = new List<Card>();

		private readonly List<Card> toolCards = new List<Card>();

		private readonly List<Card> controlCards = new List<Card>();

		private readonly Canvas header = new Canvas();

		private readonly Canvas body = new Canvas();

		private readonly Canvas footer = new Canvas();

		private readonly Card exitCard;

		private readonly Card screenCard;

		private readonly Card customCard;

		private readonly Card shotsCard;

		private readonly Card liteCard;

		private readonly ShotsView shotsView;


		private readonly RunView runView;

		private Control beforeRun;

		private string status = "Choose an action.";

		private Color statusColor = Theme.Muted;

		private bool busy;

		private Rectangle sec1;

		private Rectangle sec2;

		private Rectangle sec3;

		private Rectangle sec4;

		private Rectangle sec5;

		private Rectangle logoRect;

		private Rectangle pingRect;

		private Rectangle footDotRect;

		private float lastScan;

		private string runTitle = "";

		private string lastAction = "";

		[CompilerGenerated]
		private static Dictionary<string, int> _003C_003Ef__switch_0024map0;

		public string OsShort => (!w11) ? "10" : "11";

		public bool Busy => busy;

		private static string LiteFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ASShield", "ui.ini");

		public MainForm()
		{
			Theme.Lite = ResolveLite();
			Theme.Calm = Theme.Lite || Environment.GetEnvironmentVariable("ASCALM") != null;
			Anim.Configure(Theme.Lite);
			using (Graphics graphics = CreateGraphics())
			{
				Theme.Scale = graphics.DpiX / 96f;
			}
			build = GetBuild();
			w11 = build >= 22000;
			osName = (w11 ? "Windows 11" : ((build < 10240) ? "Not supported" : "Windows 10"));
			Text = "AS Shield · ver." + SelfInstall.AppVersion;
			BackColor = Theme.Bg;
			ForeColor = Theme.Text;
			Font = new Font("Segoe UI", 9.5f);
			base.StartPosition = FormStartPosition.CenterScreen;
			Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
			MinimumSize = new Size(Math.Min(Theme.S(980f), workingArea.Width), Math.Min(Theme.S(720f), workingArea.Height));
			base.Size = new Size(Math.Min(Theme.S(1200f), workingArea.Width), Math.Min(Theme.S(820f), workingArea.Height));
			base.KeyPreview = true;
			try
			{
				base.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
			}
			catch
			{
			}
			header.Paint += PaintHeader;
			body.Paint += PaintBody;
			body.ExtraBg = delegate(Graphics g)
			{
				Theme.RadarCorner(g, body);
			};
			body.MouseDown += BodyMouseDown;
			body.MouseMove += BodyMouseMove;
			body.MouseUp += BodyMouseUp;
			Application.AddMessageFilter(this);
			footer.Paint += PaintFooter;
			base.Controls.Add(body);
			base.Controls.Add(header);
			base.Controls.Add(footer);
			bool enabled = build >= 10240;
			if (!BuildCardsFromCatalog())
			{
				catalogWarnings.Add("Catalog/menu.json is missing from the payload - the menu is empty");
			}
			foreach (Card asCard in asCards)
			{
				body.Controls.Add(asCard);
			}
			foreach (Card extraCard in extraCards)
			{
				body.Controls.Add(extraCard);
			}
			foreach (Card toolCard in toolCards)
			{
				body.Controls.Add(toolCard);
			}
			foreach (Card controlCard in controlCards)
			{
				body.Controls.Add(controlCard);
			}
			customCard = new Card
			{
				Title = "Custom profiles",
				Desc = "Opens the folder for your own profiles (%ProgramData%\\ASShield\\Profiles). A profile can inherit a built-in one and replace or remove its steps. See README.",
				Icon = "\ue70f",
				Accent = Theme.Amber
			};
			customCard.Click += delegate
			{
				OpenLocalProfiles();
			};
			body.Controls.Add(customCard);
			shotsCard = new Card
			{
				Title = "Evidence screenshots",
				Desc = "Step-by-step screenshots for your report (secpol, event logs, BitLocker, dxdiag…) laid out in Word. Choose which items to capture; F9 captures. Saved on the desktop.",
				Icon = "\ue722",
				Accent = Theme.Neon
			};
			shotsCard.Click += delegate
			{
				ShowShots();
			};
			body.Controls.Add(shotsCard);
			shotsView = new ShotsView(this, workDir)
			{
				Visible = false
			};
			base.Controls.Add(shotsView);
			runView = new RunView(this)
			{
				Visible = false
			};
			base.Controls.Add(runView);
			runView.Finished += delegate(int code)
			{
				busy = false;
				SetStatus("'" + runTitle + "' finished" + ((code != 0) ? (" (code " + code + ").") : "."), (code != 0) ? Theme.Amber : Theme.Green, false);
				RestoreEnabled();
			};
			screenCard = new Card
			{
				Title = "Full screen · F11",
				Icon = "\ue740",
				Accent = Theme.Neon
			};
			screenCard.Click += delegate
			{
				ToggleFullscreen();
			};
			exitCard = new Card
			{
				Title = "Exit",
				Icon = "\ue711",
				Accent = Theme.Red
			};
			exitCard.Click += delegate
			{
				Close();
			};
			liteCard = new Card
			{
				Title = ((!Theme.Lite) ? "Lite: off" : "Lite: on"),
				Icon = "\ue945",
				Accent = ((!Theme.Lite) ? Theme.Muted : Theme.Teal)
			};
			liteCard.Click += delegate
			{
				ToggleLite();
			};
			footer.Controls.Add(liteCard);
			footer.Controls.Add(screenCard);
			footer.Controls.Add(exitCard);
			base.Resize += delegate
			{
				DoLayout();
			};
			Anim.Tick += OnAnim;
			base.KeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.KeyCode == Keys.F11)
				{
					ToggleFullscreen();
				}
				else if (e.KeyCode == Keys.Escape && base.FormBorderStyle == FormBorderStyle.None && body.Visible)
				{
					ToggleFullscreen();
				}
			};
			base.Load += delegate
			{
				SetFullscreen(true);
				DoLayout();
			};
			base.Shown += delegate
			{
				Prepare();
				PreviewHook();
			};
			base.FormClosing += delegate(object s, FormClosingEventArgs e)
			{
				if (!runView.ConfirmClose())
				{
					e.Cancel = true;
				}
			};
		}

		private Card MakeAction(string title, string desc, string icon, Color accent, string action, bool enabled, string badge)
		{
			Card card = new Card();
			card.Title = title;
			card.Desc = desc;
			card.Icon = icon;
			card.Accent = accent;
			card.Tag = action;
			card.Enabled = enabled;
			card.Badge = badge;
			Card card2 = card;
			card2.Click += OnAction;
			actions.Add(card2);
			baseEnabled[card2] = enabled;
			return card2;
		}

		private void SetFullscreen(bool on)
		{
			SuspendLayout();
			if (on)
			{
				base.WindowState = FormWindowState.Normal;
				base.FormBorderStyle = FormBorderStyle.None;
				base.WindowState = FormWindowState.Maximized;
			}
			else
			{
				base.FormBorderStyle = FormBorderStyle.Sizable;
				base.WindowState = FormWindowState.Normal;
				base.WindowState = FormWindowState.Maximized;
			}
			screenCard.Title = ((!on) ? "Full screen · F11" : "Windowed · F11");
			screenCard.Invalidate();
			ResumeLayout();
			DoLayout();
		}

		private void ToggleFullscreen()
		{
			SetFullscreen(base.FormBorderStyle != FormBorderStyle.None);
		}

		private void ToggleLite()
		{
			Theme.Lite = !Theme.Lite;
			Theme.Calm = Theme.Lite || Environment.GetEnvironmentVariable("ASCALM") != null;
			Anim.Configure(Theme.Lite);
			SaveLite(Theme.Lite);
			liteCard.Title = ((!Theme.Lite) ? "Lite: off" : "Lite: on");
			liteCard.Accent = ((!Theme.Lite) ? Theme.Muted : Theme.Teal);
			try
			{
				header.InvalidateBg();
				body.InvalidateBg();
				footer.InvalidateBg();
			}
			catch
			{
			}
			DoLayout();
			header.Invalidate();
			body.Invalidate();
			footer.Invalidate();
			SetStatus((!Theme.Lite) ? "Lite mode off — full visuals." : "Lite mode on — effects and animation disabled (for low-end PCs).", Theme.Teal, false);
		}

		private void DoLayout()
		{
			int width = base.ClientSize.Width;
			int height = base.ClientSize.Height;
			if (width > 0 && height > 0)
			{
				int num = Theme.S(118f);
				int num2 = Theme.S(76f);
				Anim.FormHeight = height;
				header.InvalidateBg();
				body.InvalidateBg();
				footer.InvalidateBg();
				header.SetBounds(0, 0, width, num);
				footer.SetBounds(0, height - num2, width, num2);
				body.SetBounds(0, num, width, height - num - num2);
				shotsView.SetBounds(0, num, width, height - num - num2);
				shotsView.DoLayout();
				runView.SetBounds(0, num, width, height - num - num2);
				runView.DoLayout();
				viewH = body.ClientSize.Height;
				int num3 = viewH;
				int num4 = Theme.S(16f);
				int num5 = Theme.S(28f);
				int num6 = Theme.S(8f);
				int num7 = Theme.S(18f);
				int num8 = 5 * (num5 + num6) + 4 * num7 + num4;
				int num9 = Theme.S(98f);
				int num10 = (asCards.Count + 1) / 2;
				int num11 = num8 + num9 * (num10 + 4);
				int num12 = Theme.S(18f);
				int num13 = Theme.S(18f);
				contentH = num12 + num11 + num13;
				sbScrollable = contentH > num3;
				scrollMax = (sbScrollable ? (contentH - num3) : 0);
				if (scrollY > scrollMax)
				{
					scrollY = scrollMax;
				}
				if (scrollY < 0)
				{
					scrollY = 0;
				}
				int num14 = (sbScrollable ? Theme.S(12f) : 0);
				int num15 = body.ClientSize.Width - num14;
				int num16 = Math.Min(num15 - Theme.S(80f), Theme.S(1240f));
				int num17 = (num15 - num16) / 2;
				int num18 = ((!sbScrollable) ? Math.Max(Theme.S(12f), (num3 - num11) / 2) : num12);
				int num19 = num18 - scrollY;
				int num20 = (num16 - num4) / 2;
				sec1 = new Rectangle(num17, num19, num16, num5);
				num19 += num5 + num6;
				for (int i = 0; i < asCards.Count; i++)
				{
					asCards[i].Place(num17 + i % 2 * (num20 + num4), num19 + i / 2 * (num9 + num4), num20, num9);
				}
				num19 += num9 * num10 + num4 * (num10 - 1) + num7;
				sec2 = new Rectangle(num17, num19, num16, num5);
				num19 += num5 + num6;
				int num21 = Math.Max(1, toolCards.Count);
				int num22 = (num16 - num4 * (num21 - 1)) / num21;
				for (int j = 0; j < toolCards.Count; j++)
				{
					toolCards[j].Place(num17 + j * (num22 + num4), num19, num22, num9);
				}
				num19 += num9 + num7;
				sec3 = new Rectangle(num17, num19, num16, num5);
				num19 += num5 + num6;
				int num23 = Math.Max(1, extraCards.Count);
				int num24 = (num16 - num4 * (num23 - 1)) / num23;
				for (int k = 0; k < extraCards.Count; k++)
				{
					extraCards[k].Place(num17 + k * (num24 + num4), num19, num24, num9);
				}
				num19 += num9 + num7;
				sec4 = new Rectangle(num17, num19, num16, num5);
				num19 += num5 + num6;
				customCard.Place(num17, num19, num20, num9);
				shotsCard.Place(num17 + num20 + num4, num19, num20, num9);
				num19 += num9 + num7;
				sec5 = new Rectangle(num17, num19, num16, num5);
				num19 += num5 + num6;
				int num25 = Math.Max(1, controlCards.Count);
				int num26 = (num16 - num4 * (num25 - 1)) / num25;
				for (int l = 0; l < controlCards.Count; l++)
				{
					controlCards[l].Place(num17 + l * (num26 + num4), num19, num26, num9);
				}
				if (sbScrollable)
				{
					int width2 = Theme.S(6f);
					int x = body.ClientSize.Width - Theme.S(9f);
					int num27 = Theme.S(6f);
					int num28 = num3 - Theme.S(12f);
					int num29 = Math.Max(Theme.S(36f), (int)((long)num28 * (long)num3 / contentH));
					int y = num27 + (int)((scrollMax > 0) ? ((long)(num28 - num29) * (long)scrollY / scrollMax) : 0);
					sbTrack = new Rectangle(x, num27, width2, num28);
					sbThumb = new Rectangle(x, y, width2, num29);
				}
				else
				{
					sbTrack = Rectangle.Empty;
					sbThumb = Rectangle.Empty;
				}
				int num30 = Theme.S(42f);
				int y2 = (num2 - num30) / 2;
				int num31 = (width - Math.Min(width - Theme.S(80f), Theme.S(1240f))) / 2;
				exitCard.SetBounds(width - num31 - Theme.S(120f), y2, Theme.S(120f), num30);
				screenCard.SetBounds(exitCard.Left - Theme.S(12f) - Theme.S(230f), y2, Theme.S(230f), num30);
				liteCard.SetBounds(screenCard.Left - Theme.S(12f) - Theme.S(150f), y2, Theme.S(150f), num30);
				header.Invalidate();
				body.Invalidate();
				footer.Invalidate();
			}
		}

		private void OnAnim()
		{
			if (base.IsHandleCreated && base.WindowState != FormWindowState.Minimized && !Theme.Calm)
			{
				float scanY = Anim.ScanY;
				int num = Theme.S(30f);
				InvalidateBand(this, new Rectangle(0, (int)lastScan - num + Theme.S(4f), base.ClientSize.Width, num));
				InvalidateBand(this, new Rectangle(0, (int)scanY - num + Theme.S(4f), base.ClientSize.Width, num));
				lastScan = scanY;
				if (header.Visible)
				{
					header.Invalidate(logoRect);
					header.Invalidate(pingRect);
				}
				footer.Invalidate(footDotRect);
			}
		}

		private static void InvalidateBand(Control parent, Rectangle formBand)
		{
			foreach (Control control in parent.Controls)
			{
				if (!control.Visible || (!(control is Canvas) && !(control is Card)))
				{
					continue;
				}
				Point point = Theme.FormOffset(control);
				Rectangle rc = new Rectangle(formBand.X - point.X, formBand.Y - point.Y, formBand.Width, formBand.Height);
				rc.Intersect(control.ClientRectangle);
				if (rc.Width > 0 && rc.Height > 0)
				{
					control.Invalidate(rc, false);
					if (control.HasChildren)
					{
						InvalidateBand(control, formBand);
					}
				}
			}
		}

		private void PaintHeader(object sender, PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			int width = header.Width;
			int height = header.Height;
			int num = Math.Min(width - Theme.S(80f), Theme.S(1240f));
			int num2 = (width - num) / 2;
			using (Pen pen = new Pen(Color.FromArgb(100, Theme.CyanLine)))
			{
				graphics.DrawLine(pen, num2, height - 1, num2 + num, height - 1);
			}
			RectangleF rectangleF = new RectangleF(num2, (float)(height - Theme.S(60f)) / 2f, Theme.S(60f), Theme.S(60f));
			logoRect = Rectangle.Inflate(Rectangle.Round(rectangleF), Theme.S(16f), Theme.S(16f));
			Theme.DrawLogo(graphics, rectangleF);
			Theme.GlowDot(graphics, rectangleF.Right - (float)Theme.S(2f), rectangleF.Top + (float)Theme.S(3f), Theme.S(5f), Theme.Neon, Anim.Pulse());
			float num3 = rectangleF.Right + (float)Theme.S(20f);
			float y = rectangleF.Y - (float)Theme.S(4f);
			Font font = Fonts.Mono(8f);
			string s = "DEFENSE_CORE";
			SizeF sizeF = graphics.MeasureString(s, font);
			RectangleF rectangleF2 = new RectangleF(num3, y, sizeF.Width + (float)Theme.S(12f), sizeF.Height + (float)Theme.S(2f));
			using (GraphicsPath path = Theme.Round(rectangleF2, Theme.S(3f)))
			{
				using (SolidBrush brush = new SolidBrush(Theme.CyanDeep))
				{
					graphics.FillPath(brush, path);
				}
				using (Pen pen2 = new Pen(Color.FromArgb(21, 94, 117)))
				{
					graphics.DrawPath(pen2, path);
				}
			}
			StringFormat stringFormat;
			using (SolidBrush brush2 = new SolidBrush(Color.FromArgb(34, 211, 238)))
			{
				stringFormat = new StringFormat();
				stringFormat.Alignment = StringAlignment.Center;
				stringFormat.LineAlignment = StringAlignment.Center;
				using (StringFormat format = stringFormat)
				{
					graphics.DrawString(s, font, brush2, rectangleF2, format);
				}
			}
			using (SolidBrush brush3 = new SolidBrush(Theme.Dim))
			{
				graphics.DrawString("SYS_BUILD: " + build, font, brush3, rectangleF2.Right + (float)Theme.S(8f), rectangleF2.Y + (float)Theme.S(1f));
			}
			using (SolidBrush brush4 = new SolidBrush(Theme.White))
			{
				graphics.DrawString("AS Shield", Fonts.Sans(18f, true), brush4, num3 - (float)Theme.S(3f), rectangleF2.Bottom + (float)Theme.S(1f));
			}
			using (SolidBrush brush5 = new SolidBrush(Theme.Muted))
			{
				graphics.DrawString("Windows security policy configuration · ver." + SelfInstall.AppVersion, Fonts.Mono(9f), brush5, num3, rectangleF2.Bottom + (float)Theme.S(38f));
			}
			Font f = Fonts.Mono(9f);
			string text = osName + " · " + build;
			SizeF sizeF2 = graphics.MeasureString(text, f);
			Color color = ((build < 10240) ? Theme.Red : Theme.Green);
			RectangleF r = new RectangleF((float)(num2 + num) - sizeF2.Width - (float)Theme.S(42f), (float)(height - Theme.S(34f)) / 2f, sizeF2.Width + (float)Theme.S(42f), Theme.S(34f));
			Badge(graphics, r, color, text, f, Color.FromArgb(52, 211, 153));
			using (SolidBrush brush6 = new SolidBrush(color))
			{
				graphics.FillEllipse(brush6, r.X + (float)Theme.S(12f), r.Y + r.Height / 2f - (float)Theme.S(5f), Theme.S(10f), Theme.S(10f));
			}
			bool flag = runView.Visible && runView.Running;
			string s2 = ((!busy) ? "READY" : ((!flag) ? "BUSY" : "RUNNING"));
			Color color2 = ((!busy) ? Theme.Neon : Theme.Amber);
			string s3 = "STATUS:";
			SizeF sizeF3 = graphics.MeasureString(s3, f);
			SizeF sizeF4 = graphics.MeasureString(s2, f);
			float num4 = sizeF3.Width + sizeF4.Width + (float)Theme.S(46f);
			RectangleF r2 = new RectangleF(r.X - (float)Theme.S(12f) - num4, r.Y, num4, r.Height);
			Badge(graphics, r2, color2, null, f, color2);
			float num5 = r2.X + (float)Theme.S(16f);
			float num6 = r2.Y + r2.Height / 2f;
			pingRect = Rectangle.Inflate(new Rectangle((int)num5, (int)num6, 0, 0), Theme.S(14f), Theme.S(14f));
			float num7 = Anim.Ping();
			float num8 = (float)Theme.S(4f) + num7 * (float)Theme.S(7f);
			using (SolidBrush brush7 = new SolidBrush(Color.FromArgb((int)(160f * (1f - num7)), color2)))
			{
				graphics.FillEllipse(brush7, num5 - num8, num6 - num8, num8 * 2f, num8 * 2f);
			}
			using (SolidBrush brush8 = new SolidBrush(color2))
			{
				graphics.FillEllipse(brush8, num5 - (float)Theme.S(4f), num6 - (float)Theme.S(4f), Theme.S(8f), Theme.S(8f));
			}
			stringFormat = new StringFormat();
			stringFormat.LineAlignment = StringAlignment.Center;
			stringFormat.FormatFlags = StringFormatFlags.NoWrap;
			using (StringFormat format2 = stringFormat)
			{
				using (SolidBrush brush9 = new SolidBrush(Theme.Muted))
				{
					graphics.DrawString(s3, f, brush9, new RectangleF(r2.X + (float)Theme.S(28f), r2.Y, sizeF3.Width + 4f, r2.Height), format2);
				}
				using (SolidBrush brush10 = new SolidBrush(Theme.Light(color2)))
				{
					graphics.DrawString(s2, f, brush10, new RectangleF(r2.X + (float)Theme.S(28f) + sizeF3.Width + (float)Theme.S(2f), r2.Y, sizeF4.Width + 8f, r2.Height), format2);
				}
			}
		}

		private static void Badge(Graphics g, RectangleF r, Color c, string text, Font f, Color fg)
		{
			using (GraphicsPath path = Theme.Round(r, Theme.S(4f)))
			{
				using (SolidBrush brush = new SolidBrush(Color.FromArgb(150, Theme.Mix(Theme.Bg, c, 0.14f))))
				{
					g.FillPath(brush, path);
				}
				using (Pen pen = new Pen(Color.FromArgb(77, c)))
				{
					g.DrawPath(pen, path);
				}
			}
			if (text == null)
			{
				return;
			}
			using (SolidBrush brush2 = new SolidBrush(fg))
			{
				StringFormat stringFormat = new StringFormat();
				stringFormat.LineAlignment = StringAlignment.Center;
				stringFormat.FormatFlags = StringFormatFlags.NoWrap;
				using (StringFormat format = stringFormat)
				{
					g.DrawString(text, f, brush2, new RectangleF(r.X + (float)Theme.S(30f), r.Y, r.Width, r.Height), format);
				}
			}
		}

		private void PaintBody(object sender, PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			DrawSection(graphics, sec1, "SECURITY PROFILES", "Choose the profile for this PC", Theme.Neon);
			DrawSection(graphics, sec2, "CHECKS AND MAINTENANCE", "Scans, rollback, cleanup", Theme.Amber);
			DrawSection(graphics, sec3, "EXTRAS", "Individual tools", Theme.Teal);
			DrawSection(graphics, sec4, "CUSTOMIZATION", "Your own profiles and evidence", Theme.Green);
			DrawSection(graphics, sec5, "CONTROL AND REPORTING", "Action log, work report, integrity", Theme.Violet);
			DrawScrollbar(graphics);
		}

		private void DrawScrollbar(Graphics g)
		{
			if (!sbScrollable || sbThumb.IsEmpty)
			{
				return;
			}
			Theme.Quality(g);
			using (GraphicsPath path = Theme.Round(sbTrack, Theme.S(3f)))
			{
				using (SolidBrush brush = new SolidBrush(Color.FromArgb(70, Theme.Mix(Theme.Bg, Theme.Neon, 0.18f))))
				{
					g.FillPath(brush, path);
				}
			}
			Rectangle rectangle = sbThumb;
			bool flag = sbDrag || rectangle.Contains(body.PointToClient(Cursor.Position));
			using (GraphicsPath path2 = Theme.Round(rectangle, Theme.S(3f)))
			{
				using (LinearGradientBrush brush2 = new LinearGradientBrush(rectangle, Theme.Light(Theme.Neon), Theme.Neon, 90f))
				{
					g.FillPath(brush2, path2);
				}
				using (Pen pen = new Pen(Color.FromArgb((!flag) ? 130 : 220, Theme.Neon), 1f))
				{
					g.DrawPath(pen, path2);
				}
			}
		}

		private void SetScroll(int v)
		{
			if (!sbScrollable)
			{
				return;
			}
			int num = Math.Max(0, Math.Min(scrollMax, v));
			if (num == scrollY)
			{
				return;
			}
			int num2 = num - scrollY;
			scrollY = num;
			body.SuspendLayout();
			foreach (Card action in actions)
			{
				action.Top -= num2;
			}
			customCard.Top -= num2;
			shotsCard.Top -= num2;
			body.ResumeLayout(false);
			sec1.Y -= num2;
			sec2.Y -= num2;
			sec3.Y -= num2;
			sec4.Y -= num2;
			sec5.Y -= num2;
			UpdateThumb();
			body.Invalidate();
		}

		private void UpdateThumb()
		{
			if (!sbScrollable || sbTrack.IsEmpty)
			{
				sbThumb = Rectangle.Empty;
				return;
			}
			int height = sbTrack.Height;
			int num = Math.Max(Theme.S(36f), (int)((long)height * (long)viewH / Math.Max(1, contentH)));
			int y = sbTrack.Y + (int)((scrollMax > 0) ? ((long)(height - num) * (long)scrollY / scrollMax) : 0);
			sbThumb = new Rectangle(sbTrack.X, y, sbTrack.Width, num);
		}

		private void BodyMouseDown(object sender, MouseEventArgs e)
		{
			if (sbScrollable && sbThumb.Contains(e.Location))
			{
				sbDrag = true;
				sbGrab = e.Y - sbThumb.Y;
			}
			else if (sbScrollable && sbTrack.Contains(e.Location))
			{
				SetScroll(scrollY + ((e.Y >= sbThumb.Y) ? (viewH * 8 / 10) : (-viewH * 8 / 10)));
			}
		}

		private void BodyMouseMove(object sender, MouseEventArgs e)
		{
			if (!sbDrag)
			{
				if (sbScrollable)
				{
					body.Invalidate(sbTrack);
				}
			}
			else
			{
				int height = sbTrack.Height;
				int height2 = sbThumb.Height;
				int num = e.Y - sbGrab - sbTrack.Y;
				int num2 = Math.Max(1, height - height2);
				SetScroll((int)((long)num * (long)scrollMax / num2));
			}
		}

		private void BodyMouseUp(object sender, MouseEventArgs e)
		{
			sbDrag = false;
		}

		public bool PreFilterMessage(ref Message m)
		{
			if (m.Msg == 522 && sbScrollable && body.Visible && !body.IsDisposed)
			{
				Point pt = body.PointToClient(Cursor.Position);
				if (body.ClientRectangle.Contains(pt))
				{
					int value = (short)((long)m.WParam >> 16);
					SetScroll(scrollY - Math.Sign(value) * Theme.S(68f));
					return true;
				}
			}
			return false;
		}

		public static void DrawSection(Graphics g, Rectangle r, string title, string sub, Color c)
		{
			RectangleF r2 = new RectangleF(r.X, (float)r.Y + (float)(r.Height - Theme.S(20f)) / 2f, Theme.S(6f), Theme.S(20f));
			Theme.GlowRect(g, r2, Theme.S(2f), c, 1f, 3);
			using (GraphicsPath path = Theme.Round(r2, Theme.S(2f)))
			{
				using (SolidBrush brush = new SolidBrush(c))
				{
					g.FillPath(brush, path);
				}
			}
			Font font = Fonts.Mono(10.5f, true);
			using (SolidBrush brush2 = new SolidBrush(Color.FromArgb(226, 232, 240)))
			{
				StringFormat stringFormat = new StringFormat();
				stringFormat.LineAlignment = StringAlignment.Center;
				stringFormat.FormatFlags = StringFormatFlags.NoWrap;
				using (StringFormat format = stringFormat)
				{
					g.DrawString(title, font, brush2, new RectangleF(r.X + Theme.S(18f), r.Y, r.Width, r.Height), format);
					float width = g.MeasureString(title, font).Width;
					float num = 0f;
					using (SolidBrush brush3 = new SolidBrush(Theme.Dim))
					{
						g.DrawString("// " + sub, Fonts.Mono(9f), brush3, new RectangleF((float)(r.X + Theme.S(18f)) + width + (float)Theme.S(8f), r.Y, r.Width, r.Height), format);
						num = g.MeasureString("// " + sub, Fonts.Mono(9f)).Width;
					}
					if (!Theme.Calm)
					{
						float num2 = (float)(r.X + Theme.S(18f)) + width + (float)Theme.S(16f) + num;
						float num3 = r.Right - Theme.S(158f);
						float num4 = (float)r.Y + (float)r.Height / 2f;
						if (num3 - num2 > (float)Theme.S(24f))
						{
							using (Pen pen = new Pen(Color.FromArgb(34, c), 1f))
							{
								pen.DashStyle = DashStyle.Dot;
								g.DrawLine(pen, num2, num4, num3, num4);
							}
						}
					}
				}
			}
			if (Theme.Calm)
			{
				return;
			}
			float num5 = r.Right - Theme.S(150f);
			float num6 = (float)r.Y + (float)r.Height / 2f;
			Random random = new Random(title.Length * 31 + sub.Length);
			using (SolidBrush brush4 = new SolidBrush(Color.FromArgb(42, c)))
			{
				float num7;
				for (; num5 < (float)(r.Right - Theme.S(8f)); num5 += num7 + (float)Theme.S(2f) + (float)random.Next(0, 2))
				{
					num7 = Theme.S(1f) + random.Next(0, 3);
					if (random.Next(0, 4) != 0)
					{
						float num8 = Theme.S(5f) + random.Next(0, Theme.S(11f));
						g.FillRectangle(brush4, num5, num6 - num8 / 2f, num7, num8);
					}
				}
			}
		}

		private void PaintFooter(object sender, PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			int width = footer.Width;
			int num = Math.Min(width - Theme.S(80f), Theme.S(1240f));
			int num2 = (width - num) / 2;
			using (Pen pen = new Pen(Color.FromArgb(128, Theme.CyanLine)))
			{
				graphics.DrawLine(pen, num2, 0, num2 + num, 0);
			}
			Font font = Fonts.Mono(9.5f);
			float num3 = Theme.S(42f);
			float num4 = liteCard.Left - num2 - Theme.S(20f);
			float num5 = Math.Max(Theme.S(40f), Math.Min(num4 - (float)Theme.S(64f), graphics.MeasureString(status, font).Width + (float)Theme.S(4f)));
			RectangleF r = new RectangleF(num2, ((float)footer.Height - num3) / 2f, num5 + (float)Theme.S(64f), num3);
			using (GraphicsPath path = Theme.Round(r, Theme.S(6f)))
			{
				using (SolidBrush brush = new SolidBrush(Color.FromArgb(100, Theme.CyanDeep)))
				{
					graphics.FillPath(brush, path);
				}
				using (Pen pen2 = new Pen(Color.FromArgb(150, Theme.CyanLine)))
				{
					graphics.DrawPath(pen2, path);
				}
			}
			float num6 = r.Y + r.Height / 2f;
			footDotRect = Rectangle.Inflate(new Rectangle((int)(r.X + (float)Theme.S(18f)), (int)num6, 0, 0), Theme.S(16f), Theme.S(16f));
			Theme.GlowDot(graphics, r.X + (float)Theme.S(18f), num6, Theme.S(5f), statusColor, Anim.Pulse());
			StringFormat stringFormat = new StringFormat();
			stringFormat.LineAlignment = StringAlignment.Center;
			stringFormat.Trimming = StringTrimming.EllipsisCharacter;
			stringFormat.FormatFlags = StringFormatFlags.NoWrap;
			using (StringFormat format = stringFormat)
			{
				using (SolidBrush brush2 = new SolidBrush(Color.FromArgb(34, 211, 238)))
				{
					graphics.DrawString("$", Fonts.Mono(10f, true), brush2, new RectangleF(r.X + (float)Theme.S(34f), r.Y, Theme.S(20f), r.Height), format);
				}
				using (SolidBrush brush3 = new SolidBrush((!busy) ? Theme.Text : Theme.White))
				{
					graphics.DrawString(status, font, brush3, new RectangleF(r.X + (float)Theme.S(54f), r.Y, num5, r.Height), format);
				}
			}
		}

		public void SetStatus(string text, Color c, bool isBusy)
		{
			status = text;
			statusColor = c;
			busy = isBusy;
			footer.Invalidate();
			header.Invalidate();
		}

		private static int GetBuild()
		{
			try
			{
				using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion"))
				{
					return int.Parse((string)registryKey.GetValue("CurrentBuildNumber"));
				}
			}
			catch
			{
				return (Environment.GetEnvironmentVariable("ASPREVIEW") != null) ? 26200 : 0;
			}
		}

		[DllImport("kernel32.dll")]
		private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);

		private static bool ResolveLite()
		{
			string environmentVariable = Environment.GetEnvironmentVariable("ASLITE");
			if (environmentVariable == "1")
			{
				return true;
			}
			if (environmentVariable == "0")
			{
				return false;
			}
			try
			{
				if (File.Exists(LiteFile))
				{
					string[] array = File.ReadAllLines(LiteFile);
					foreach (string text in array)
					{
						if (text.StartsWith("Lite="))
						{
							return text.Substring(5).Trim() == "1";
						}
					}
				}
			}
			catch
			{
			}
			return IsWeakPc();
		}

		private static bool IsWeakPc()
		{
			try
			{
				if (Environment.GetEnvironmentVariable("ASPREVIEW") != null)
				{
					return false;
				}
				if (Environment.ProcessorCount <= 2)
				{
					return true;
				}
				MEMORYSTATUSEX mEMORYSTATUSEX = default(MEMORYSTATUSEX);
				mEMORYSTATUSEX.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
				MEMORYSTATUSEX m = mEMORYSTATUSEX;
				if (GlobalMemoryStatusEx(ref m))
				{
					double num = (double)m.ullTotalPhys / 1073741824.0;
					if (num > 0.0 && num < 3.6)
					{
						return true;
					}
				}
			}
			catch
			{
			}
			return false;
		}

		private static void SaveLite(bool lite)
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(LiteFile));
				File.WriteAllText(LiteFile, "Lite=" + ((!lite) ? "0" : "1") + "\r\n");
			}
			catch
			{
			}
		}

		private void Prepare()
		{
			try
			{
				Directory.CreateDirectory(workDir);
				// v2: remove the scripts of a previous version so that nothing outside the manifest stays in the folder.
				foreach (string stale in new string[6] { "Private", "Public", "Engine", "Profiles", "Catalog", "Schema" })
				{
					string staleDir = Path.Combine(workDir, stale);
					if (Directory.Exists(staleDir))
					{
						Directory.Delete(staleDir, true);
					}
				}
				using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
				{
					using (ZipArchive zipArchive = new ZipArchive(stream, ZipArchiveMode.Read))
					{
						string value = Path.GetFullPath(workDir) + Path.DirectorySeparatorChar;
						foreach (ZipArchiveEntry entry in zipArchive.Entries)
						{
							if (entry.FullName.EndsWith("/"))
							{
								continue;
							}
							string fullPath = Path.GetFullPath(Path.Combine(workDir, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
							if (!fullPath.StartsWith(value, StringComparison.OrdinalIgnoreCase))
							{
								continue;
							}
							Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
							using (Stream stream2 = entry.Open())
							{
								using (FileStream destination = File.Create(fullPath))
								{
									stream2.CopyTo(destination);
								}
							}
						}
					}
				}
				string hardenError;
				bool hardened = PayloadGuard.Harden(workDir, out hardenError);
				List<string> tampered = PayloadGuard.Verify(workDir);
				if (PayloadGuard.Manifest != null && tampered.Count > 0)
				{
					throw new InvalidOperationException("the extracted files do not match the manifest: " + tampered[0]);
				}
				if (!hardened || PayloadGuard.Manifest == null || catalogWarnings.Count > 0)
				{
					List<string> w = new List<string>(catalogWarnings);
					if (!hardened)
					{
						w.Add("integrity label not set (" + hardenError + ")");
					}
					if (PayloadGuard.Manifest == null)
					{
						w.Add("build without an integrity manifest");
					}
					SetStatus("Ready, with warnings: " + string.Join("; ", w), Theme.Amber, false);
				}
				else
				{
					SetStatus("Ready. Choose an action — every step and question appears right in this window.", Theme.Green, false);
				}
				UpdateCustomBadge();
			}
			catch (Exception ex)
			{
				SetStatus("Failed to extract files: " + ex.Message, Theme.Red, false);
				foreach (Card action in actions)
				{
					action.Enabled = false;
				}
			}
		}

		private void GenerateAkt()
		{
			List<ActLog.Row> list = ActLog.Read();
			if (list.Count == 0)
			{
				MessageBox.Show(this, "The action log is empty. The work report is created after at least one action (applying a profile, a check, etc.).", Text, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			int brokenAt;
			bool flag = ActLog.VerifyChain(list, out brokenAt);
			string admin = "";
			try
			{
				admin = new ShotSettings(workDir).Admin;
			}
			catch
			{
			}
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
			string text = Path.Combine(folderPath, "AS Shield reports");
			try
			{
				if (!Directory.Exists(text))
				{
					Directory.CreateDirectory(text);
				}
			}
			catch
			{
			}
			string text2 = Path.Combine(text, "Work report " + Environment.MachineName + " " + DateTime.Now.ToString("yyyy-MM-dd HH-mm") + ".docx");
			try
			{
				AktDocx.Write(text2, list, admin, flag, brokenAt);
				try
				{
					Process.Start(text2);
				}
				catch
				{
				}
				string text3 = "Work report created (" + list.Count + " actions).\n" + text2;
				if (!flag)
				{
					string text4 = text3;
					text3 = text4 + "\n\nWARNING: the log may have been modified — the integrity chain is broken at record no. " + brokenAt + ".";
				}
				MessageBox.Show(this, text3, Text, MessageBoxButtons.OK, (!flag) ? MessageBoxIcon.Exclamation : MessageBoxIcon.Asterisk);
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "Could not create the work report: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void GenerateSessionProtocol()
		{
			List<ActLog.Row> list = ActLog.Read();
			List<ActLog.Row> list2 = new List<ActLog.Row>();
			foreach (ActLog.Row item in list)
			{
				if (string.Compare(item.Timestamp, sessionStart, StringComparison.Ordinal) >= 0)
				{
					list2.Add(item);
				}
			}
			if (list2.Count == 0)
			{
				MessageBox.Show(this, "No actions have been performed in this session yet. The session report is created after a profile or a check is run.", Text, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			int brokenAt;
			bool flag = ActLog.VerifyChain(list, out brokenAt);
			SessionDocx.Info info = new SessionDocx.Info();
			info.Machine = Environment.MachineName;
			info.Os = osName + " (build " + build + ")";
			info.Operator = WinUser();
			info.SessionStart = sessionStart;
			info.SessionEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
			info.Total = list2.Count;
			info.ChainOk = flag;
			info.BrokenAt = brokenAt;
			SessionDocx.Info info2 = info;
			foreach (ActLog.Row item2 in list2)
			{
				if (item2.Result != null && (item2.Result.StartsWith("ERROR") || item2.Result.StartsWith("STOPPED") || item2.Result.StartsWith("WITH ERRORS") || item2.Result.StartsWith("FAIL")))
				{
					info2.Err++;
				}
				else
				{
					info2.Ok++;
				}
			}
			try
			{
				info2.Admin = new ShotSettings(workDir).Admin;
			}
			catch
			{
			}
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
			string text = Path.Combine(folderPath, "AS Shield reports");
			try
			{
				if (!Directory.Exists(text))
				{
					Directory.CreateDirectory(text);
				}
			}
			catch
			{
			}
			string text2 = Path.Combine(text, "Session report " + Environment.MachineName + " " + DateTime.Now.ToString("yyyy-MM-dd HH-mm") + ".docx");
			try
			{
				SessionDocx.Write(text2, list2, info2);
				try
				{
					Process.Start(text2);
				}
				catch
				{
				}
				string text3 = "Session report created (" + list2.Count + " actions: succeeded " + info2.Ok + ", failed " + info2.Err + ").\n" + text2;
				if (!flag)
				{
					string text4 = text3;
					text3 = text4 + "\n\nWARNING: the log may have been modified — the integrity chain is broken at record no. " + brokenAt + ".";
				}
				MessageBox.Show(this, text3, Text, MessageBoxButtons.OK, (!flag) ? MessageBoxIcon.Exclamation : MessageBoxIcon.Asterisk);
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "Could not create the session report: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private static string WinUser()
		{
			try
			{
				return WindowsIdentity.GetCurrent().Name;
			}
			catch
			{
				return Environment.UserName;
			}
		}

		private void RestoreEnabled()
		{
			foreach (Card action in actions)
			{
				bool enabled;
				action.Enabled = !baseEnabled.TryGetValue(action, out enabled) || enabled;
			}
		}

		/// <summary>v2: cards from Catalog/menu.json and Profiles/*/profile.json (see Catalog.cs).</summary>
		private bool BuildCardsFromCatalog()
		{
			List<CardDef> defs;
			try
			{
				defs = Catalog.Load(catalogWarnings);
			}
			catch (Exception ex)
			{
				catalogWarnings.Add("Catalog could not be read: " + ex.Message);
				return false;
			}
			if (defs == null || defs.Count == 0)
			{
				return false;
			}
			foreach (CardDef d in defs)
			{
				bool allowed = d.IsAllowed(build);
				Card card = MakeAction(d.Title, d.Desc, d.Icon ?? "\ue946", d.Accent, d.Action, allowed, allowed ? (d.IsLocal ? "Local" : null) : (d.Note ?? "Not for this OS"));
				cardDefs[d.Action] = d;
				switch (d.Section)
				{
				case "as":
					asCards.Add(card);
					break;
				case "tools":
					toolCards.Add(card);
					break;
				case "control":
					controlCards.Add(card);
					break;
				default:
					extraCards.Add(card);
					break;
				}
			}
			return true;
		}

		private void OnAction(object sender, EventArgs e)
		{
			Card card = (Card)sender;
			RunAction((string)card.Tag, "", card.Title, card.Desc);
		}

		public void RunAction(string action, string extraArgs, string title, string desc)
		{
			if (busy)
			{
				return;
			}
			string text;
			switch (action)
			{
			case "act":
				GenerateAkt();
				return;
			case "session":
				GenerateSessionProtocol();
				return;
			case "phrases":
				try
				{
					using (PhraseEditor phraseEditor = new PhraseEditor(workDir))
					{
						phraseEditor.ShowDialog(this);
						return;
					}
				}
				catch (Exception ex)
				{
					MessageBox.Show(this, "Phrase editor: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
					return;
				}
			default:
			{
				if (_003C_003Ef__switch_0024map0 == null)
				{
					Dictionary<string, int> dictionary = new Dictionary<string, int>(9);
					dictionary.Add("scanlic", 0);
					dictionary.Add("scandocs", 0);
					dictionary.Add("pcinfo", 0);
					dictionary.Add("policyaudit", 0);
					dictionary.Add("integrity", 0);
					dictionary.Add("allowself", 1);
					dictionary.Add("restore", 2);
					dictionary.Add("usbclean", 3);
					_003C_003Ef__switch_0024map0 = dictionary;
				}
				if (_003C_003Ef__switch_0024map0.TryGetValue(action, out var value))
				{
					switch (value)
					{
					case 0:
						break;
					case 1:
						goto IL_0163;
					case 2:
						goto IL_016e;
					case 3:
						goto IL_0179;
					default:
						goto IL_0184;
					}
					text = "This is a check — the system will not be changed.";
					break;
				}
				goto IL_0184;
			}
			case null:
				goto IL_0184;
				IL_0184:
				text = "The system will be changed.";
				break;
				IL_0179:
				text = "Only traces of OLD devices will be removed. Drives that are connected now are detected and left untouched.";
				break;
				IL_016e:
				text = "WARNING: all applied security policies, AppLocker rules and services will be reset to Windows defaults.";
				break;
				IL_0163:
				text = "The program will be added to the SRP and AppLocker exceptions (allowed to run for auditing after the policies are applied).";
				break;
			}
			int num;
			switch (action)
			{
			case "scanlic":
			case "scandocs":
			case "pcinfo":
			case "policyaudit":
			case "integrity":
				num = 64;
				break;
			default:
				num = 48;
				break;
			}
			CardDef def;
			if (cardDefs.TryGetValue(action, out def))
			{
				if (!string.IsNullOrEmpty(def.ConfirmText))
				{
					text = def.ConfirmText;
				}
				else if (def.Confirm == "check")
				{
					text = "This is a check — the system will not be changed.";
				}
				num = (def.Confirm == "check") ? 64 : 48;
			}
			MessageBoxIcon messageBoxIcon = (MessageBoxIcon)num;
			if (MessageBox.Show(this, "Run '" + title + "'?\n\n" + desc + "\n\n" + text, Text, MessageBoxButtons.YesNo, messageBoxIcon) != DialogResult.Yes)
			{
				return;
			}
			foreach (Card action2 in actions)
			{
				action2.Enabled = false;
			}
			busy = true;
			runTitle = title;
			lastAction = action;
			SetStatus("Running '" + title + "'…", Theme.Amber, true);
			beforeRun = body;
			body.Visible = false;
			runView.Visible = true;
			runView.BringToFront();
			try
			{
				List<string> tampered = PayloadGuard.Verify(workDir);
				if (tampered.Count > 0 && PayloadGuard.Manifest != null)
				{
					throw new InvalidOperationException("Program files were changed after extraction — the run is blocked:\n" + string.Join("\n", tampered.GetRange(0, Math.Min(10, tampered.Count))) + "\nRestart AS Shield to extract a clean copy.");
				}
				string psAction = action;
				string psExtra = extraArgs;
				if (action.StartsWith("profile:", StringComparison.Ordinal))
				{
					psAction = "profile";
					psExtra = "-Build " + action.Substring(8) + ((!string.IsNullOrEmpty(extraArgs)) ? (" " + extraArgs) : "");
				}
				runView.Start(title, Path.Combine(workDir, "RunAction.ps1"), "-Action " + psAction + ((!string.IsNullOrEmpty(psExtra)) ? (" " + psExtra) : ""));
			}
			catch (Exception ex2)
			{
				busy = false;
				SetStatus("Failed to start PowerShell: " + ex2.Message, Theme.Red, false);
				if (ex2 is InvalidOperationException)
				{
					MessageBox.Show(this, ex2.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Hand);
				}
				RestoreEnabled();
				CloseRun();
			}
		}

		public void CloseRun()
		{
			if (!runView.Running)
			{
				runView.Visible = false;
				ShowHome();
			}
		}

		/// <summary>Opens the folder for administrator-defined profiles (created on first use, admin-only ACL is checked by the engine).</summary>
		public void OpenLocalProfiles()
		{
			try
			{
				Directory.CreateDirectory(Catalog.LocalProfilesDir);
				Process.Start("explorer.exe", "\"" + Catalog.LocalProfilesDir + "\"");
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Hand);
			}
		}

		private void UpdateCustomBadge()
		{
			int count = 0;
			foreach (CardDef d in cardDefs.Values)
			{
				if (d.IsLocal)
				{
					count++;
				}
			}
			customCard.Badge = ((count <= 0) ? null : ("Local: " + count));
			customCard.Invalidate();
		}

		public void ShowShots()
		{
			body.Visible = false;
			shotsView.Visible = true;
			shotsView.Refresh();
			shotsView.BringToFront();
		}

		public void CaptureSingleShot(int n)
		{
			try
			{
				ShotItem shotItem = null;
				foreach (ShotItem item in ShotCatalog.Items)
				{
					if (item.N == n)
					{
						shotItem = item;
						break;
					}
				}
				if (shotItem == null)
				{
					return;
				}
				ShotSettings shotSettings = new ShotSettings(workDir);
				try
				{
					Directory.CreateDirectory(shotSettings.Folder);
					Directory.CreateDirectory(shotSettings.PngDir);
				}
				catch
				{
				}
				FormWindowState windowState = base.WindowState;
				base.WindowState = FormWindowState.Minimized;
				try
				{
					List<ShotItem> list = new List<ShotItem>();
					list.Add(shotItem);
					using (CaptureBar captureBar = new CaptureBar(list, shotSettings, workDir))
					{
						captureBar.ShowDialog(this);
					}
				}
				finally
				{
					base.WindowState = ((windowState != FormWindowState.Minimized) ? windowState : FormWindowState.Maximized);
					Activate();
				}
			}
			catch
			{
			}
		}

		public void ShowHome()
		{
			shotsView.Visible = false;
			int done = shotsView.Done;
			shotsCard.Badge = ((done <= 0) ? null : ("Done: " + done + "/" + ShotCatalog.Items.Count));
			shotsCard.Invalidate();
			body.Visible = true;
			UpdateCustomBadge();
			SetStatus("Ready. Choose an action — every step and question appears right in this window.", Theme.Green, false);
		}

		private void PreviewHook()
		{
			string environmentVariable = Environment.GetEnvironmentVariable("ASPREVIEW");
			if (environmentVariable == "shots")
			{
				ShowShots();
			}
		}
	}
}
