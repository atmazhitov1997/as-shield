using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ASConfig
{
	internal class ShotsView : Canvas
	{
		private readonly MainForm main;

		private readonly string workDir;

		private readonly ShotSettings cfg;

		private readonly Card back;

		private readonly Card start;

		private readonly Card bAll;

		private readonly Card bNone;

		private readonly Card bMissing;

		private readonly Card bFolder;

		private readonly Card bOpen;

		private readonly List<Card> items = new List<Card>();

		private readonly TextBox admin = new TextBox();

		private readonly ToolTip tip = new ToolTip
		{
			AutoPopDelay = 20000,
			InitialDelay = 300
		};

		private Rectangle titleRect;

		private Rectangle setRect;

		private Rectangle listRect;

		public int Done => ShotCatalog.Items.Count((ShotItem i) => File.Exists(cfg.DocPath(i.N)));

		public ShotsView(MainForm main, string workDir)
		{
			ShotsView shotsView = this;
			this.main = main;
			this.workDir = workDir;
			cfg = new ShotSettings(workDir);
			base.Paint += OnPaintView;
			back = Btn("Back", "\ue72b", Theme.Neon, delegate
			{
				main.ShowHome();
			});
			start = Btn("Start capturing", "\ue722", Theme.Green, delegate
			{
				shotsView.StartSession();
			});
			bAll = Btn("Select all", "\ue73a", Theme.Neon, delegate
			{
				shotsView.SetAll(true);
			});
			bNone = Btn("Clear selection", "\ue739", Theme.Muted, delegate
			{
				shotsView.SetAll(false);
			});
			bMissing = Btn("Missing only", "\ue7ba", Theme.Amber, delegate
			{
				foreach (Card item in shotsView.items)
				{
					item.Selected = !File.Exists(shotsView.cfg.DocPath(((ShotItem)item.Tag).N));
				}
				shotsView.Refresh();
			});
			bFolder = Btn("Change", "\ue8da", Theme.Neon, delegate
			{
				shotsView.ChooseFolder();
			});
			bOpen = Btn("Open", "\ue838", Theme.Neon, delegate
			{
				Directory.CreateDirectory(shotsView.cfg.Folder);
				Process.Start("explorer.exe", "\"" + shotsView.cfg.Folder + "\"");
			});
			admin.BorderStyle = BorderStyle.FixedSingle;
			admin.BackColor = Theme.Card;
			admin.ForeColor = Theme.White;
			admin.Font = Fonts.Sans(11f);
			admin.Text = cfg.Admin;
			admin.TextChanged += delegate
			{
				shotsView.cfg.Admin = shotsView.admin.Text;
				shotsView.cfg.Save();
			};
			base.Controls.Add(admin);
			foreach (ShotItem item2 in ShotCatalog.Items)
			{
				Card c = new Card
				{
					Title = item2.N.ToString("00") + "  " + item2.Title,
					Icon = ((!item2.NoCapture) ? "\ue91b" : "\ue722"),
					Accent = Theme.Neon,
					Tag = item2,
					Selected = true
				};
				c.Click += delegate
				{
					c.Selected = !c.Selected;
					c.Invalidate();
					shotsView.Invalidate(shotsView.setRect);
				};
				tip.SetToolTip(c, item2.N + ". " + item2.Title + "\n\n" + item2.Hint);
				items.Add(c);
				base.Controls.Add(c);
			}
		}

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

		private void SetAll(bool v)
		{
			foreach (Card item in items)
			{
				item.Selected = v;
			}
			Refresh();
		}

		public new void Refresh()
		{
			foreach (Card item in items)
			{
				ShotItem shotItem = (ShotItem)item.Tag;
				item.Badge = ((!File.Exists(cfg.DocPath(shotItem.N))) ? null : "DONE");
				item.Invalidate();
			}
			Invalidate();
		}

		private void ChooseFolder()
		{
			FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
			folderBrowserDialog.Description = "Where to save the screenshots";
			folderBrowserDialog.SelectedPath = ((!Directory.Exists(cfg.Folder)) ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) : cfg.Folder);
			using (FolderBrowserDialog folderBrowserDialog2 = folderBrowserDialog)
			{
				if (folderBrowserDialog2.ShowDialog(FindForm()) == DialogResult.OK)
				{
					cfg.Folder = folderBrowserDialog2.SelectedPath;
					cfg.Save();
					Refresh();
				}
			}
		}

		private void StartSession()
		{
			List<ShotItem> list = (from c in items
				where c.Selected
				select (ShotItem)c.Tag into i
				orderby i.CaptureOrder
				select i).ToList();
			if (list.Count == 0)
			{
				MessageBox.Show(FindForm(), "Select at least one screenshot.", "Evidence screenshots");
			}
			else
			{
				if (string.IsNullOrWhiteSpace(cfg.Admin) && MessageBox.Show(FindForm(), "The system administrator's name is not set — the documents will have an empty signature line. Continue?", "Evidence screenshots", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
				{
					return;
				}
				try
				{
					Directory.CreateDirectory(cfg.Folder);
					Directory.CreateDirectory(cfg.PngDir);
				}
				catch (Exception ex)
				{
					MessageBox.Show(FindForm(), "Could not create the folder:\n" + ex.Message, "Evidence screenshots");
					return;
				}
				Form form = FindForm();
				CaptureBar bar = new CaptureBar(list, cfg, workDir);
				bar.FormClosed += delegate
				{
					form.WindowState = FormWindowState.Maximized;
					form.Show();
					form.Activate();
					Refresh();
					main.SetStatus("Documents saved: " + bar.Saved + " → " + cfg.Folder, Theme.Green, false);
					if (bar.Saved > 0 && MessageBox.Show(form, "Done. Documents saved: " + bar.Saved + ".\nOpen the screenshots folder?", "Evidence screenshots", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk) == DialogResult.Yes)
					{
						Process.Start("explorer.exe", "\"" + cfg.Folder + "\"");
					}
				};
				form.WindowState = FormWindowState.Minimized;
				bar.Show();
			}
		}

		public void DoLayout()
		{
			int width = base.Width;
			int height = base.Height;
			if (width > 0 && height > 0)
			{
				int num = Theme.S(32f);
				int num2 = Theme.S(20f);
				int num3 = Theme.S(46f);
				back.SetBounds(num, num2, Theme.S(136f), num3);
				start.SetBounds(width - num - Theme.S(220f), num2, Theme.S(220f), num3);
				titleRect = new Rectangle(back.Right + Theme.S(20f), num2, start.Left - back.Right - Theme.S(40f), num3);
				int num4 = num2 + num3 + Theme.S(24f);
				int num5 = Theme.S(360f);
				setRect = new Rectangle(num, num4, num5, height - num4 - Theme.S(24f));
				int num6 = num + Theme.S(18f);
				int num7 = num5 - Theme.S(36f);
				admin.SetBounds(num6, num4 + Theme.S(66f), num7, admin.Height);
				int num8 = num4 + Theme.S(66f) + admin.Height + Theme.S(70f);
				bFolder.SetBounds(num6, num8, (num7 - Theme.S(8f)) / 2, Theme.S(40f));
				bOpen.SetBounds(num6 + (num7 + Theme.S(8f)) / 2, num8, (num7 - Theme.S(8f)) / 2, Theme.S(40f));
				num8 += Theme.S(84f);
				bAll.SetBounds(num6, num8, num7, Theme.S(40f));
				num8 += Theme.S(48f);
				bNone.SetBounds(num6, num8, num7, Theme.S(40f));
				num8 += Theme.S(48f);
				bMissing.SetBounds(num6, num8, num7, Theme.S(40f));
				int num9 = num + num5 + Theme.S(24f);
				int num10 = width - num9 - num;
				listRect = new Rectangle(num9, num4, num10, Theme.S(30f));
				int num11 = num4 + Theme.S(42f);
				int num12 = Theme.S(8f);
				int num13 = (items.Count + 1) / 2;
				int num14 = Math.Max(Theme.S(36f), Math.Min(Theme.S(50f), (height - num11 - Theme.S(24f) - num12 * (num13 - 1)) / num13));
				int num15 = (num10 - num12) / 2;
				for (int i = 0; i < items.Count; i++)
				{
					int num16 = i / num13;
					int num17 = i % num13;
					items[i].SetBounds(num9 + num16 * (num15 + num12), num11 + num17 * (num14 + num12), num15, num14);
				}
				Invalidate();
			}
		}

		private void OnPaintView(object sender, PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			using (SolidBrush brush = new SolidBrush(Theme.White))
			{
				StringFormat stringFormat = new StringFormat();
				stringFormat.FormatFlags = StringFormatFlags.NoWrap;
				stringFormat.Trimming = StringTrimming.EllipsisCharacter;
				using (StringFormat format = stringFormat)
				{
					graphics.DrawString("Evidence screenshots", Fonts.Mono(15f, true), brush, new RectangleF(titleRect.X, titleRect.Y - Theme.S(6f), titleRect.Width, Theme.S(34f)), format);
				}
			}
			int num = items.Count((Card c) => c.Selected);
			using (SolidBrush brush2 = new SolidBrush(Theme.Muted))
			{
				graphics.DrawString("Selected: " + num + " of " + items.Count + "  ·  done: " + Done + "  ·  F9 — capture the screen", Fonts.Mono(9f), brush2, titleRect.X + Theme.S(2f), titleRect.Y + Theme.S(26f));
			}
			RectangleF r = new RectangleF(setRect.X, setRect.Y, setRect.Width, Math.Min(setRect.Height, Theme.S(470f)));
			using (GraphicsPath path = Theme.Round(r, Theme.S(8f)))
			{
				using (SolidBrush brush3 = new SolidBrush(Color.FromArgb(200, 9, 16, 36)))
				{
					graphics.FillPath(brush3, path);
				}
				using (Pen pen = new Pen(Color.FromArgb(150, Theme.Border)))
				{
					graphics.DrawPath(pen, path);
				}
			}
			float x = r.X + (float)Theme.S(18f);
			using (SolidBrush brush4 = new SolidBrush(Color.FromArgb(34, 211, 238)))
			{
				graphics.DrawString("// SYSTEM ADMINISTRATOR NAME", Fonts.Mono(8.5f, true), brush4, x, r.Y + (float)Theme.S(20f));
				graphics.DrawString("// OUTPUT FOLDER", Fonts.Mono(8.5f, true), brush4, x, admin.Bottom + Theme.S(16f));
				graphics.DrawString("// SELECTION", Fonts.Mono(8.5f, true), brush4, x, bFolder.Bottom + Theme.S(18f));
			}
			using (SolidBrush brush5 = new SolidBrush(Theme.Dim))
			{
				graphics.DrawString("for the signature at the bottom of every document", Fonts.Sans(8.5f), brush5, x, r.Y + (float)Theme.S(40f));
			}
			using (SolidBrush brush6 = new SolidBrush(Theme.Text))
			{
				StringFormat stringFormat = new StringFormat();
				stringFormat.Trimming = StringTrimming.EllipsisPath;
				stringFormat.FormatFlags = StringFormatFlags.NoWrap;
				using (StringFormat format2 = stringFormat)
				{
					graphics.DrawString(cfg.Folder, Fonts.Sans(9f), brush6, new RectangleF(x, admin.Bottom + Theme.S(36f), r.Width - (float)Theme.S(36f), Theme.S(20f)), format2);
				}
			}
			MainForm.DrawSection(graphics, listRect, "SCREENSHOTS", "click to toggle · hover for a hint", Theme.Neon);
		}
	}
}
