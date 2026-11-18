using System.Collections.Generic;

namespace ASConfig
{
	public class PhraseGroup
	{
		public string Name { get; set; }

		public string ColorHex { get; set; }

		public List<PhraseItem> Phrases { get; set; }
	}
}
