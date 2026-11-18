using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace ASConfig
{
	/// <summary>
	/// Integrity of the extracted scripts.
	/// 1.5.9 extracted the payload to %LocalAppData% and ran the scripts elevated without any check:
	/// any process of the same user running at the normal (Medium) integrity level could replace RunAction.ps1
	/// between extraction and launch. Now:
	///   • the folder gets a High integrity label (writes from Medium processes are denied);
	///   • before EVERY run, the SHA-256 of every file is compared with the manifest embedded in the exe at build time.
	/// </summary>
	internal static class PayloadGuard
	{
		private static Dictionary<string, string> manifest;

		/// <summary>Manifest (resource payload.manifest.json, produced by build/hash-payload.ps1). null means a build without a manifest.</summary>
		public static Dictionary<string, string> Manifest
		{
			get
			{
				if (manifest != null) return manifest;
				using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.manifest.json"))
				{
					if (s == null) return null;
					using (StreamReader r = new StreamReader(s, Encoding.UTF8))
					{
						Dictionary<string, object> root = new JavaScriptSerializer().DeserializeObject(r.ReadToEnd()) as Dictionary<string, object>;
						object files;
						if (root == null || !root.TryGetValue("files", out files) || !(files is Dictionary<string, object>)) return null;
						manifest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
						foreach (KeyValuePair<string, object> kv in (Dictionary<string, object>)files) manifest[kv.Key.Replace('/', Path.DirectorySeparatorChar)] = Convert.ToString(kv.Value);
					}
				}
				return manifest;
			}
		}

		/// <summary>Sets a High integrity label on the folder (inherited by the files). Requires administrator rights.</summary>
		public static bool Harden(string dir, out string error)
		{
			error = null;
			try
			{
				ProcessStartInfo psi = new ProcessStartInfo("icacls.exe", "\"" + dir + "\" /setintegritylevel (OI)(CI)H /Q")
				{ UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
				using (Process p = Process.Start(psi))
				{
					string err = p.StandardError.ReadToEnd();
					p.WaitForExit(15000);
					if (p.ExitCode != 0) { error = "icacls: code " + p.ExitCode + " " + err.Trim(); return false; }
				}
				return true;
			}
			catch (Exception ex) { error = ex.Message; return false; }
		}

		/// <summary>Compares the files with the manifest. Returns the list of differences (empty means everything is fine).</summary>
		public static List<string> Verify(string dir)
		{
			List<string> bad = new List<string>();
			Dictionary<string, string> m = Manifest;
			if (m == null) { bad.Add("(this build has no payload.manifest.json - the check cannot be performed)"); return bad; }
			using (SHA256 sha = SHA256.Create())
			{
				foreach (KeyValuePair<string, string> kv in m)
				{
					string path = Path.Combine(dir, kv.Key);
					if (!File.Exists(path)) { bad.Add("missing: " + kv.Key); continue; }
					string h;
					using (FileStream fs = File.OpenRead(path)) h = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "");
					if (!string.Equals(h, kv.Value, StringComparison.OrdinalIgnoreCase)) bad.Add("modified: " + kv.Key);
				}
			}
			// Extra .ps1 files planted next to ours are suspicious too: RunAction.ps1 loads every *.ps1 from Private\* and Engine\Steps.
			foreach (string sub in new[] { "Private", "Public", "Engine" })
			{
				string root = Path.Combine(dir, sub);
				if (!Directory.Exists(root)) continue;
				foreach (string f in Directory.GetFiles(root, "*.ps1", SearchOption.AllDirectories))
				{
					string rel = f.Substring(dir.TrimEnd(Path.DirectorySeparatorChar).Length + 1);
					if (!m.ContainsKey(rel)) bad.Add("unexpected script: " + rel);
				}
			}
			return bad;
		}
	}
}
