using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace ASConfig
{
	internal static class Anim
	{
		private static readonly Stopwatch sw;

		private static readonly Timer timer;

		public static float ScanY;

		public static float ScanAlpha;

		public static int FormHeight;

		public static float T => (float)sw.Elapsed.TotalSeconds;

		public static event Action Tick;

		static Anim()
		{
			sw = Stopwatch.StartNew();
			timer = new Timer
			{
				Interval = 33
			};
			FormHeight = 800;
			timer.Tick += delegate
			{
				float num = T % 8f / 8f;
				ScanY = num * (float)FormHeight;
				ScanAlpha = ((num < 0.05f) ? (num / 0.05f) : ((!(num > 0.95f)) ? 1f : ((1f - num) / 0.05f)));
				Anim.Tick?.Invoke();
			};
			timer.Start();
		}

		public static void Touch()
		{
		}

		public static void Configure(bool lite)
		{
			try
			{
				timer.Interval = ((!lite) ? 33 : 100);
			}
			catch
			{
			}
		}

		public static float Pulse(float period = 2f)
		{
			return 0.7f + 0.3f * (float)Math.Cos((double)T * Math.PI * 2.0 / (double)period);
		}

		public static float Ping(float period = 1.2f)
		{
			return T % period / period;
		}
	}
}
