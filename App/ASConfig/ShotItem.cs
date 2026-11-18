namespace ASConfig
{
	internal class ShotItem
	{
		public int N;

		public string Title;

		public string Hint;

		public string[] Open = new string[0];

		public int Expect = 1;

		public bool NoCapture;

		public string Prereq;

		public int Seq;

		public string Apply;

		public string Node;

		public bool Tree;

		public int CaptureOrder => (Seq <= 0) ? N : Seq;
	}
}
