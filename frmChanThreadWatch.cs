using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace ChanThreadWatch {
	public partial class frmChanThreadWatch : Form {
		private List<WatchInfo> _watchInfoList = new List<WatchInfo>();

		// Can't lock or Join in the UI thread because if it gets stuck waiting and a worker thread
		// tries to Invoke, it will never return because Invoke needs to run on the (frozen) UI
		// thread.  And I don't like the idea of BeginInvoke in this particular situation.

		// ReleaseDate property and version in AssemblyInfo.cs should be updated for each release.

		// Change log:
		// 1.2.2 (2009-Aug-22):
		//   * Download folder can be relative to the executable folder.
		//   * Settings and thread list can be saved in the executable folder instead of
		//     the application data folder.
		//   * Detects image wrapper pages and sends referer for better site
		//     compatibility.
		//   * Locking is utilized properly when exiting.
		// 1.2.1 (2009-May-06):
		//   * Works with HTTPS sites (stupid bug).
		// 1.2.0 (2009-May-05):
		//   * Settings are remembered across runs.
		//   * Thread list is remembered across runs, with a prompt at start before
		//     reloading.
		//   * Main window is resizable.
		//   * Download location is configurable. Default download location is now in My
		//     Documents for limited user account compatibility.
		//   * User Agent is configurable.
		//   * Added context menu for the thread list. Moved Stop and Open Folder buttons
		//     there and added new features: Open URL, Copy URL, Check Now, Check Every X
		//     Minutes.
		//   * Double-clicking a thread can open its folder or URL (configurable).
		// 1.1.3 (2008-Jul-08):
		//   * Restores AnonIB support.
		// 1.1.2 (2008-Jun-29):
		//   * Ignores duplicate filenames when creating image URL list; fixes incorrect
		//     image count on 4chan.
		//   * Doesn't convert page URL to lowercase; fixes 404 problem when page URL
		//     contains uppercase characters.
		// 1.1.1 (2008-Jan-16):
		//   * Workarounds for Mono's form scaling problems and HttpWebResponse 
		//     LastModified bug.
		// 1.1.0 (2008-Jan-07):
		//   * Fixed UI slugishness and freezing caused by accidentally leaving a Sleep
		//     inside one of the locks for debugging.
		//   * Supports AnonIB.
		// 1.0.0 (2007-Dec-05):
		//   * Initial release.

		private static string Version {
			get {
				Version ver = Assembly.GetExecutingAssembly().GetName().Version;
				return ver.Major + "." + ver.Minor + "." + ver.Revision;
			}
		}

		private static string ReleaseDate {
			get {
				return "2009-Aug-22";
			}
		}

		public frmChanThreadWatch() {
			// Older versons of Mono don't disable this automatically
			AutoScale = false;

			InitializeComponent();

			if (Font.Name != "Tahoma") Font = new Font("Arial", 8.25F);

			Settings.Load();

			for (int i = 0; i < cboCheckEvery.Items.Count; i++) {
				int minutes = Int32.Parse((string)cboCheckEvery.Items[i]);
				MenuItem menuItem = new MenuItem();
				menuItem.Name = "miCheckEvery_" + minutes;
				menuItem.Index = i;
				menuItem.Tag = minutes;
				menuItem.Text = minutes + " Minute" + ((minutes != 1) ? "s" : String.Empty);
				menuItem.Click += new EventHandler(miCheckEvery_Click);
				miCheckEvery.MenuItems.Add(menuItem);
			}

			if ((GetDownloadFolder() == null) || !Directory.Exists(GetDownloadFolder())) {
				Settings.DownloadFolder = Path.Combine(Environment.GetFolderPath(
					Environment.SpecialFolder.MyDocuments), "Watched Threads");
				Settings.DownloadFolderIsRelative = false;
			}
			if (Settings.OnThreadDoubleClick == null) {
				Settings.OnThreadDoubleClick = ThreadDoubleClickAction.OpenFolder;
			}

			chkPageAuth.Checked = Settings.UsePageAuth ?? false;
			txtPageAuth.Text = Settings.PageAuth ?? String.Empty;
			chkImageAuth.Checked = Settings.UseImageAuth ?? false;
			txtImageAuth.Text = Settings.ImageAuth ?? String.Empty;
			chkOneTime.Checked = Settings.OneTimeDownload ?? false;
			cboCheckEvery.SelectedItem = (Settings.CheckEvery ?? 3).ToString();
			OnThreadDoubleClick = Settings.OnThreadDoubleClick.Value;
		}

		private ThreadDoubleClickAction OnThreadDoubleClick {
			get {
				if (rbOpenURL.Checked)
					return ThreadDoubleClickAction.OpenURL;
				else
					return ThreadDoubleClickAction.OpenFolder;
			}
			set {
				if (value == ThreadDoubleClickAction.OpenURL)
					rbOpenURL.Checked = true;
				else
					rbOpenFolder.Checked = true;
			}
		}

		private void frmChanThreadWatch_Shown(object sender, EventArgs e) {
			try {
				LoadThreadList();
			}
			catch { }
		}

		private void frmChanThreadWatch_FormClosed(object sender, FormClosedEventArgs e) {
			Settings.UsePageAuth = chkPageAuth.Checked;
			Settings.PageAuth = txtPageAuth.Text;
			Settings.UseImageAuth = chkImageAuth.Checked;
			Settings.ImageAuth = txtImageAuth.Text;
			Settings.OneTimeDownload = chkOneTime.Checked;
			Settings.CheckEvery = Int32.Parse((string)cboCheckEvery.SelectedItem);
			Settings.OnThreadDoubleClick = OnThreadDoubleClick;
			try {
				Settings.Save();
			}
			catch { }

			using (SoftLock.Obtain(_watchInfoList)) {
				try {
					SaveThreadList();
				}
				catch { }
				foreach (WatchInfo w in _watchInfoList) {
					w.Stop = true;
				}
			}

			while (true) {
				Thread watchThread = null;
				using (SoftLock.Obtain(_watchInfoList)) {
					foreach (WatchInfo w in _watchInfoList) {
						if ((w.WatchThread != null) && w.WatchThread.IsAlive) {
							watchThread = w.WatchThread;
							break;
						}
					}
				}
				if (watchThread == null) break;
				while (watchThread.IsAlive) {
					Thread.Sleep(10);
					// Don't call DoEvents inside the lock because it could allow another
					// part of the UI thread to enter the lock as well.
					Application.DoEvents();
				}
			}
		}

		private void btnAdd_Click(object sender, EventArgs e) {
			string pageURL = txtPageURL.Text.Trim();
			string pageAuth = (chkPageAuth.Checked && (txtPageAuth.Text.IndexOf(':') != -1)) ? txtPageAuth.Text : String.Empty;
			string imageAuth = (chkImageAuth.Checked && (txtImageAuth.Text.IndexOf(':') != -1)) ? txtImageAuth.Text : String.Empty;
			int waitSeconds = Int32.Parse((string)cboCheckEvery.SelectedItem) * 60;

			if (pageURL.Length == 0) return;
			if (!pageURL.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
				!pageURL.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			{
				pageURL = "http://" + pageURL;
			}

			if (!AddThread(pageURL, pageAuth, imageAuth, waitSeconds, chkOneTime.Checked, null)) {
				MessageBox.Show("The same thread is already being watched or downloaded.",
					"Duplicate Thread", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			txtPageURL.Clear();
			txtPageURL.Focus();
		}

		private void btnRemoveCompleted_Click(object sender, EventArgs e) {
			RemoveThreads(true, false);
		}

		private void miStop_Click(object sender, EventArgs e) {
			using (SoftLock.Obtain(_watchInfoList)) {
				foreach (ListViewItem item in lvThreads.SelectedItems) {
					_watchInfoList[item.Index].Stop = true;
				}
			}
		}

		private void miOpenFolder_Click(object sender, EventArgs e) {
			foreach (string dir in GetFromSelected(wi => wi.SaveDir)) {
				try {
					Process.Start(dir);
				}
				catch {}
			}
		}

		private void miOpenURL_Click(object sender, EventArgs e) {
			foreach (string url in GetFromSelected(wi => wi.PageURL)) {
				try {
					Process.Start(url);
				}
				catch {}
			}
		}

		private void miCopyURL_Click(object sender, EventArgs e) {
			StringBuilder sb = new StringBuilder();
			foreach (string url in GetFromSelected(wi => wi.PageURL)) {
				if (sb.Length != 0) sb.Append(Environment.NewLine);
				sb.Append(url);
			}
			Clipboard.Clear();
			Clipboard.SetText(sb.ToString());
		}

		
		private void miCheckNow_Click(object sender, EventArgs e) {
			using (SoftLock.Obtain(_watchInfoList)) {
				foreach (ListViewItem item in lvThreads.SelectedItems) {
					_watchInfoList[item.Index].NextCheck = TickCount.Now;
				}
			}
		}	
	
		private void miCheckEvery_Click(object sender, EventArgs e) {
			MenuItem menuItem = sender as MenuItem;
			if (menuItem != null) {
				int waitSeconds = Convert.ToInt32(menuItem.Tag) * 60;
				using (SoftLock.Obtain(_watchInfoList)) {
					foreach (ListViewItem item in lvThreads.SelectedItems) {
						WatchInfo wi = _watchInfoList[item.Index];
						wi.NextCheck += (waitSeconds - wi.WaitSeconds) * 1000;
						wi.WaitSeconds = waitSeconds;
					}
				}
			}
		}

		private void btnSettings_Click(object sender, EventArgs e) {
			using (frmSettings settingsForm = new frmSettings()) {
				settingsForm.ShowDialog();
			}
		}

		private void btnAbout_Click(object sender, EventArgs e) {
			MessageBox.Show(String.Format("Chan Thread Watch{0}Version {1} ({2}){0}jart1126@yahoo.com", Environment.NewLine,
				Version, ReleaseDate), "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		private void lvThreads_KeyDown(object sender, KeyEventArgs e) {
			if (e.KeyCode == Keys.Delete) {
				RemoveThreads(false, true);
			}
		}

		private void lvThreads_MouseClick(object sender, MouseEventArgs e) {
			if (e.Button == MouseButtons.Right) {
				if (lvThreads.SelectedItems.Count != 0) {
					cmThreads.Show(lvThreads, e.Location);
				}
			}
		}

		private void lvThreads_MouseDoubleClick(object sender, MouseEventArgs e) {
			if (OnThreadDoubleClick == ThreadDoubleClickAction.OpenFolder) {
				miOpenFolder_Click(sender, e);
			}
			else {
				miOpenURL_Click(sender, e);
			}
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

		private string GetDownloadFolder() {
			string path = Settings.DownloadFolder;
			if (!String.IsNullOrEmpty(path) && (Settings.DownloadFolderIsRelative == true)) {
				path = Path.GetFullPath(path);
			}
			return path;
		}

		private bool AddThread(string pageURL, string pageAuth, string imageAuth, int waitSeconds, bool oneTime, string saveDir) {
			WatchInfo watchInfo = new WatchInfo();
			int listIndex = -1;

			using (SoftLock.Obtain(_watchInfoList)) {
				foreach (WatchInfo w in _watchInfoList) {
					if (String.Compare(w.PageURL, pageURL, true) == 0) {
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
						lvThreads.Items[listIndex].Text = pageURL;
					}
					watchInfo.PageURL = pageURL;
					watchInfo.PageAuth = pageAuth;
					watchInfo.ImageAuth = imageAuth;
					watchInfo.WaitSeconds = waitSeconds;
					watchInfo.OneTime = oneTime;
					watchInfo.SaveDir = saveDir;
					watchInfo.NextCheck = TickCount.Now;
					watchInfo.ListIndex = listIndex;
					watchInfo.WatchThread = new Thread(WatchThread);
				}
			}

			if (listIndex == -2) return false;

			watchInfo.WatchThread.Start(watchInfo);
			return true;
		}

		private void RemoveThreads(bool removeCompleted, bool removeSelected) {
			using (SoftLock.Obtain(_watchInfoList)) {
				int i = 0;
				while (i < _watchInfoList.Count) {
					WatchInfo watchInfo = _watchInfoList[i];
					if ((removeCompleted || (removeSelected && lvThreads.Items[i].Selected)) &&
						(watchInfo.Stop || !watchInfo.WatchThread.IsAlive))
					{
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
		}

		private void SetStatus(WatchInfo watchInfo, string status) {
			if (watchInfo.ListIndex == -1) return;
			Invoke((MethodInvoker)delegate() {
				var item = lvThreads.Items[watchInfo.ListIndex].SubItems[1];
				if (item.Text != status) {
					item.Text = status;
				}
			});
		}

		private void SaveThreadList() {
			using (SoftLock.Obtain(_watchInfoList)) {
				using (StreamWriter sw = new StreamWriter(Path.Combine(Settings.GetSettingsDir(), Settings.ThreadsFileName))) {
					sw.WriteLine("1"); // File version
					foreach (WatchInfo w in _watchInfoList) {
						if ((w.WatchThread != null) && w.WatchThread.IsAlive) {
							sw.WriteLine(w.PageURL);
							sw.WriteLine(w.PageAuth);
							sw.WriteLine(w.ImageAuth);
							sw.WriteLine(w.WaitSeconds.ToString());
							sw.WriteLine(w.OneTime ? "1" : "0");
							sw.WriteLine(w.SaveDir);
						}
					}
				}
			}
		}

		private void LoadThreadList() {
			const int linesPerThread = 6;
			string path = Path.Combine(Settings.GetSettingsDir(), Settings.ThreadsFileName);
			if (!File.Exists(path)) return;
			string[] lines = File.ReadAllLines(path);
			if ((lines.Length < 1) || (Int32.Parse(lines[0]) != 1)) return;
			if (lines.Length < (1 + linesPerThread)) return;
			if (MessageBox.Show("Would you like to reload the list of active threads from the last run?",
				"Reload Threads", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
			{
				return;
			}
			int i = 1;
			while (i <= lines.Length - linesPerThread) {
				string pageURL = lines[i++];
				string pageAuth = lines[i++];
				string imageAuth = lines[i++];
				int waitSeconds = Math.Max(Int32.Parse(lines[i++]), 60);
				bool oneTime = lines[i++] == "1";
				string saveDir = lines[i++];
				AddThread(pageURL, pageAuth, imageAuth, waitSeconds, oneTime, saveDir);
			}
		}

		private List<string> GetFromSelected(WatchInfoSelector selector) {
			List<string> values = new List<string>();
			using (SoftLock.Obtain(_watchInfoList)) {
				foreach (ListViewItem item in lvThreads.SelectedItems) {
					string value = selector(_watchInfoList[item.Index]);
					if (!String.IsNullOrEmpty(value)) {
						values.Add(value);
					}
				}
			}
			return values;
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

		private Stream GetToStream(string url, string auth, string referer, ref DateTime? cacheTime) {
			HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
			req.UserAgent = (Settings.UseCustomUserAgent == true) ? Settings.CustomUserAgent : ("Chan Thread Watch " + Version);
			req.Referer = referer;
			if (cacheTime != null) {
				req.IfModifiedSince = cacheTime.Value;
			}
			if (!String.IsNullOrEmpty(auth)) {
				req.Headers.Add("Authorization", "Basic " +
					Convert.ToBase64String(Encoding.ASCII.GetBytes(auth)));
			}
			HttpWebResponse resp;
			try {
				resp = (HttpWebResponse)req.GetResponse();
				cacheTime = null;
				if (resp.Headers["Last-Modified"] != null) {
					try {
						// Parse the time string ourself instead of using .LastModified because
						// older versions of Mono don't convert it from GMT to local.
						cacheTime = DateTime.ParseExact(resp.Headers["Last-Modified"], new string[] {
							"r", "dddd, dd-MMM-yy HH:mm:ss G\\MT", "ddd MMM d HH:mm:ss yyyy" },
							CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces |
							DateTimeStyles.AssumeUniversal);
					}
					catch { }
				}
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

		private Stream GetToStream(string url, string auth, string referer) {
			DateTime? cacheTime = null;
			return GetToStream(url, auth, referer, ref cacheTime);
		}

		private string GetToString(string url, string auth, string path, ref DateTime? cacheTime) {
			byte[] respBytes;
			using (Stream respStream = GetToStream(url, auth, null, ref cacheTime)) {
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

		private void GetToFile(string url, string auth, string referer, string path) {
			using (Stream respStream = GetToStream(url, auth, referer)) {
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

		private void ParseURL(string url, out SiteHelper siteHelper, out string site, out string board, out string thread) {
			string[] urlSplit = url.Substring(7).Split(new char[] { '/' },
				StringSplitOptions.RemoveEmptyEntries);
			site = String.Empty;
			if (urlSplit.Length >= 1) {
				string[] hostSplit = urlSplit[0].Split('.');
				if (hostSplit.Length >= 2) {
					site = hostSplit[hostSplit.Length - 2];
				}
			}
			switch (site.ToLower()) {
				case "anonib":
					siteHelper = new SiteHelperAnonIB();
					break;
				default:
					siteHelper = new SiteHelper();
					break;
			}
			board = siteHelper.GetBoardName(urlSplit);
			thread = siteHelper.GetThreadName(urlSplit);
		}

		private void WatchThread(object p) {
			WatchInfo watchInfo = (WatchInfo)p;
			SiteHelper siteHelper;
			string pageURL = watchInfo.PageURL;
			string pageAuth = watchInfo.PageAuth;
			string imgAuth = watchInfo.ImageAuth;
			string page = null;
			string saveDir, saveFilename, savePath, site, board, thread;
			int numTries;
			const int maxTries = 3;
			DateTime pageGetTime = DateTime.Now;
			DateTime? pageCacheTime = null;
			long waitRemain;

			ParseURL(pageURL, out siteHelper, out site, out board, out thread);
			lock (_watchInfoList) {
				saveDir = watchInfo.SaveDir;
			}
			if (String.IsNullOrEmpty(saveDir)) {
				saveDir = Settings.DownloadFolder;
				if (Settings.DownloadFolderIsRelative == true) saveDir = Path.GetFullPath(saveDir);
				saveDir = Path.Combine(saveDir, site + "_" + board + "_" + thread);
				if (!Directory.Exists(saveDir)) {
					Directory.CreateDirectory(saveDir);
				}
				lock (_watchInfoList) {
					watchInfo.SaveDir = saveDir;
				}
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
				lock (_watchInfoList) {
					watchInfo.NextCheck = TickCount.Now + (watchInfo.WaitSeconds * 1000);
				}
				if (page != null) {
					List<string> links = GetLinks(page, pageURL);
					OrderedDictionary imgs = new OrderedDictionary(StringComparer.CurrentCultureIgnoreCase);
					int i;
					for (i = 0; i < links.Count; i++) {
						string originalLink = links[i];
						string link = siteHelper.GetImageLink(originalLink);
						if (!String.IsNullOrEmpty(link)) {
							string imgFilename = URLFilename(link);
							if (!imgs.Contains(imgFilename)) {
								LinkInfo linkInfo = new LinkInfo();
								linkInfo.URL = link;
								linkInfo.Referer = (link == originalLink) ? pageURL : originalLink;
								imgs.Add(imgFilename, linkInfo);
							}
						}
					}
					i = 0;
					foreach (DictionaryEntry imgEntry in imgs) {
						saveFilename = (string)imgEntry.Key;
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
									LinkInfo linkInfo = (LinkInfo)imgEntry.Value;
									GetToFile(linkInfo.URL, imgAuth, linkInfo.Referer, savePath);
									break;
								}
								catch {
								}
							}
						}
						i++;
					}
					page = null;
				}
				while (true) {
					lock (_watchInfoList) {
						waitRemain = watchInfo.NextCheck - TickCount.Now;
						if (waitRemain <= 0) {
							break;
						}
						if (watchInfo.Stop || watchInfo.OneTime) {
							SetStatus(watchInfo, watchInfo.Stop ? "Stopped by user" :
								"Stopped, download finished");
							return;
						}
						SetStatus(watchInfo, String.Format("Waiting {0:0} seconds", Math.Ceiling(waitRemain / 1000.0)));
					}
					Thread.Sleep(200);
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
		public long NextCheck;
	}

	public class LinkInfo {
		public string URL;
		public string Referer;
	}

	public class SiteHelper {
		public virtual string GetBoardName(string[] urlSplit) {
			return (urlSplit.Length >= 2) ? urlSplit[1] : String.Empty;
		}

		public virtual string GetThreadName(string[] urlSplit) {
			if (urlSplit.Length >= 3) {
				string page = urlSplit[urlSplit.Length - 1];
				int pos = page.LastIndexOf('.');
				if (pos != -1) {
					return page.Substring(0, pos);
				}
			}
			return String.Empty;
		}

		public virtual string GetImageLink(string link) {
			if (link.Contains("/src/")) {
				int pos = Math.Max(link.LastIndexOf("http://"), link.LastIndexOf("https://"));
				return (pos > 0) ? link.Substring(pos) : link;
			}
			return null;
		}
	}

	public class SiteHelperAnonIB : SiteHelper {
		public override string GetImageLink(string link) {
			if (link.Contains("/images/")) {
				int pos = link.IndexOf("=http");
				return (pos != -1) ? link.Substring(pos + 1) : link;
			}
			return null;
		}

		public override string GetThreadName(string[] urlSplit) {
			if (urlSplit.Length >= 3) {
				string page = urlSplit[urlSplit.Length - 1];
				int pos = page.IndexOf('?');
				if (pos != -1) {
					string[] urlVarsSplit = page.Substring(pos + 1).Split(new char[] { '&' },
						StringSplitOptions.RemoveEmptyEntries);
					foreach (string v in urlVarsSplit) {
						if (v.StartsWith("t=")) {
							return v.Substring(2);
						}
					}
				}
			}
			return String.Empty;
		}
	}

	public class HTTP404Exception : Exception {
		public HTTP404Exception() {
		}
	}

	public class HTTP304Exception : Exception {
		public HTTP304Exception() {
		}
	}

	public class SoftLock : IDisposable {
		private object _obj;

		public SoftLock(object obj) {
			_obj = obj;
			while (!Monitor.TryEnter(_obj, 10)) Application.DoEvents();
		}

		public static SoftLock Obtain(object obj) {
			return new SoftLock(obj);
		}

		public void Dispose() {
			Dispose(true);
		}

		private void Dispose(bool disposing) {
			if (_obj != null) {
				Monitor.Exit(_obj);
				_obj = null;
			}
		}
	}

	public static class TickCount {
		static object _synchObj = new object();
		static int _lastTickCount = Environment.TickCount;
		static long _correction;

		public static long Now {
			get {
				lock (_synchObj) {
					int tickCount = Environment.TickCount;
					if ((tickCount < 0) && (_lastTickCount >= 0)) {
						_correction += 0x100000000L;
					}
					_lastTickCount = tickCount;
					return tickCount + _correction;
				}
			}
		}
	}

	public delegate string WatchInfoSelector(WatchInfo watchInfo);

	public enum ThreadDoubleClickAction {
		OpenFolder = 1,
		OpenURL = 2
	}
}
