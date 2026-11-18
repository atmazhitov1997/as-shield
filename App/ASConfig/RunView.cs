using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace ASConfig
{
	internal class RunView : Canvas
	{
		private readonly MainForm main;

		private readonly RichTextBox log = new RichTextBox();

		private readonly Label promptLbl = new Label();

		private readonly TextBox input = new TextBox();

		private readonly Card send;

		private readonly Card stop;

		private readonly Card back;

		private readonly Card copy;

		private readonly List<Card> choiceBtns = new List<Card>();

		private Process proc;

		private StreamWriter stdin;

		private string title = "";

		private string mode;

		private bool secure;

		private bool running;

		private int exitCode;

		private readonly StringBuilder recent = new StringBuilder();

		private bool lineOpen;

		private Rectangle titleRect;

		private Rectangle logRect;

		private Rectangle askRect;

		private Rectangle progRect;

		private int progPct = -1;

		private string progLabel = "";

		private int shotN;

		public bool Running => running;

		public event Action<int> Finished;

		public RunView(MainForm main)
		{
			RunView runView = this;
			this.main = main;
			BackColor = Theme.Bg;
			base.Paint += OnPaintView;
			Anim.Tick += delegate
			{
				if (runView.Visible && (runView.running || runView.mode != null))
				{
					runView.Invalidate(new Rectangle(runView.titleRect.X, runView.titleRect.Y, Theme.S(240f), runView.titleRect.Height));
				}
				if (runView.Visible && runView.mode != null)
				{
					runView.Invalidate(Rectangle.Inflate(runView.askRect, Theme.S(14f), Theme.S(14f)), false);
				}
				if (runView.Visible && runView.progPct != -1)
				{
					runView.Invalidate(Rectangle.Inflate(runView.progRect, Theme.S(4f), Theme.S(4f)), false);
				}
			};
			log.ReadOnly = true;
			log.BorderStyle = BorderStyle.None;
			log.BackColor = Color.FromArgb(7, 12, 27);
			log.ForeColor = Theme.Text;
			log.Font = Fonts.MonoGdi(10f);
			log.WordWrap = false;
			log.DetectUrls = false;
			log.ScrollBars = RichTextBoxScrollBars.Both;
			base.Controls.Add(log);
			promptLbl.ForeColor = Theme.Text;
			promptLbl.BackColor = Theme.Header;
			promptLbl.Font = Fonts.MonoGdi(10.5f, true);
			base.Controls.Add(promptLbl);
			input.BorderStyle = BorderStyle.FixedSingle;
			input.BackColor = Theme.Card;
			input.ForeColor = Theme.Text;
			input.Font = new Font("Segoe UI", 12f);
			input.KeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.KeyCode == Keys.Return)
				{
					e.SuppressKeyPress = true;
					runView.Answer(runView.input.Text);
				}
			};
			base.Controls.Add(input);
			send = MakeBtn("Reply", "\ue724", Theme.Blue, delegate
			{
				runView.Answer((!(runView.mode == "pause")) ? runView.input.Text : "");
			});
			stop = MakeBtn("Stop", "\ue71a", Theme.Red, delegate
			{
				runView.Stop();
			});
			back = MakeBtn("Back", "\ue72b", Theme.Blue, delegate
			{
				main.CloseRun();
			});
			copy = MakeBtn("", "\ue8c8", Theme.Muted, delegate
			{
				try
				{
					Clipboard.SetText(runView.log.Text);
					main.SetStatus("Log copied to the clipboard.", Theme.Green, runView.running);
				}
				catch
				{
				}
			});
			new ToolTip().SetToolTip(copy, "Copy the whole log");
			SetAsk(null);
		}

		private Card MakeBtn(string t, string icon, Color c, EventHandler click)
		{
			Card card = new Card();
			card.Title = t;
			card.Icon = icon;
			card.Accent = c;
			card.BackColor = Theme.Bg;
			Card card2 = card;
			card2.Click += click;
			base.Controls.Add(card2);
			return card2;
		}

		public void Start(string title, string script, string args)
		{
			this.title = title;
			log.Clear();
			recent.Clear();
			lineOpen = false;
			exitCode = 0;
			progPct = -1;
			progLabel = "";
			SetAsk(null);
			Append("root@as-config:~$ run \"" + title + "\"\n\n", Theme.Neon);
			string fileName = "powershell.exe";
			try
			{
				string text = Path.Combine(Environment.GetEnvironmentVariable("WINDIR") ?? "C:\\Windows", "Sysnative\\WindowsPowerShell\\v1.0\\powershell.exe");
				if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess && File.Exists(text))
				{
					fileName = text;
				}
			}
			catch
			{
			}
			ProcessStartInfo processStartInfo = new ProcessStartInfo(fileName, "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\" " + args);
			processStartInfo.WorkingDirectory = Path.GetDirectoryName(script);
			processStartInfo.UseShellExecute = false;
			processStartInfo.CreateNoWindow = true;
			processStartInfo.RedirectStandardInput = true;
			processStartInfo.RedirectStandardOutput = true;
			processStartInfo.RedirectStandardError = true;
			processStartInfo.StandardOutputEncoding = new UTF8Encoding(false);
			processStartInfo.StandardErrorEncoding = new UTF8Encoding(false);
			ProcessStartInfo processStartInfo2 = processStartInfo;
			try
			{
				processStartInfo2.EnvironmentVariables["ASEXE"] = Application.ExecutablePath;
			}
			catch
			{
			}
			proc = new Process
			{
				StartInfo = processStartInfo2
			};
			proc.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
			{
				if (e.Data != null)
				{
					Post(e.Data, false);
				}
			};
			proc.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
			{
				if (e.Data != null)
				{
					Post(e.Data, true);
				}
			};
			proc.Start();
			stdin = new StreamWriter(proc.StandardInput.BaseStream, new UTF8Encoding(false))
			{
				AutoFlush = true,
				NewLine = "\r\n"
			};
			proc.BeginOutputReadLine();
			proc.BeginErrorReadLine();
			running = true;
			Process p = proc;
			Thread thread = new Thread((ThreadStart)delegate
			{
				try
				{
					p.WaitForExit();
				}
				catch
				{
				}
				int code = -1;
				try
				{
					code = p.ExitCode;
				}
				catch
				{
				}
				try
				{
					BeginInvoke((MethodInvoker)delegate
					{
						OnExit(code);
					});
				}
				catch
				{
				}
			});
			thread.IsBackground = true;
			thread.Start();
			UpdateButtons();
			DoLayout();
		}

		private void Post(string line, bool err)
		{
			try
			{
				BeginInvoke((MethodInvoker)delegate
				{
					OnLine(line, err);
				});
			}
			catch
			{
			}
		}

		private void OnExit(int code)
		{
			running = false;
			exitCode = code;
			progPct = -1;
			progLabel = "";
			SetAsk(null);
			if (lineOpen)
			{
				Append("\n", Theme.Text);
			}
			Append("\n" + ((code != 0) ? ("[FAIL] The process exited with code " + code + ".") : "[ OK ] Finished.") + "\n", (code != 0) ? Theme.Amber : Theme.Green);
			UpdateButtons();
			DoLayout();
			if (this.Finished != null)
			{
				this.Finished(code);
			}
		}

		private void Stop()
		{
			if (running && proc != null && MessageBox.Show(FindForm(), "Stop the run?\nSome settings may already have been applied.", "AS Shield", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
			{
				try
				{
					proc.Kill();
				}
				catch
				{
				}
				Append("\n[STOP] Stopped by the user.\n", Theme.Red);
			}
		}

		public bool ConfirmClose()
		{
			if (!running)
			{
				return true;
			}
			if (MessageBox.Show(FindForm(), "A script is still running. Stop it and close the program?", "AS Shield", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != DialogResult.Yes)
			{
				return false;
			}
			try
			{
				proc.Kill();
			}
			catch
			{
			}
			return true;
		}

		private void OnLine(string line, bool err)
		{
			line = line.TrimStart(new char[1] { '\ufeff' });
			line = Regex.Replace(line, "\u001b\\[[0-9;]*[A-Za-z]", "");
			if (line.Length > 0 && line[0] == '\u0001')
			{
				string[] array = line.Substring(1).Split(new char[1] { '|' });
				switch (array[0])
				{
				case "H":
				{
					if (array.Length < 4)
					{
						break;
					}
					string text2 = Dec(array[3]);
					bool flag2 = array[2] == "1";
					Append(text2 + ((!flag2) ? "\n" : ""), Map(array[1]));
					lineOpen = flag2;
					recent.Append(text2 + ((!flag2) ? "\n" : ""));
					return;
				}
				case "A":
					if (array.Length < 3)
					{
						break;
					}
					if (lineOpen)
					{
						Append("\n", Theme.Text);
						lineOpen = false;
					}
					secure = array[1] == "1";
					ShowAsk("ask", Dec(array[2]));
					return;
				case "P":
					if (lineOpen)
					{
						Append("\n", Theme.Text);
						lineOpen = false;
					}
					ShowAsk("pause", "");
					return;
				case "C":
					if (log.TextLength > 0)
					{
						Append("\n" + new string('─', 60) + "\n\n", Theme.Border);
					}
					recent.Clear();
					return;
				case "S":
				{
					if (lineOpen)
					{
						Append("\n", Theme.Text);
						lineOpen = false;
					}
					int result2 = 0;
					if (array.Length >= 2)
					{
						int.TryParse(array[1], out result2);
					}
					shotN = result2;
					ShowShotAsk(result2, (array.Length < 3) ? "" : Dec(array[2]));
					return;
				}
				case "G":
				{
					int result = -1;
					if (array.Length >= 2)
					{
						int.TryParse(array[1], out result);
					}
					string text = ((array.Length < 3) ? progLabel : Dec(array[2]));
					bool flag = progPct == -1 != (result == -1);
					progPct = ((result < -2) ? (-2) : ((result <= 100) ? result : 100));
					progLabel = text;
					if (flag)
					{
						DoLayout();
					}
					else
					{
						Invalidate(Rectangle.Inflate(progRect, Theme.S(6f), Theme.S(6f)));
					}
					return;
				}
				}
			}
			if (lineOpen)
			{
				Append("\n", Theme.Text);
				lineOpen = false;
			}
			Append(line + "\n", (!err) ? Theme.Muted : Color.FromArgb(248, 113, 113));
			recent.Append(line + "\n");
		}

		private static string Dec(string b64)
		{
			try
			{
				return Encoding.UTF8.GetString(Convert.FromBase64String(b64));
			}
			catch
			{
				return b64;
			}
		}

		private static Color Map(string c)
		{
			switch ((c ?? "").ToLowerInvariant())
			{
			case "cyan":
				return Color.FromArgb(34, 211, 238);
			case "darkcyan":
				return Color.FromArgb(20, 184, 166);
			case "yellow":
				return Color.FromArgb(250, 204, 21);
			case "darkyellow":
				return Color.FromArgb(217, 119, 6);
			case "green":
				return Color.FromArgb(74, 222, 128);
			case "darkgreen":
				return Color.FromArgb(34, 160, 90);
			case "red":
				return Color.FromArgb(248, 113, 113);
			case "darkred":
				return Color.FromArgb(220, 38, 38);
			case "magenta":
				return Color.FromArgb(232, 121, 249);
			case "darkmagenta":
				return Color.FromArgb(168, 85, 247);
			case "blue":
				return Color.FromArgb(96, 165, 250);
			case "darkblue":
				return Color.FromArgb(59, 130, 246);
			case "gray":
				return Color.FromArgb(148, 163, 184);
			case "darkgray":
				return Color.FromArgb(100, 116, 139);
			case "black":
				return Color.FromArgb(71, 85, 105);
			default:
				return Theme.Text;
			}
		}

		private void Append(string text, Color c)
		{
			log.SelectionStart = log.TextLength;
			log.SelectionLength = 0;
			log.SelectionColor = c;
			log.AppendText(text);
			log.SelectionStart = log.TextLength;
			log.ScrollToCaret();
		}

		private void ShowAsk(string m, string prompt)
		{
			mode = m;
			string text = recent.ToString();
			recent.Clear();
			foreach (Card choiceBtn in choiceBtns)
			{
				base.Controls.Remove(choiceBtn);
				choiceBtn.Dispose();
			}
			choiceBtns.Clear();
			if (m == "pause")
			{
				promptLbl.Text = "Press 'Continue' to go to the next step.";
				send.Title = "Continue";
				send.Icon = "\ue768";
			}
			else
			{
				promptLbl.Text = ((!string.IsNullOrWhiteSpace(prompt)) ? prompt.Trim() : "Enter a value:");
				send.Title = "Reply";
				send.Icon = "\ue724";
				if (!secure)
				{
					foreach (KeyValuePair<string, string> item in ParseChoices(text + "\n" + prompt).Take(10))
					{
						string text2 = ((item.Value.Length <= 48) ? item.Value : (item.Value.Substring(0, 47) + "…"));
						string key = item.Key;
						Card card = new Card();
						card.Title = key + "   " + text2;
						card.Accent = Theme.UaYel;
						card.BackColor = Theme.Header;
						Card card2 = card;
						card2.Click += delegate
						{
							Answer(key);
						};
						base.Controls.Add(card2);
						card2.BringToFront();
						choiceBtns.Add(card2);
					}
				}
			}
			input.UseSystemPasswordChar = secure;
			input.Text = "";
			SetAsk(m);
			DoLayout();
			if (m == "ask")
			{
				input.Focus();
			}
			else
			{
				send.Focus();
			}
			main.SetStatus((!(m == "pause")) ? "The script is waiting for your answer." : "Waiting for confirmation — 'Continue'.", Theme.UaYel, true);
			FlashTaskbar();
		}

		private void SetAsk(string m)
		{
			mode = m;
			bool visible = m == "ask";
			promptLbl.Visible = m != null;
			input.Visible = visible;
			send.Visible = m != null && m != "shot";
			if (m != null)
			{
				return;
			}
			foreach (Card choiceBtn in choiceBtns)
			{
				choiceBtn.Visible = false;
			}
		}

		private void ShowShotAsk(int n, string title)
		{
			foreach (Card choiceBtn in choiceBtns)
			{
				base.Controls.Remove(choiceBtn);
				choiceBtn.Dispose();
			}
			choiceBtns.Clear();
			promptLbl.Text = "Step applied" + ((!string.IsNullOrEmpty(title)) ? (": " + title) : "") + ".\nTake an evidence screenshot for the report (item " + n + ")?";
			Card card = new Card();
			card.Title = "\ue722   Take screenshot";
			card.Accent = Theme.Neon;
			card.BackColor = Theme.Header;
			Card card2 = card;
			card2.Click += delegate
			{
				Append("> Take screenshot (item " + shotN + ")\n", Theme.Amber);
				try
				{
					main.CaptureSingleShot(shotN);
				}
				catch
				{
				}
				ResumeAfterShot();
			};
			card = new Card();
			card.Title = "Skip";
			card.Accent = Theme.Muted;
			card.BackColor = Theme.Header;
			Card card3 = card;
			card3.Click += delegate
			{
				Append("> Skip\n", Theme.Amber);
				ResumeAfterShot();
			};
			base.Controls.Add(card2);
			base.Controls.Add(card3);
			card2.BringToFront();
			card3.BringToFront();
			choiceBtns.Add(card2);
			choiceBtns.Add(card3);
			SetAsk("shot");
			DoLayout();
			main.SetStatus("Step applied — take an evidence screenshot or skip.", Theme.UaYel, true);
			FlashTaskbar();
		}

		private void ResumeAfterShot()
		{
			try
			{
				if (stdin != null)
				{
					stdin.WriteLine("");
				}
			}
			catch
			{
			}
			foreach (Card choiceBtn in choiceBtns)
			{
				base.Controls.Remove(choiceBtn);
				choiceBtn.Dispose();
			}
			choiceBtns.Clear();
			SetAsk(null);
			DoLayout();
			main.SetStatus("Running '" + title + "'…", Theme.Amber, true);
		}

		private void Answer(string a)
		{
			if (mode == null || stdin == null)
			{
				return;
			}
			string text = ((mode == "pause") ? "Continue" : (secure ? new string('•', Math.Max(4, a.Length)) : ((a.Length != 0) ? a : "(empty)")));
			Append("> " + text + "\n", Theme.Amber);
			try
			{
				stdin.WriteLine(a);
			}
			catch (Exception ex)
			{
				Append("Could not send the answer: " + ex.Message + "\n", Theme.Red);
			}
			secure = false;
			foreach (Card choiceBtn in choiceBtns)
			{
				base.Controls.Remove(choiceBtn);
				choiceBtn.Dispose();
			}
			choiceBtns.Clear();
			SetAsk(null);
			DoLayout();
			main.SetStatus("Running '" + title + "'…", Theme.Amber, true);
		}

		private static List<KeyValuePair<string, string>> ParseChoices(string text)
		{
			List<KeyValuePair<string, string>> res = new List<KeyValuePair<string, string>>();
			Action<string, string> action = delegate(string k, string l)
			{
				k = k.ToUpperInvariant();
				if (!res.Any((KeyValuePair<string, string> x) => x.Key == k))
				{
					l = Regex.Replace(l ?? "", "\\s+", " ");
					l = Regex.Replace(l, "(?i)\\b(for|enter|choose|select)\\b", "");
					l = l.Trim(' ', '-', ':', ',', '.', '–', '—', '\'', '"', '`');
					if (l.Length > 0)
					{
						l = char.ToUpper(l[0]) + l.Substring(1);
					}
					res.Add(new KeyValuePair<string, string>(k, (l.Length != 0) ? l : k));
				}
			};
			if (Regex.IsMatch(text, "\\(\\s*Y\\s*/\\s*N\\s*\\)", RegexOptions.IgnoreCase))
			{
				action("Y", "Yes");
				action("N", "No");
			}
			string[] array = text.Replace("\r", "").Split(new char[1] { '\n' });
			foreach (string text2 in array)
			{
				string text3 = Regex.Replace(text2, "^[\\s│#\\-─|!]+|[\\s│#\\-─|!]+$", "");
				if (text3.Length == 0)
				{
					continue;
				}
				Match match = Regex.Match(text3, "^(\\d{1,2})[\\.\\)]\\s+(.+)$");
				if (match.Success)
				{
					action(match.Groups[1].Value, match.Groups[2].Value);
					continue;
				}
				string[] array2 = Regex.Split(text3, "(?<=[\\)\\]'])\\s*,\\s*");
				foreach (string text4 in array2)
				{
					Match match2 = Regex.Match(text4, "[\\(\\['](?<k>[0-9A-Za-z])[\\)\\]']");
					if (match2.Success)
					{
						string text5 = text4.Remove(match2.Index, match2.Length);
						text5 = Regex.Replace(text5, "\\(\\s*\\)|\\[\\s*\\]", "");
						action(match2.Groups["k"].Value, text5);
					}
				}
			}
			return res;
		}

		[DllImport("user32.dll")]
		private static extern bool FlashWindow(IntPtr h, bool invert);

		private void FlashTaskbar()
		{
			try
			{
				Form form = FindForm();
				if (form != null && !form.ContainsFocus)
				{
					FlashWindow(form.Handle, true);
				}
			}
			catch
			{
			}
		}

		private void UpdateButtons()
		{
			stop.Visible = running;
			back.Visible = !running;
			copy.Visible = true;
		}

		public void DoLayout()
		{
			int width = base.Width;
			int height = base.Height;
			if (width <= 0 || height <= 0)
			{
				return;
			}
			int num = Theme.S(32f);
			int num2 = Theme.S(20f);
			int num3 = Theme.S(46f);
			stop.SetBounds(width - num - Theme.S(180f), num2, Theme.S(180f), num3);
			back.SetBounds(width - num - Theme.S(190f), num2, Theme.S(190f), num3);
			copy.SetBounds(((!running) ? back.Left : stop.Left) - Theme.S(10f) - num3, num2, num3, num3);
			titleRect = new Rectangle(num, num2, copy.Left - num - Theme.S(20f), num3);
			int num4 = num2 + num3 + Theme.S(20f);
			int num5 = 0;
			if (mode != null)
			{
				int num6 = width - num * 2 - Theme.S(40f);
				Size size = TextRenderer.MeasureText(promptLbl.Text, promptLbl.Font, new Size(num6, 1000), TextFormatFlags.WordBreak);
				int num7 = Math.Min(Theme.S(120f), size.Height + Theme.S(4f));
				int num8 = 0;
				int num9 = 0;
				int num10 = Theme.S(40f);
				int num11 = 0;
				List<Rectangle> list = new List<Rectangle>();
				using (Graphics graphics = CreateGraphics())
				{
					using (Font font = Fonts.NewMono(9.5f, true))
					{
						foreach (Card choiceBtn in choiceBtns)
						{
							int num12 = Math.Min(num6, (int)graphics.MeasureString(choiceBtn.Title, font).Width + Theme.S(44f));
							if (num9 > 0 && num9 + num12 > num6)
							{
								num9 = 0;
								num8 += num10 + Theme.S(8f);
							}
							list.Add(new Rectangle(num9, num8, num12, num10));
							num9 += num12 + Theme.S(8f);
							num11 = num8 + num10;
						}
					}
				}
				int num13 = Theme.S(44f);
				num5 = Theme.S(20f) + num7 + Theme.S(12f) + ((choiceBtns.Count > 0) ? (num11 + Theme.S(12f)) : 0) + num13 + Theme.S(20f);
				askRect = new Rectangle(num, height - Theme.S(20f) - num5, width - num * 2, num5);
				int num14 = askRect.X + Theme.S(20f);
				int num15 = askRect.Y + Theme.S(20f);
				promptLbl.SetBounds(num14, num15, num6, num7);
				num15 += num7 + Theme.S(12f);
				for (int i = 0; i < choiceBtns.Count; i++)
				{
					Rectangle rectangle = list[i];
					choiceBtns[i].SetBounds(num14 + rectangle.X, num15 + rectangle.Y, rectangle.Width, rectangle.Height);
					choiceBtns[i].Visible = true;
				}
				if (choiceBtns.Count > 0)
				{
					num15 += num11 + Theme.S(12f);
				}
				int num16 = Theme.S(190f);
				if (mode == "ask")
				{
					input.SetBounds(num14, num15 + (num13 - input.Height) / 2, num6 - num16 - Theme.S(12f), input.Height);
					send.SetBounds(num14 + num6 - num16, num15, num16, num13);
				}
				else
				{
					send.SetBounds(num14, num15, num16, num13);
				}
				num5 += Theme.S(16f);
			}
			int num17 = ((progPct != -1) ? Theme.S(58f) : 0);
			logRect = new Rectangle(num, num4, width - num * 2, height - num4 - Theme.S(20f) - num5 - num17);
			log.SetBounds(logRect.X + Theme.S(16f), logRect.Y + Theme.S(12f), logRect.Width - Theme.S(24f), logRect.Height - Theme.S(20f));
			if (num17 > 0)
			{
				progRect = new Rectangle(num, logRect.Bottom + Theme.S(12f), width - num * 2, Theme.S(44f));
			}
			UpdateButtons();
			Invalidate();
		}

		private void OnPaintView(object sender, PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			Color color = (running ? ((mode == null) ? Theme.Amber : Theme.UaYel) : ((exitCode != 0) ? Theme.Red : Theme.Green));
			string s = (running ? ((mode == null) ? "Running" : "Waiting for an answer") : ((exitCode != 0) ? "Finished with errors" : "Finished"));
			using (Font font2 = Fonts.NewMono(15f, true))
			{
				using (Font font = Fonts.NewMono(9f, true))
				{
					using (SolidBrush brush3 = new SolidBrush(Theme.Text))
					{
						StringFormat stringFormat = new StringFormat();
						stringFormat.Trimming = StringTrimming.EllipsisCharacter;
						stringFormat.FormatFlags = StringFormatFlags.NoWrap;
						stringFormat.LineAlignment = StringAlignment.Center;
						using (StringFormat format = stringFormat)
						{
							SizeF sizeF = graphics.MeasureString(s, font);
							RectangleF r = new RectangleF(titleRect.X, (float)titleRect.Y + (float)(titleRect.Height - Theme.S(30f)) / 2f, sizeF.Width + (float)Theme.S(36f), Theme.S(30f));
							using (GraphicsPath path = Theme.Round(r, r.Height / 2f))
							{
								using (SolidBrush brush = new SolidBrush(Color.FromArgb(35, color)))
								{
									graphics.FillPath(brush, path);
								}
								using (Pen pen = new Pen(Color.FromArgb(120, color)))
								{
									graphics.DrawPath(pen, path);
								}
							}
							Theme.GlowDot(graphics, r.X + (float)Theme.S(16f), r.Y + r.Height / 2f, Theme.S(4f), color, (!running) ? 1f : Anim.Pulse(1.2f));
							using (SolidBrush brush2 = new SolidBrush(color))
							{
								graphics.DrawString(s, font, brush2, new RectangleF(r.X + (float)Theme.S(26f), r.Y, r.Width, r.Height), format);
							}
							graphics.DrawString(title, font2, brush3, new RectangleF(r.Right + (float)Theme.S(16f), titleRect.Y, (float)titleRect.Right - r.Right - (float)Theme.S(16f), titleRect.Height), format);
						}
					}
				}
			}
			DrawBox(graphics, logRect, Color.FromArgb(7, 12, 27), Color.FromArgb(150, Theme.Border));
			Hud(graphics, logRect, Theme.Neon);
			if (progPct != -1)
			{
				DrawProgress(graphics);
			}
			if (mode != null)
			{
				Theme.GlowRect(graphics, askRect, Theme.S(12f), Theme.Amber, 0.5f + 0.5f * Anim.Pulse(), 4);
				DrawBox(graphics, askRect, Theme.Header, Theme.Amber);
				Hud(graphics, askRect, Theme.Amber);
			}
		}

		private void DrawProgress(Graphics g)
		{
			Rectangle rectangle = progRect;
			if (rectangle.Width <= 0 || rectangle.Height <= 0)
			{
				return;
			}
			int num = Theme.S(12f);
			int num2 = rectangle.Height - num - Theme.S(6f);
			Rectangle rectangle2 = new Rectangle(rectangle.X, rectangle.Y + num2 + Theme.S(6f), rectangle.Width, num);
			using (Font font = Fonts.NewMono(9.5f, true))
			{
				StringFormat stringFormat = new StringFormat();
				stringFormat.Trimming = StringTrimming.EllipsisCharacter;
				stringFormat.FormatFlags = StringFormatFlags.NoWrap;
				stringFormat.LineAlignment = StringAlignment.Center;
				using (StringFormat format = stringFormat)
				{
					using (SolidBrush brush = new SolidBrush(Theme.Dim))
					{
						g.DrawString((!string.IsNullOrEmpty(progLabel)) ? progLabel : "Scanning…", font, brush, new RectangleF(rectangle.X, rectangle.Y, rectangle.Width - Theme.S(60f), num2), format);
					}
					if (progPct >= 0)
					{
						using (SolidBrush brush2 = new SolidBrush(Theme.Neon))
						{
							stringFormat = new StringFormat();
							stringFormat.Alignment = StringAlignment.Far;
							stringFormat.LineAlignment = StringAlignment.Center;
							using (StringFormat format2 = stringFormat)
							{
								g.DrawString(progPct + "%", font, brush2, new RectangleF(rectangle.Right - Theme.S(58f), rectangle.Y, Theme.S(58f), num2), format2);
							}
						}
					}
				}
			}
			using (GraphicsPath path = Theme.Round(new RectangleF(rectangle2.X, rectangle2.Y, rectangle2.Width, rectangle2.Height), (float)num / 2f))
			{
				using (SolidBrush brush3 = new SolidBrush(Color.FromArgb(16, 26, 48)))
				{
					g.FillPath(brush3, path);
				}
				using (Pen pen = new Pen(Color.FromArgb(120, Theme.Border)))
				{
					g.DrawPath(pen, path);
				}
			}
			Region clip = g.Clip;
			using (GraphicsPath clip2 = Theme.Round(new RectangleF(rectangle2.X, rectangle2.Y, rectangle2.Width, rectangle2.Height), (float)num / 2f))
			{
				g.SetClip(clip2);
				if (progPct >= 0)
				{
					int num3 = (int)((float)rectangle2.Width * ((float)progPct / 100f));
					if (num3 > 2)
					{
						using (LinearGradientBrush brush4 = new LinearGradientBrush(new Rectangle(rectangle2.X, rectangle2.Y, Math.Max(2, num3), rectangle2.Height), Theme.Teal, Theme.Neon, LinearGradientMode.Horizontal))
						{
							g.FillRectangle(brush4, rectangle2.X, rectangle2.Y, num3, rectangle2.Height);
						}
					}
					float num4 = rectangle2.X + num3;
					using (SolidBrush brush5 = new SolidBrush(Color.FromArgb(160, Theme.White)))
					{
						g.FillRectangle(brush5, num4 - (float)Theme.S(2f), rectangle2.Y, Theme.S(2f), rectangle2.Height);
					}
				}
				else
				{
					int num5 = rectangle2.Width / 3;
					float num6 = Anim.T * 0.8f % 1f;
					int x = rectangle2.X - num5 + (int)((float)(rectangle2.Width + num5) * num6);
					using (LinearGradientBrush brush6 = new LinearGradientBrush(new Rectangle(x, rectangle2.Y, Math.Max(2, num5), rectangle2.Height), Color.FromArgb(0, Theme.Neon), Theme.Neon, LinearGradientMode.Horizontal))
					{
						g.FillRectangle(brush6, x, rectangle2.Y, num5, rectangle2.Height);
					}
				}
				g.Clip = clip;
			}
		}

		private static void Hud(Graphics g, Rectangle r, Color c)
		{
			float num = Theme.S(14f);
			using (Pen pen = new Pen(Color.FromArgb(200, c), Theme.S(2f)))
			{
				g.DrawLine(pen, r.X, r.Y, (float)r.X + num, r.Y);
				g.DrawLine(pen, r.X, r.Y, r.X, (float)r.Y + num);
				g.DrawLine(pen, r.Right - 1, r.Bottom - 1, (float)(r.Right - 1) - num, r.Bottom - 1);
				g.DrawLine(pen, r.Right - 1, r.Bottom - 1, r.Right - 1, (float)(r.Bottom - 1) - num);
			}
		}

		private static void DrawBox(Graphics g, Rectangle r, Color fill, Color border)
		{
			if (r.Width <= 0 || r.Height <= 0)
			{
				return;
			}
			using (GraphicsPath path = Theme.Round(new RectangleF(r.X, r.Y, r.Width - 1, r.Height - 1), Theme.S(12f)))
			{
				using (SolidBrush brush = new SolidBrush(fill))
				{
					g.FillPath(brush, path);
				}
				using (Pen pen = new Pen(border))
				{
					g.DrawPath(pen, path);
				}
			}
		}
	}
}
