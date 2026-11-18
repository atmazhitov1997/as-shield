using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ASConfig
{
	internal class CaptureBar : Form
	{
		private readonly List<ShotItem> queue;

		private readonly ShotSettings cfg;

		private readonly string workDir;

		private int idx;

		private readonly List<string> shots = new List<string>();

		private readonly Card bApply;

		private readonly Card bOpen;

		private readonly Card bShot;

		private readonly Card bFile;

		private readonly Card bSkip;

		private readonly Card bNext;

		private readonly Card bClose;

		private bool applying;

		private string flash;

		private DateTime flashUntil;

		private Image thumb;

		public int Saved;

		private ShotItem Cur => queue[Math.Min(idx, queue.Count - 1)];

		public CaptureBar(List<ShotItem> queue, ShotSettings cfg, string workDir)
		{
			CaptureBar captureBar = this;
			this.queue = queue;
			this.cfg = cfg;
			this.workDir = workDir;
			Text = "AS Shield — evidence screenshots";
			base.FormBorderStyle = FormBorderStyle.None;
			base.TopMost = true;
			base.ShowInTaskbar = true;
			base.StartPosition = FormStartPosition.Manual;
			BackColor = Theme.Bg;
			DoubleBuffered = true;
			base.Size = new Size(Theme.S(540f), Theme.S(464f));
			Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
			base.Location = new Point(workingArea.Right - base.Width - Theme.S(16f), workingArea.Bottom - base.Height - Theme.S(16f));
			try
			{
				base.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
			}
			catch
			{
			}
			bApply = Btn("Apply setting", "\ue713", Theme.Violet, delegate
			{
				captureBar.ApplyAction();
			});
			new ToolTip().SetToolTip(bApply, "Apply exactly this setting before taking the screenshot");
			bOpen = Btn("Open", "\ue8a7", Theme.Neon, delegate
			{
				ShotLauncher.Open(captureBar.Cur, workDir);
			});
			bShot = Btn("Capture · F9", "\ue722", Theme.Green, delegate
			{
				captureBar.Capture();
			});
			bFile = Btn("From file", "\ue8e5", Theme.Amber, delegate
			{
				captureBar.FromFile();
			});
			bSkip = Btn("Skip", "\ue893", Theme.Muted, delegate
			{
				captureBar.Advance(false);
			});
			bNext = Btn("Next", "\ue72a", Theme.Neon, delegate
			{
				captureBar.Advance(true);
			});
			bClose = Btn("", "\ue711", Theme.Red, delegate
			{
				captureBar.Finish();
			});
			new ToolTip().SetToolTip(bClose, "Finish capturing");
			base.MouseDown += delegate(object s, MouseEventArgs e)
			{
				if (e.Button == MouseButtons.Left)
				{
					ReleaseCapture();
					SendMessage(captureBar.Handle, 161, (IntPtr)2, IntPtr.Zero);
				}
			};
			base.Paint += OnPaintBar;
			Anim.Tick += OnTick;
			base.Load += delegate
			{
				try
				{
					RegisterHotKey(captureBar.Handle, 1, 0u, 120u);
				}
				catch
				{
				}
				captureBar.ShowItem();
			};
			base.FormClosed += delegate
			{
				try
				{
					UnregisterHotKey(captureBar.Handle, 1);
				}
				catch
				{
				}
				Anim.Tick -= captureBar.OnTick;
				if (captureBar.thumb != null)
				{
					captureBar.thumb.Dispose();
				}
			};
			base.Resize += delegate
			{
				captureBar.LayoutBar();
			};
			LayoutBar();
		}

		[DllImport("user32.dll")]
		private static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk);

		[DllImport("user32.dll")]
		private static extern bool UnregisterHotKey(IntPtr h, int id);

		[DllImport("user32.dll")]
		private static extern bool ReleaseCapture();

		[DllImport("user32.dll")]
		private static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

		private Card Btn(string t, string icon, Color c, EventHandler click)
		{
			Card card = new Card();
			card.Title = t;
			card.Icon = icon;
			card.Accent = c;
			Card card2 = card;
			card2.Click += click;
			base.Controls.Add(card2);
			return card2;
		}

		private void OnTick()
		{
			if (flash != null && DateTime.Now > flashUntil)
			{
				flash = null;
				Invalidate();
			}
		}

		protected override void WndProc(ref Message m)
		{
			if (m.Msg == 786 && m.WParam.ToInt32() == 1)
			{
				BeginInvoke(new MethodInvoker(Capture));
			}
			else
			{
				base.WndProc(ref m);
			}
		}

		protected override void OnPaintBackground(PaintEventArgs e)
		{
			Theme.Backdrop(e.Graphics, this, e.ClipRectangle);
		}

		private void LayoutBar()
		{
			int width = base.ClientSize.Width;
			int height = base.ClientSize.Height;
			int num = Theme.S(16f);
			int num2 = Theme.S(8f);
			int num3 = Theme.S(40f);
			bClose.SetBounds(width - num - Theme.S(34f), Theme.S(12f), Theme.S(34f), Theme.S(30f));
			int num4 = height - num - num3;
			int y = num4 - num2 - num3;
			List<Card> list = new List<Card>();
			if (bApply.Visible)
			{
				list.Add(bApply);
			}
			list.Add(bOpen);
			list.Add(bShot);
			list.Add(bFile);
			int count = list.Count;
			int num5 = (width - num * 2 - num2 * (count - 1)) / count;
			for (int i = 0; i < count; i++)
			{
				list[i].SetBounds(num + i * (num5 + num2), y, num5, num3);
			}
			int num6 = (width - num * 2 - num2) / 2;
			bSkip.SetBounds(num, num4, num6, num3);
			bNext.SetBounds(num + num6 + num2, num4, num6, num3);
		}

		private void ShowItem()
		{
			shots.Clear();
			if (thumb != null)
			{
				thumb.Dispose();
				thumb = null;
			}
			ShotItem cur = Cur;
			bOpen.Enabled = cur.Open.Length > 0;
			bShot.Enabled = !cur.NoCapture;
			bApply.Visible = !string.IsNullOrEmpty(cur.Apply);
			bApply.Enabled = !applying;
			LayoutBar();
			bNext.Title = ((idx != queue.Count - 1) ? "Save · next" : "Save and finish");
			bNext.Invalidate();
			Invalidate();
			if (cur.Open.Length > 0)
			{
				ShotLauncher.Open(cur, workDir);
			}
			Activate();
		}

		private void ApplyAction()
		{
			if (applying || string.IsNullOrEmpty(Cur.Apply) || MessageBox.Show(this, "Apply the setting for '" + Cur.Title + "'?\nThe system will be changed.", "AS Shield", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
			{
				return;
			}
			applying = true;
			bApply.Enabled = false;
			FlashMsg("Applying the setting…");
			string tok = Cur.Apply;
			Thread thread = new Thread((ThreadStart)delegate
			{
				string text = "";
				string text2 = "";
				int num = -1;
				try
				{
					ProcessStartInfo startInfo = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + Path.Combine(workDir, "ShotApply.ps1") + "\" -Token " + tok)
					{
						UseShellExecute = false,
						CreateNoWindow = true,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
						WorkingDirectory = workDir,
						StandardOutputEncoding = new UTF8Encoding(false),
						StandardErrorEncoding = new UTF8Encoding(false)
					};
					Process process = Process.Start(startInfo);
					text = process.StandardOutput.ReadToEnd();
					text2 = process.StandardError.ReadToEnd();
					process.WaitForExit();
					num = process.ExitCode;
				}
				catch (Exception ex)
				{
					text2 = ex.Message;
				}
				string last = "Applied";
				string[] array = (text ?? "").Replace("\r", "").Split(new char[1] { '\n' });
				foreach (string text3 in array)
				{
					if (text3.Trim().Length > 0 && text3.Trim() != "OK")
					{
						last = text3.Trim();
					}
				}
				int c2 = num;
				string e2 = text2;
				try
				{
					BeginInvoke((MethodInvoker)delegate
					{
						applying = false;
						bApply.Enabled = true;
						if (c2 == 0)
						{
							FlashMsg("✔ " + last);
							if (Cur.Open.Length > 0)
							{
								ShotLauncher.Open(Cur, workDir);
							}
						}
						else
						{
							FlashMsg("Failed to apply");
							MessageBox.Show(this, "Could not apply the setting:\n" + e2, Text);
						}
					});
				}
				catch
				{
				}
			});
			thread.IsBackground = true;
			Thread thread2 = thread;
			thread2.Start();
		}

		private new void Capture()
		{
			if (Cur.NoCapture)
			{
				FlashMsg("For this item use 'From file' only");
				return;
			}
			Screen screen = Screen.FromPoint(Cursor.Position);
			base.Opacity = 0.0;
			Hide();
			Application.DoEvents();
			Thread.Sleep(350);
			try
			{
				using (Bitmap bitmap = new Bitmap(screen.Bounds.Width, screen.Bounds.Height, PixelFormat.Format24bppRgb))
				{
					using (Graphics graphics = Graphics.FromImage(bitmap))
					{
						graphics.CopyFromScreen(screen.Bounds.Location, Point.Empty, screen.Bounds.Size);
					}
					string text = NextPath(".png");
					bitmap.Save(text, ImageFormat.Png);
					AddShot(text, bitmap);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("Could not capture the screen:\n" + ex.Message, Text);
			}
			finally
			{
				Show();
				base.Opacity = 1.0;
				base.TopMost = true;
			}
		}

		private void FromFile()
		{
			base.TopMost = false;
			OpenFileDialog openFileDialog = new OpenFileDialog();
			openFileDialog.Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp";
			openFileDialog.Title = "Add a screenshot for '" + Cur.Title + "'";
			using (OpenFileDialog openFileDialog2 = openFileDialog)
			{
				if (openFileDialog2.ShowDialog(this) == DialogResult.OK)
				{
					try
					{
						string text = Path.GetExtension(openFileDialog2.FileName).ToLowerInvariant();
						string text2 = NextPath((!(text == ".jpg") && !(text == ".jpeg")) ? ".png" : ".jpg");
						using (Image image = Image.FromFile(openFileDialog2.FileName))
						{
							if (text == ".jpg" || text == ".jpeg")
							{
								File.Copy(openFileDialog2.FileName, text2, true);
							}
							else
							{
								image.Save(text2, ImageFormat.Png);
							}
							AddShot(text2, image);
						}
					}
					catch (Exception ex)
					{
						MessageBox.Show(this, "Could not add the file:\n" + ex.Message, Text);
					}
				}
			}
			base.TopMost = true;
		}

		private string NextPath(string ext)
		{
			string path = "Screenshot " + Cur.N + ((shots.Count <= 0) ? "" : (" (" + (shots.Count + 1) + ")")) + ext;
			return Path.Combine(cfg.PngDir, path);
		}

		private void AddShot(string path, Image img)
		{
			shots.Add(path);
			if (thumb != null)
			{
				thumb.Dispose();
			}
			int num = Theme.S(150f);
			int val = Math.Max(1, (int)((double)num * (double)img.Height / (double)img.Width));
			thumb = new Bitmap(img, num, Math.Min(val, Theme.S(100f)));
			FlashMsg("Screenshot " + shots.Count + " saved" + ((Cur.Expect <= shots.Count) ? "" : (" — " + (Cur.Expect - shots.Count) + " more needed")));
		}

		private void FlashMsg(string m)
		{
			flash = m;
			flashUntil = DateTime.Now.AddSeconds(3.0);
			Invalidate();
		}

		private void SaveCurrent()
		{
			if (shots.Count == 0)
			{
				return;
			}
			try
			{
				ShotDocx.Write(cfg.DocPath(Cur.N), Cur, shots, cfg.Admin);
				Saved++;
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "Could not save the document:\n" + ex.Message, Text);
			}
		}

		private void Advance(bool save)
		{
			if ((!save || shots.Count != 0 || MessageBox.Show(this, "This item has no screenshots yet. Move on without saving?", Text, MessageBoxButtons.YesNo) == DialogResult.Yes) && (!save || shots.Count <= 0 || shots.Count >= Cur.Expect || MessageBox.Show(this, "This item usually needs " + Cur.Expect + " screenshot(s), taken: " + shots.Count + ". Save anyway?", Text, MessageBoxButtons.YesNo) == DialogResult.Yes))
			{
				if (save)
				{
					SaveCurrent();
				}
				idx++;
				if (idx >= queue.Count)
				{
					Close();
				}
				else
				{
					ShowItem();
				}
			}
		}

		private void Finish()
		{
			if (shots.Count > 0)
			{
				switch (MessageBox.Show(this, "Save the document for the current item before leaving?", Text, MessageBoxButtons.YesNoCancel))
				{
				case DialogResult.Cancel:
					return;
				case DialogResult.Yes:
					SaveCurrent();
					break;
				}
			}
			Close();
		}

		private void OnPaintBar(object sender, PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			Theme.Quality(graphics);
			Rectangle rect = new Rectangle(0, 0, base.ClientSize.Width - 1, base.ClientSize.Height - 1);
			using (Pen pen = new Pen(Color.FromArgb(160, Theme.Neon)))
			{
				graphics.DrawRectangle(pen, rect);
			}
			using (Pen pen2 = new Pen(Theme.Neon, Theme.S(2f)))
			{
				float num = Theme.S(16f);
				graphics.DrawLine(pen2, 1f, 1f, num, 1f);
				graphics.DrawLine(pen2, 1f, 1f, 1f, num);
				graphics.DrawLine(pen2, rect.Right - 1, rect.Bottom - 1, (float)rect.Right - num, rect.Bottom - 1);
				graphics.DrawLine(pen2, rect.Right - 1, rect.Bottom - 1, rect.Right - 1, (float)rect.Bottom - num);
			}
			ShotItem cur = Cur;
			int num2 = Theme.S(16f);
			Theme.GlowDot(graphics, num2 + Theme.S(4f), Theme.S(26f), Theme.S(4f), Theme.Red, Anim.Pulse(1.2f));
			using (SolidBrush brush = new SolidBrush(Color.FromArgb(34, 211, 238)))
			{
				graphics.DrawString("REC  ·  " + (idx + 1) + " / " + queue.Count + "  ·  SCREENSHOT " + cur.N.ToString("00"), Fonts.Mono(9f, true), brush, num2 + Theme.S(16f), Theme.S(17f));
			}
			float num3 = base.ClientSize.Width - num2 - ((thumb != null) ? (thumb.Width + Theme.S(12f)) : 0);
			using (SolidBrush brush2 = new SolidBrush(Theme.White))
			{
				graphics.DrawString("“" + cur.Title + "”", Fonts.Mono(11f, true), brush2, new RectangleF(num2, Theme.S(48f), num3 - (float)num2, Theme.S(44f)));
			}
			float num4 = (float)Theme.S(48f) + Math.Min(Theme.S(44f), graphics.MeasureString("“" + cur.Title + "”", Fonts.Mono(11f, true), (int)(num3 - (float)num2)).Height) + (float)Theme.S(6f);
			float num5 = bOpen.Top - Theme.S(30f);
			if (!string.IsNullOrEmpty(cur.Node))
			{
				float num6 = base.ClientSize.Width - num2 * 2;
				Font font = Fonts.Sans(9f, true);
				float num7 = Theme.S(16f);
				float height = graphics.MeasureString(cur.Node, font, (int)(num6 - (float)Theme.S(20f))).Height;
				RectangleF r = new RectangleF(num2, num4, num6, num7 + height + (float)Theme.S(16f));
				using (GraphicsPath path = Theme.Round(r, Theme.S(6f)))
				{
					using (SolidBrush brush3 = new SolidBrush(Color.FromArgb(40, Theme.Neon)))
					{
						graphics.FillPath(brush3, path);
					}
					using (Pen pen3 = new Pen(Color.FromArgb(150, Theme.Neon)))
					{
						graphics.DrawPath(pen3, path);
					}
				}
				using (SolidBrush brush4 = new SolidBrush(Theme.Neon))
				{
					graphics.DrawString("▶ WHERE TO CLICK (the window is already open):", Fonts.Mono(8.5f, true), brush4, r.X + (float)Theme.S(10f), r.Y + (float)Theme.S(6f));
				}
				using (SolidBrush brush5 = new SolidBrush(Theme.White))
				{
					graphics.DrawString(cur.Node, font, brush5, new RectangleF(r.X + (float)Theme.S(10f), r.Y + (float)Theme.S(6f) + num7, num6 - (float)Theme.S(20f), height + 2f));
				}
				num4 = r.Bottom + (float)Theme.S(8f);
			}
			if (!string.IsNullOrEmpty(cur.Prereq))
			{
				float num8 = base.ClientSize.Width - num2 * 2;
				Font font2 = Fonts.Sans(9f);
				float num9 = Theme.S(16f);
				float height2 = graphics.MeasureString(cur.Prereq, font2, (int)(num8 - (float)Theme.S(20f))).Height;
				RectangleF r2 = new RectangleF(num2, num4, num8, num9 + height2 + (float)Theme.S(16f));
				using (GraphicsPath path2 = Theme.Round(r2, Theme.S(6f)))
				{
					using (SolidBrush brush6 = new SolidBrush(Color.FromArgb(40, Theme.Amber)))
					{
						graphics.FillPath(brush6, path2);
					}
					using (Pen pen4 = new Pen(Color.FromArgb(150, Theme.Amber)))
					{
						graphics.DrawPath(pen4, path2);
					}
				}
				using (SolidBrush brush7 = new SolidBrush(Theme.Amber))
				{
					graphics.DrawString("▸ FIRST DO THIS:", Fonts.Mono(8.5f, true), brush7, r2.X + (float)Theme.S(10f), r2.Y + (float)Theme.S(6f));
				}
				using (SolidBrush brush8 = new SolidBrush(Theme.Light(Theme.Amber)))
				{
					graphics.DrawString(cur.Prereq, font2, brush8, new RectangleF(r2.X + (float)Theme.S(10f), r2.Y + (float)Theme.S(6f) + num9, num8 - (float)Theme.S(20f), height2 + 2f));
				}
				num4 = r2.Bottom + (float)Theme.S(8f);
			}
			using (SolidBrush brush9 = new SolidBrush(Theme.Muted))
			{
				StringFormat stringFormat = new StringFormat();
				stringFormat.Trimming = StringTrimming.EllipsisWord;
				using (StringFormat format = stringFormat)
				{
					graphics.DrawString(cur.Hint, Fonts.Sans(9f), brush9, new RectangleF(num2, num4, num3 - (float)num2, Math.Max(10f, num5 - num4)), format);
				}
			}
			if (thumb != null)
			{
				Rectangle rect2 = new Rectangle(base.ClientSize.Width - num2 - thumb.Width, Theme.S(50f), thumb.Width, thumb.Height);
				graphics.DrawImage(thumb, rect2);
				using (Pen pen5 = new Pen(Color.FromArgb(180, Theme.Green)))
				{
					graphics.DrawRectangle(pen5, rect2);
				}
			}
			string s = flash ?? ("Screenshots: " + shots.Count + ((cur.Expect <= 1) ? "" : (" of " + cur.Expect)) + "   ·   F9 — capture the screen, the panel hides automatically");
			using (SolidBrush brush10 = new SolidBrush((flash == null) ? Theme.Dim : Theme.Green))
			{
				StringFormat stringFormat = new StringFormat();
				stringFormat.FormatFlags = StringFormatFlags.NoWrap;
				stringFormat.Trimming = StringTrimming.EllipsisCharacter;
				using (StringFormat format2 = stringFormat)
				{
					graphics.DrawString(s, Fonts.Mono(8.5f), brush10, new RectangleF(num2, bOpen.Top - Theme.S(24f), base.ClientSize.Width - num2 * 2, Theme.S(20f)), format2);
				}
			}
		}
	}
}
