using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace ASConfig
{
	public class PhraseEditor : Form
	{
		private readonly string workDir;

		private PhraseConfig cfg;

		private int curIdx = -1;

		private ListBox groups;

		private TextBox name;

		private Button colorBtn;

		private DataGridView grid;

		private bool loading;

		public PhraseEditor(string workDir)
		{
			this.workDir = workDir;
			cfg = PhraseStore.Load(workDir);
			Text = "Sensitive phrase groups";
			base.StartPosition = FormStartPosition.CenterParent;
			base.FormBorderStyle = FormBorderStyle.FixedDialog;
			base.MaximizeBox = false;
			base.MinimizeBox = false;
			base.ClientSize = new Size(Theme.S(820f), Theme.S(540f));
			BackColor = Theme.Bg;
			Font = Fonts.Sans(9.5f);
			Label value = new Label
			{
				Text = "PHRASE GROUPS FOR THE DOCUMENT SEARCH",
				AutoSize = true,
				ForeColor = Theme.Neon,
				Font = Fonts.Mono(11f, true),
				Location = new Point(Theme.S(16f), Theme.S(12f))
			};
			Label value2 = new Label
			{
				Text = "For each phrase choose where to look: in the file name and/or in the content.",
				AutoSize = true,
				ForeColor = Theme.Muted,
				Location = new Point(Theme.S(16f), Theme.S(36f))
			};
			base.Controls.Add(value);
			base.Controls.Add(value2);
			Label value3 = new Label
			{
				Text = "Groups",
				AutoSize = true,
				ForeColor = Theme.Dim,
				Font = Fonts.Mono(9f, true),
				Location = new Point(Theme.S(16f), Theme.S(66f))
			};
			base.Controls.Add(value3);
			groups = new ListBox
			{
				Location = new Point(Theme.S(16f), Theme.S(86f)),
				Size = new Size(Theme.S(220f), Theme.S(330f)),
				BackColor = Theme.Card,
				ForeColor = Theme.Text,
				BorderStyle = BorderStyle.FixedSingle,
				IntegralHeight = false
			};
			groups.SelectedIndexChanged += delegate
			{
				SelectGroup(groups.SelectedIndex);
			};
			base.Controls.Add(groups);
			Button button = MakeBtn("+ Group", Theme.Green, new Point(Theme.S(16f), Theme.S(424f)), Theme.S(107f));
			button.Click += delegate
			{
				AddGroup();
			};
			Button button2 = MakeBtn("− Group", Theme.Red, new Point(Theme.S(129f), Theme.S(424f)), Theme.S(107f));
			button2.Click += delegate
			{
				DelGroup();
			};
			base.Controls.Add(button);
			base.Controls.Add(button2);
			int num = Theme.S(256f);
			Label value4 = new Label
			{
				Text = "Group name",
				AutoSize = true,
				ForeColor = Theme.Dim,
				Font = Fonts.Mono(9f, true),
				Location = new Point(num, Theme.S(66f))
			};
			base.Controls.Add(value4);
			name = new TextBox
			{
				Location = new Point(num, Theme.S(86f)),
				Size = new Size(Theme.S(420f), Theme.S(26f)),
				BackColor = Theme.Card,
				ForeColor = Theme.Text,
				BorderStyle = BorderStyle.FixedSingle
			};
			name.TextChanged += delegate
			{
				if (curIdx >= 0)
				{
					cfg.Groups[curIdx].Name = name.Text;
					groups.Items[curIdx] = name.Text;
				}
			};
			base.Controls.Add(name);
			Label value5 = new Label
			{
				Text = "Color",
				AutoSize = true,
				ForeColor = Theme.Dim,
				Font = Fonts.Mono(9f, true),
				Location = new Point(num + Theme.S(430f), Theme.S(66f))
			};
			base.Controls.Add(value5);
			colorBtn = new Button
			{
				Location = new Point(num + Theme.S(430f), Theme.S(86f)),
				Size = new Size(Theme.S(110f), Theme.S(26f)),
				FlatStyle = FlatStyle.Flat,
				ForeColor = Color.White
			};
			colorBtn.FlatAppearance.BorderColor = Theme.Border;
			colorBtn.Click += delegate
			{
				PickColor();
			};
			base.Controls.Add(colorBtn);
			Label value6 = new Label
			{
				Text = "Search phrases",
				AutoSize = true,
				ForeColor = Theme.Dim,
				Font = Fonts.Mono(9f, true),
				Location = new Point(num, Theme.S(122f))
			};
			base.Controls.Add(value6);
			grid = new DataGridView
			{
				Location = new Point(num, Theme.S(142f)),
				Size = new Size(Theme.S(548f), Theme.S(274f)),
				AllowUserToAddRows = true,
				AllowUserToResizeRows = false,
				RowHeadersVisible = false,
				AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
				SelectionMode = DataGridViewSelectionMode.CellSelect,
				MultiSelect = false
			};
			StyleGrid(grid);
			grid.Columns.Add(new DataGridViewTextBoxColumn
			{
				Name = "Text",
				HeaderText = "Phrase",
				FillWeight = 60f
			});
			grid.Columns.Add(new DataGridViewCheckBoxColumn
			{
				Name = "InName",
				HeaderText = "In name",
				FillWeight = 20f
			});
			grid.Columns.Add(new DataGridViewCheckBoxColumn
			{
				Name = "InBody",
				HeaderText = "In content",
				FillWeight = 20f
			});
			grid.CellValueChanged += delegate
			{
				CommitGrid();
			};
			grid.CurrentCellDirtyStateChanged += delegate
			{
				if (grid.IsCurrentCellDirty && grid.CurrentCell is DataGridViewCheckBoxCell)
				{
					grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
				}
			};
			base.Controls.Add(grid);
			Button button3 = MakeBtn("− Delete phrase", Theme.Red, new Point(num, Theme.S(424f)), Theme.S(180f));
			button3.Click += delegate
			{
				DelPhrase();
			};
			base.Controls.Add(button3);
			Button button4 = MakeBtn("Save", Theme.Blue, new Point(base.ClientSize.Width - Theme.S(16f) - Theme.S(240f), base.ClientSize.Height - Theme.S(46f)), Theme.S(120f));
			button4.Click += delegate
			{
				DoSave();
			};
			Button button5 = MakeBtn("Close", Theme.CardHov, new Point(base.ClientSize.Width - Theme.S(16f) - Theme.S(112f), base.ClientSize.Height - Theme.S(46f)), Theme.S(112f));
			button5.Click += delegate
			{
				Close();
			};
			base.Controls.Add(button4);
			base.Controls.Add(button5);
			Reload();
			if (cfg.Groups.Count > 0)
			{
				groups.SelectedIndex = 0;
			}
		}

		private Button MakeBtn(string text, Color accent, Point loc, int w)
		{
			Button button = new Button();
			button.Text = text;
			button.Location = loc;
			button.Size = new Size(w, Theme.S(32f));
			button.FlatStyle = FlatStyle.Flat;
			button.BackColor = Theme.Mix(Theme.Bg, accent, 0.18f);
			button.ForeColor = Theme.Light(accent);
			button.Font = Fonts.Sans(9f);
			Button button2 = button;
			button2.FlatAppearance.BorderColor = Color.FromArgb(120, accent);
			return button2;
		}

		private static void StyleGrid(DataGridView g)
		{
			g.BackgroundColor = Color.FromArgb(9, 16, 36);
			g.BorderStyle = BorderStyle.FixedSingle;
			g.GridColor = Color.FromArgb(22, 40, 70);
			g.EnableHeadersVisualStyles = false;
			g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
			g.ColumnHeadersHeight = Theme.S(30f);
			g.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
			{
				BackColor = Color.FromArgb(14, 24, 48),
				ForeColor = Theme.Neon,
				Font = (Fonts.Mono(8.5f, true) ?? SystemFonts.DefaultFont),
				Alignment = DataGridViewContentAlignment.MiddleLeft
			};
			Font font2 = (g.Font = Fonts.Sans(9.5f) ?? SystemFonts.DefaultFont);
			g.DefaultCellStyle = new DataGridViewCellStyle
			{
				Font = font2,
				BackColor = Color.FromArgb(9, 16, 36),
				ForeColor = Theme.Text,
				SelectionBackColor = Color.FromArgb(0, 60, 90),
				SelectionForeColor = Theme.White
			};
			g.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
			{
				Font = font2,
				ForeColor = Theme.Text,
				BackColor = Color.FromArgb(12, 22, 46)
			};
			g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
			g.RowTemplate.Height = Theme.S(26f);
			try
			{
				typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(g, true, null);
			}
			catch
			{
			}
		}

		private void Reload()
		{
			groups.Items.Clear();
			foreach (PhraseGroup group in cfg.Groups)
			{
				groups.Items.Add(group.Name ?? "(unnamed)");
			}
		}

		private void SelectGroup(int idx)
		{
			CommitGrid();
			curIdx = idx;
			loading = true;
			if (idx < 0 || idx >= cfg.Groups.Count)
			{
				name.Text = "";
				grid.Rows.Clear();
				UpdateColorBtn("#FFA53C");
				loading = false;
				return;
			}
			PhraseGroup phraseGroup = cfg.Groups[idx];
			name.Text = phraseGroup.Name ?? "";
			UpdateColorBtn(phraseGroup.ColorHex);
			grid.Rows.Clear();
			foreach (PhraseItem phrase in phraseGroup.Phrases)
			{
				grid.Rows.Add(phrase.Text, phrase.SearchInFileName, phrase.SearchInContent);
			}
			loading = false;
		}

		private void CommitGrid()
		{
			if (loading || curIdx < 0 || curIdx >= cfg.Groups.Count)
			{
				return;
			}
			List<PhraseItem> list = new List<PhraseItem>();
			foreach (DataGridViewRow item in (IEnumerable)grid.Rows)
			{
				if (!item.IsNewRow)
				{
					string text = Convert.ToString(item.Cells["Text"].Value);
					if (!string.IsNullOrWhiteSpace(text))
					{
						list.Add(new PhraseItem
						{
							Text = text.Trim(),
							SearchInFileName = (item.Cells["InName"].Value is bool && (bool)item.Cells["InName"].Value),
							SearchInContent = (item.Cells["InBody"].Value is bool && (bool)item.Cells["InBody"].Value)
						});
					}
				}
			}
			cfg.Groups[curIdx].Phrases = list;
		}

		private void AddGroup()
		{
			CommitGrid();
			cfg.Groups.Add(new PhraseGroup
			{
				Name = "New group",
				ColorHex = "#3CC8FF",
				Phrases = new List<PhraseItem>()
			});
			Reload();
			groups.SelectedIndex = cfg.Groups.Count - 1;
			name.Focus();
			name.SelectAll();
		}

		private void DelGroup()
		{
			if (curIdx >= 0 && curIdx < cfg.Groups.Count && MessageBox.Show(this, "Delete the group '" + cfg.Groups[curIdx].Name + "'?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
			{
				cfg.Groups.RemoveAt(curIdx);
				curIdx = -1;
				Reload();
				if (cfg.Groups.Count > 0)
				{
					groups.SelectedIndex = 0;
				}
				else
				{
					SelectGroup(-1);
				}
			}
		}

		private void DelPhrase()
		{
			if (grid.CurrentRow != null && !grid.CurrentRow.IsNewRow)
			{
				grid.Rows.Remove(grid.CurrentRow);
				CommitGrid();
			}
		}

		private void UpdateColorBtn(string hex)
		{
			Color backColor = ParseHex(hex);
			colorBtn.BackColor = backColor;
			colorBtn.Text = ((!(hex ?? "").StartsWith("#")) ? ("#" + (hex ?? "")) : hex);
			colorBtn.ForeColor = ((!((double)(int)backColor.R * 0.299 + (double)(int)backColor.G * 0.587 + (double)(int)backColor.B * 0.114 > 150.0)) ? Color.White : Color.Black);
		}

		private void PickColor()
		{
			if (curIdx < 0)
			{
				return;
			}
			ColorDialog colorDialog = new ColorDialog();
			colorDialog.Color = ParseHex(cfg.Groups[curIdx].ColorHex);
			colorDialog.FullOpen = true;
			using (ColorDialog colorDialog2 = colorDialog)
			{
				if (colorDialog2.ShowDialog(this) == DialogResult.OK)
				{
					string text = $"#{colorDialog2.Color.R:X2}{colorDialog2.Color.G:X2}{colorDialog2.Color.B:X2}";
					cfg.Groups[curIdx].ColorHex = text;
					UpdateColorBtn(text);
				}
			}
		}

		private static Color ParseHex(string hex)
		{
			try
			{
				return ColorTranslator.FromHtml((!(hex ?? "").StartsWith("#")) ? ("#" + hex) : hex);
			}
			catch
			{
				return Color.FromArgb(255, 165, 60);
			}
		}

		private void DoSave()
		{
			CommitGrid();
			cfg.Groups.RemoveAll((PhraseGroup g) => string.IsNullOrWhiteSpace(g.Name) && (g.Phrases == null || g.Phrases.Count == 0));
			try
			{
				PhraseStore.Save(workDir, cfg);
				MessageBox.Show(this, "Saved. The new groups/phrases will be used by the next 'Sensitive documents' scan.", Text, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				base.DialogResult = DialogResult.OK;
				Close();
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "Could not save: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}
	}
}
