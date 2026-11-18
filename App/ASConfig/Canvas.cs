using System;
using System.Drawing;
using System.Windows.Forms;

namespace ASConfig
{
	internal class Canvas : Panel
	{
		private Bitmap bgCache;

		private Size bgSize = Size.Empty;

		public Action<Graphics> ExtraBg;

		public Canvas()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
		}

		public void InvalidateBg()
		{
			if (bgCache != null)
			{
				bgCache.Dispose();
				bgCache = null;
			}
		}

		protected override void OnPaintBackground(PaintEventArgs e)
		{
			Size clientSize = base.ClientSize;
			if (clientSize.Width <= 0 || clientSize.Height <= 0)
			{
				Theme.Backdrop(e.Graphics, this, e.ClipRectangle);
				return;
			}
			if (bgCache == null || bgSize != clientSize)
			{
				if (bgCache != null)
				{
					bgCache.Dispose();
				}
				bgSize = clientSize;
				bgCache = new Bitmap(clientSize.Width, clientSize.Height);
				using (Graphics graphics = Graphics.FromImage(bgCache))
				{
					Theme.Quality(graphics);
					Theme.Backdrop(graphics, this, new Rectangle(0, 0, clientSize.Width, clientSize.Height));
					if (ExtraBg != null)
					{
						ExtraBg(graphics);
					}
				}
			}
			e.Graphics.DrawImageUnscaled(bgCache, 0, 0);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Theme.Quality(e.Graphics);
			base.OnPaint(e);
			Theme.Scanline(e.Graphics, this);
		}
	}
}
