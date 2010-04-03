using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

namespace ChanThreadWatch {
	public partial class frmChanThreadWatch : Form {
		private List<WatchInfo> _watchInfoList = new List<WatchInfo>();

		// Can't lock or Join in the UI thread because if it gets stuck waiting and a worker thread
		// tries to Invoke, it will never return because Invoke needs to run on the (frozen) UI
		// thread.  And I don't like the idea of BeginInvoke in this particular situation.

		// About button, UserAgent, and AssemblyInfo.cs should be updated for version bump.

		// Change log:
		// 1.1.0 (2008-Jan-07):
		//   * Fixed UI slugishness and freezing caused by accidentally leaving a Sleep
		//     inside one of the locks for debugging.
		//   * Supports AnonIB.
		// 1.0.0 (2007-Dec-05):
		//   * Initial release.

		public frmChanThreadWatch() {
			InitializeComponent();
		}

		private void frmChanThreadWatch_Load(object sender, EventArgs e) {
			cboCheckEvery.SelectedItem = "3";
		}

		private void frmChanThreadWatch_FormClosed(object sender, FormClosedEventArgs e) {
			while (!Monitor.TryEnter(_watchInfoList, 10)) Application.DoEvents();
			foreach (WatchInfo w in _watchInfoList) {
				w.Stop = true;
			}
			Monitor.Exit(_watchInfoList);
			foreach (WatchInfo w in _watchInfoList) {
				while ((w.WatchThread != null) && w.WatchThread.IsAlive) {
					Thread.Sleep(10);
					Application.DoEvents();
				}
			}
		}

		private void btnAdd_Click(object sender, EventArgs e) {
			WatchInfo watchInfo = new WatchInfo();
			string pageURL = txtPageURL.Text.Trim().ToLower();
			int listIndex = -1;

			if (pageURL.Length == 0) return;
			if (!pageURL.StartsWith("http://")) pageURL = "http://" + pageURL;

			while (!Monitor.TryEnter(_watchInfoList, 10)) Application.DoEvents();
			try {
				foreach (WatchInfo w in _watchInfoList) {
					if (w.PageURL == pageURL) {
						listIndex = ((w.WatchThread != null) && w.WatchThread.IsAlive) ? -2 : w.ListIndex;
						break;
					}
				}
				if (listIndex != -2) {
					if (listIndex == -1) {
						lvThreads.Items.Add(new ListViewItem(pageURL)).SubItems.Add(String.Empty);
						_watchInfoList.Add(watchInfo);
						listIndex = _watchInfoList.Count - 1;
					}
					else {
						_watchInfoList[listIndex].ListIndex = -1;
						_watchInfoList[listIndex] = watchInfo;
					}
					watchInfo.PageURL = pageURL;
					watchInfo.PageAuth = (chkPageAuth.Checked && (txtPageAuth.Text.IndexOf(':') != -1)) ?
						txtPageAuth.Text : String.Empty;
					watchInfo.ImageAuth = (chkImageAuth.Checked && (txtImageAuth.Text.IndexOf(':') != -1)) ?
						txtImageAuth.Text : String.Empty;
					watchInfo.WaitSeconds = Int32.Parse((string)cboCheckEvery.SelectedItem) * 60;
					watchInfo.OneTime = chkOneTime.Checked;
					watchInfo.ListIndex = listIndex;
					watchInfo.WatchThread = new Thread(WatchThread);
				}
			}
			finally {
				Monitor.Exit(_watchInfoList);
			}

			if (listIndex == -2) {
				MessageBox.Show("The same thread is already being watched or downloaded.",
					"Duplicate Thread", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			watchInfo.WatchThread.Start(watchInfo);
			txtPageURL.Clear();
			txtPageURL.Focus();
		}
		
		private void btnStopSelected_Click(object sender, EventArgs e) {
			while (!Monitor.TryEnter(_watchInfoList, 10)) Application.DoEvents();
			try {
				foreach (ListViewItem item in lvThreads.SelectedItems) {
					_watchInfoList[item.Index].Stop = true;
				}
			}
			finally {
				Monitor.Exit(_watchInfoList);
			}
		}

		private void btnRemoveCompleted_Click(object sender, EventArgs e) {
			while (!Monitor.TryEnter(_watchInfoList, 10)) Application.DoEvents();
			try {
				int i = 0;
				while (i < _watchInfoList.Count) {
					WatchInfo watchInfo = _watchInfoList[i];
					if (watchInfo.Stop || !watchInfo.WatchThread.IsAlive) {
						watchInfo.ListIndex = -1;
						_watchInfoList.RemoveAt(i);
						lvThreads.Items.RemoveAt(i);
					}
					else {
						watchInfo.ListIndex = i;
						i++;
					}
				}
			}
			finally {
				Monitor.Exit(_watchInfoList);
			}
		}

		private void btnOpenSelectedFolder_Click(object sender, EventArgs e) {
			while (!Monitor.TryEnter(_watchInfoList, 10)) Application.DoEvents();
			try {
				foreach (ListViewItem item in lvThreads.SelectedItems) {
					string saveDir = _watchInfoList[item.Index].SaveDir;
					if (!String.IsNullOrEmpty(saveDir)) {
						try {
							System.Diagnostics.Process.Start(saveDir);
						}
						catch {}
					}
				}
			}
			finally {
				Monitor.Exit(_watchInfoList);
			}
		}

		private void btnAbout_Click(object sender, EventArgs e) {
			MessageBox.Show(String.Format("Chan Thread Watch{0}Version 1.1.0 (2008-Jan-07){0}jart1126@yahoo.com",
				Environment.NewLine), "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		private void chkOneTime_CheckedChanged(object sender, EventArgs e) {
			cboCheckEvery.Enabled = !chkOneTime.Checked;
		}

		private void chkPageAuth_CheckedChanged(object sender, EventArgs e) {
			txtPageAuth.Enabled = chkPageAuth.Checked;
		}

		private void chkImageAuth_CheckedChanged(object sender, EventArgs e) {
			txtImageAuth.Enabled = chkImageAuth.Checked;
		}

		private void SetStatus(WatchInfo watchInfo, string status) {
			if (watchInfo.ListIndex == -1) return;
			Invoke((MethodInvoker)delegate() {
				lvThreads.Items[watchInfo.ListIndex].SubItems[1].Text = status;
			});
		}

		private byte[] StreamToBytes(Stream stream, FileStream fs) {
			List<ByteBuff> buffList = new List<ByteBuff>();
			while (true) {
				byte[] data = new byte[8192];
				int dataLen = stream.Read(data, 0, data.Length);
				if (dataLen == 0) break;
				buffList.Add(new ByteBuff(data, dataLen));
				if (fs != null) {
					fs.Write(data, 0, dataLen);
				}
			}
			return ByteBuffListToBytes(buffList);
		}

		private byte[] ByteBuffListToBytes(List<ByteBuff> list) {
			int totalLen = 0;
			int offset = 0;
			byte[] ret;

			for (int i = 0; i < list.Count; i++) {
				totalLen += list[i].Length;
			}

			ret = new byte[totalLen];

			for (int i = 0; i < list.Count; i++) {
				byte[] data = list[i].Data;
				int len = list[i].Length;
				Buffer.BlockCopy(data, 0, ret, offset, len);
				offset += len;
			}
			
			return ret;
		}

		private string BytesToString(byte[] bytes) {
			byte[] copy = new byte[bytes.Length];
			bool prevWasSpace = false;
			int iDst = 0;
			for (int iSrc = 0; iSrc < bytes.Length; iSrc++) {
				if ((bytes[iSrc] == 32) || (bytes[iSrc] == 9) || (bytes[iSrc] == 13) || (bytes[iSrc] == 10)) {
					if (!prevWasSpace) {
						copy[iDst++] = 32;
					}
					prevWasSpace = true;
				}
				else {
					copy[iDst++] = bytes[iSrc];
					prevWasSpace = false;
				}
			}
			return Encoding.ASCII.GetString(copy, 0, iDst);
		}

		private Stream GetToStream(string url, string auth, ref DateTime? cacheTime) {
			HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
			req.UserAgent = "Chan Thread Watch 1.1.0";
			if (cacheTime != null) {
				req.IfModifiedSince = (DateTime)cacheTime;
			}
			if (!String.IsNullOrEmpty(auth)) {
				req.Headers.Add("Authorization", "Basic " +
					Convert.ToBase64String(Encoding.ASCII.GetBytes(auth)));
			}
			HttpWebResponse resp;
			try {
				resp = (HttpWebResponse)req.GetResponse();
				cacheTime = resp.LastModified;
			}
			catch (WebException ex) {
				if (ex.Status == WebExceptionStatus.ProtocolError) {
					HttpStatusCode code = ((HttpWebResponse)ex.Response).StatusCode;
					if (code == HttpStatusCode.NotFound) {
						throw new HTTP404Exception();
					}
					else if (code == HttpStatusCode.NotModified) {
						throw new HTTP304Exception();
					}
				}
				throw;
			}
			return resp.GetResponseStream();
		}

		private Stream GetToStream(string url, string auth) {
			DateTime? cacheTime = null;
			return GetToStream(url, auth, ref cacheTime);
		}

		private string GetToString(string url, string auth, string path, ref DateTime? cacheTime) {
			byte[] respBytes;
			using (Stream respStream = GetToStream(url, auth, ref cacheTime)) {
				if (path != null) {
					using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read)) {
						respBytes = StreamToBytes(respStream, fs);
					}
				}
				else {
					respBytes = StreamToBytes(respStream, null);
				}
			}
			return BytesToString(respBytes);
		}

		private void GetToFile(string url, string auth, string path) {
			using (Stream respStream = GetToStream(url, auth)) {
				using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read)) {
					while (true) {
						byte[] data = new byte[8192];
						int dataLen = respStream.Read(data, 0, data.Length);
						if (dataLen == 0) break;
						fs.Write(data, 0, dataLen);
					}
				}
			}
		}

		private string AbsoluteURL(string pageURL, string linkURL) {
			try {
				return new Uri(new Uri(pageURL), linkURL).AbsoluteUri;
			}
			catch {
				return linkURL;
			}
		}

		private List<string> GetLinks(string page, string pageURL) {
			List<string> links = new List<string>();
			Regex r = new Regex("href\\s*=\\s*(?:(?:\\\"(?<url>[^\\\"]*)\\\")|(?<url>[^\\s]* ))");
			MatchCollection mc = r.Matches(page);
			foreach (Match m in mc) {
				links.Add(AbsoluteURL(pageURL, m.Groups[1].Value));
			}
			return links;
		}

		private string URLFilename(string url) {
			int pos = url.LastIndexOf("/");
			return (pos == -1) ? String.Empty : url.Substring(pos + 1);
		}

		private void ParseURL(string url, out string site, out string board, out string thread) {
			string[] urlSplit = url.Substring(7).Split(new char[] { '/' },
				StringSplitOptions.RemoveEmptyEntries);
			site = String.Empty;
			board = String.Empty;
			thread = String.Empty;
			if (urlSplit.Length >= 1) {
				string[] hostSplit = urlSplit[0].Split('.');
				if (hostSplit.Length >= 2) {
					site = hostSplit[hostSplit.Length - 2];
				}
			}
			if (urlSplit.Length >= 2) {
				board = urlSplit[1];
			}
			if (urlSplit.Length >= 3) {
				thread = urlSplit[urlSplit.Length - 1];
				if (site == "anonib") {
					int pos = thread.IndexOf('?');
					if (pos != -1) {
						string[] urlVarsSplit = thread.Substring(pos + 1).Split(
							new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
						foreach (string v in urlVarsSplit) {
							if (v.StartsWith("t=")) {
								thread = v.Substring(2);
								break;
							}
						}
					}
				}
				else {
					int pos = thread.LastIndexOf('.');
					if (pos != -1) {
						thread = thread.Substring(0, pos);
					}
				}
			}
		}

		private void WatchThread(object p) {
			WatchInfo watchInfo = (WatchInfo)p;
			string pageURL = watchInfo.PageURL;
			string pageAuth = watchInfo.PageAuth;
			string imgAuth = watchInfo.ImageAuth;
			double waitSeconds = (double)watchInfo.WaitSeconds;
			string page = null;
			string saveDir, saveFilename, savePath, site, board, thread, linkFilter;
			int numTries;
			const int maxTries = 3;
			DateTime pageGetTime = DateTime.Now;
			DateTime? pageCacheTime = null;
			double waitRemain;

			ParseURL(pageURL, out site, out board, out thread);
			linkFilter = (site == "anonib") ? "/images/" : "/src/";
			saveDir = Path.Combine(Application.StartupPath, site + "_" + board + "_" + thread);
			if (!Directory.Exists(saveDir)) {
				Directory.CreateDirectory(saveDir);
			}
			lock (_watchInfoList) {
				watchInfo.SaveDir = saveDir;
			}
			
			while (true) {
				saveFilename = thread + ".html";
				savePath = (saveFilename.Length != 0) ? Path.Combine(saveDir, saveFilename) : null;
				for (numTries = 1; numTries <= maxTries; numTries++) {
					lock (_watchInfoList) {
						if (watchInfo.Stop) {
							SetStatus(watchInfo, "Stopped by user");
							return;
						}
						SetStatus(watchInfo, String.Format("Downloading page{0}", numTries == 1 ?
							String.Empty : " (retry " + (numTries - 1).ToString() + ")"));
					}
					try {
						page = GetToString(pageURL, pageAuth, savePath, ref pageCacheTime);
						break;
					}
					catch (HTTP404Exception) {
						lock (_watchInfoList) {
							SetStatus(watchInfo, "Stopped, page not found");
						}
						return;
					}
					catch (HTTP304Exception) {
						page = null;
						break;
					}
					catch {
						page = null;
					}
				}
				pageGetTime = DateTime.Now;
				if (page != null) {
					List<string> links = GetLinks(page, pageURL);
					List<string> imgs = new List<string>();
					foreach (string link in links) {
						if ((link.IndexOf(linkFilter) != -1) && !imgs.Contains(link)) {
							imgs.Add(link);
						}
					}
					for (int i = 0; i < imgs.Count; i++) {
						saveFilename = URLFilename(imgs[i]);
						savePath = (saveFilename.Length != 0) ? Path.Combine(saveDir, saveFilename) : null;
						if ((savePath != null) && !File.Exists(savePath)) {
							for (numTries = 1; numTries <= maxTries; numTries++) {
								lock (_watchInfoList) {
									if (watchInfo.Stop) {
										SetStatus(watchInfo, "Stopped by user");
										return;
									}
									SetStatus(watchInfo, String.Format("Downloading image {0} " + 
										"of {1}{2}", i + 1, imgs.Count, numTries == 1 ? String.Empty :
										" (retry " + (numTries - 1).ToString() + ")"));
								}
								try {
									GetToFile(imgs[i], imgAuth, savePath);
									break;
								}
								catch {
								}
							}
						}
					}
					page = null;
				}
				while (true) {
					waitRemain = waitSeconds - ((TimeSpan)(DateTime.Now - pageGetTime)).TotalSeconds;
					if (waitRemain <= 0.0) {
						break;
					}
					lock (_watchInfoList) {
						if (watchInfo.Stop || watchInfo.OneTime) {
							SetStatus(watchInfo, watchInfo.Stop ? "Stopped by user" :
								"Stopped, download finished");
							return;
						}
						SetStatus(watchInfo, String.Format("Waiting {0:0} seconds", waitRemain));
					}
					Thread.Sleep(500);
				}
			}
		}
	}

	public class ByteBuff {
		public byte[] Data;
		public int Length;

		public ByteBuff(byte[] data, int len) {
			Data = data;
			Length = len;
		}
	}

	public class WatchInfo {
		public int ListIndex;
		public bool Stop;
		public Thread WatchThread;
		public string PageURL;
		public string PageAuth;
		public string ImageAuth;
		public int WaitSeconds;
		public bool OneTime;
		public string SaveDir;
	}

	public class HTTP404Exception : Exception {
		public HTTP404Exception() {
		}
	}

	public class HTTP304Exception : Exception {
		public HTTP304Exception() {
		}
	}
}