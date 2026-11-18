using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ASConfig
{
	internal class InstallerForm : Form
	{
		private readonly CheckBox desktop;

		private readonly CheckBox nouacHint;

		private readonly Button install;

		private readonly Button cancel;

		private readonly Label status;

		public InstallerForm()
		{
			Text = "AS Shield — setup";
			base.StartPosition = FormStartPosition.CenterScreen;
			base.FormBorderStyle = FormBorderStyle.FixedDialog;
			base.MaximizeBox = false;
			base.MinimizeBox = false;
			base.ClientSize = new Size(Theme.S(560f), Theme.S(360f));
			BackColor = Theme.Bg;
			ForeColor = Theme.Text;
			Font = Fonts.Sans(9.5f);
			try
			{
				base.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
			}
			catch
			{
			}
			Label value = new Label
			{
				Text = "AS Shield",
				AutoSize = true,
				ForeColor = Theme.Neon,
				Font = Fonts.Mono(20f, true),
				Location = new Point(Theme.S(28f), Theme.S(24f))
			};
			Label value2 = new Label
			{
				Text = "Windows security policy configuration · ver." + SelfInstall.AppVersion,
				AutoSize = true,
				ForeColor = Theme.Dim,
				Font = Fonts.Mono(9f),
				Location = new Point(Theme.S(30f), Theme.S(62f))
			};
			string text = SelfInstall.InstalledVersion();
			string text2 = ((text != null) ? ("Installed version " + text + " found. It will be updated to " + SelfInstall.AppVersion + ".\nInstall folder:") : "The program will be installed to:");
			Label value3 = new Label
			{
				Text = text2,
				AutoSize = true,
				ForeColor = Theme.Text,
				Location = new Point(Theme.S(30f), Theme.S(104f)),
				MaximumSize = new Size(Theme.S(500f), 0)
			};
			Label value4 = new Label
			{
				Text = SelfInstall.InstallDir,
				AutoSize = true,
				ForeColor = Theme.Teal,
				Font = Fonts.Mono(9.5f),
				Location = new Point(Theme.S(30f), Theme.S(150f))
			};
			desktop = new CheckBox
			{
				Text = "Create a desktop shortcut",
				AutoSize = true,
				Checked = true,
				ForeColor = Theme.Text,
				Location = new Point(Theme.S(30f), Theme.S(182f))
			};
			nouacHint = new CheckBox
			{
				Text = "After installation, show a note about administrator rights",
				AutoSize = true,
				Checked = false,
				ForeColor = Theme.Dim,
				Location = new Point(Theme.S(30f), Theme.S(210f))
			};
			status = new Label
			{
				Text = "",
				AutoSize = false,
				Size = new Size(Theme.S(500f), Theme.S(22f)),
				ForeColor = Theme.Dim,
				Font = Fonts.Mono(8.5f),
				Location = new Point(Theme.S(30f), Theme.S(250f))
			};
			install = new Button
			{
				Text = "Install",
				Size = new Size(Theme.S(150f), Theme.S(38f)),
				Location = new Point(Theme.S(286f), Theme.S(300f)),
				FlatStyle = FlatStyle.Flat,
				BackColor = Theme.Mix(Theme.Bg, Theme.Neon, 0.22f),
				ForeColor = Theme.Light(Theme.Neon),
				Font = Fonts.Sans(10f, true)
			};
			install.FlatAppearance.BorderColor = Color.FromArgb(140, Theme.Neon);
			cancel = new Button
			{
				Text = "Cancel",
				Size = new Size(Theme.S(110f), Theme.S(38f)),
				Location = new Point(Theme.S(442f), Theme.S(300f)),
				FlatStyle = FlatStyle.Flat,
				BackColor = Theme.Card,
				ForeColor = Theme.Dim,
				Font = Fonts.Sans(10f)
			};
			cancel.FlatAppearance.BorderColor = Color.FromArgb(60, 90, 130);
			install.Click += DoInstall;
			cancel.Click += delegate
			{
				base.DialogResult = DialogResult.Cancel;
				Close();
			};
			base.Controls.Add(value);
			base.Controls.Add(value2);
			base.Controls.Add(value3);
			base.Controls.Add(value4);
			base.Controls.Add(desktop);
			base.Controls.Add(nouacHint);
			base.Controls.Add(status);
			base.Controls.Add(install);
			base.Controls.Add(cancel);
			base.Paint += delegate(object s, PaintEventArgs e)
			{
				using (Pen pen = new Pen(Color.FromArgb(40, 70, 110)))
				{
					e.Graphics.DrawLine(pen, Theme.S(30f), Theme.S(92f), base.ClientSize.Width - Theme.S(30f), Theme.S(92f));
				}
			};
		}

		private void DoInstall(object sender, EventArgs e)
		{
			install.Enabled = false;
			cancel.Enabled = false;
			try
			{
				SelfInstall.Install(desktop.Checked, delegate(string m)
				{
					status.ForeColor = Theme.Dim;
					status.Text = m;
					status.Refresh();
				});
				string text = ((!nouacHint.Checked) ? "" : "\n\nAS Shield needs administrator rights, so Windows asks for confirmation (UAC) every time it starts.");
				if (MessageBox.Show("AS Shield is installed.\n\nStart the program now?" + text, "AS Shield", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk) == DialogResult.Yes)
				{
					try
					{
						Process.Start(SelfInstall.InstalledExe);
					}
					catch
					{
					}
				}
				base.DialogResult = DialogResult.OK;
				Close();
			}
			catch (Exception ex)
			{
				status.ForeColor = Theme.Red;
				status.Text = ex.Message;
				MessageBox.Show(ex.Message, "AS Shield", MessageBoxButtons.OK, MessageBoxIcon.Error);
				install.Enabled = true;
				cancel.Enabled = true;
			}
		}
	}
}
