using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Web;
using System.Windows.Forms;

namespace ChanThreadWatch {
	public partial class frmChanThreadWatch : Form {
		private List<WatchInfo> _watchInfoList = new List<WatchInfo>();
		private object _promptSync = new object();

		// Can't lock or Join in the UI thread because if it gets stuck waiting and a worker thread
		// tries to Invoke, it will never return because Invoke needs to run on the (frozen) UI
		// thread.  And I don't like the idea of BeginInvoke in this particular situation.

		// ReleaseDate property and version in AssemblyInfo.cs should be updated for each release.

		// Change log:
		// 1.4.2 (2010-Apr-02):
		//   * Fixed crash with non-4chan threads containing mailto links.
		//   * Enabled auto-scaling to fix text truncation with larger font sizes.
		//   * Enter key is a shortcut for "Add Thread".
		//   * Backs up the page before redownloading in case it 404s in the middle of
		//     downloading.
		//   * Fixed non-breaking spaces being converted to spaces when post-processing
		//     HTML.
		// 1.4.1 (2010-Jan-01):
		//   * Workaround for crash in Mono when program update checking is enabled.
		// 1.4.0 (2010-Jan-01):
		//   * Option to download thumbnails and post-process HTML to create a mostly-
		//     working thread backup (no external CSS, no embedded images other than
		//     thumbnails, etc).
		//   * Fixed handling of special characters in filenames.
		//   * Ability to restart stopped threads in the context menu.
		// 1.3.0 (2009-Dec-28):
		//   * Option to verify hash of downloaded images (currently 4chan only).
		//   * Option to save images with original filenames (currently 4chan only).
		//   * Option to automatically check for program updates (disabled by default).
		//   * Various fixes related to URL parsing, file error handling, etc.
		// 1.2.3 (2009-Dec-25):
		//   * Restores 4chan and AnonIB support.
		//   * Custom link parsing and other code to allow for automatic downloading of
		//     multiple page threads (not implemented for any site at this time as I
		//     didn't find any I wanted to support).
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
		//   * Fixed UI sluggishness and freezing caused by accidentally leaving a Sleep
		//     inside one of the locks for debugging.
		//   * Supports AnonIB.
		// 1.0.0 (2007-Dec-05):
		//   * Initial release.

		public frmChanThreadWatch() {
			InitializeComponent();
			General.SetFontAndScaling(this);

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

			if ((Settings.CheckForUpdates == true) && (Settings.LastUpdateCheck ?? DateTime.MinValue) < DateTime.Now.Date) {
				CheckForUpdates();
			}
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

		private void txtPageURL_KeyDown(object sender, KeyEventArgs e) {
			if (e.KeyCode == Keys.Enter) {
				btnAdd_Click(null, null);
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
			pageURL = General.ProperURL(HttpUtility.HtmlDecode(pageURL));
			if (pageURL == null) {
				MessageBox.Show("The specified URL is invalid.", "Invalid URL", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
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

		private void miStart_Click(object sender, EventArgs e) {
			using (SoftLock.Obtain(_watchInfoList)) {
				foreach (ListViewItem item in lvThreads.SelectedItems) {
					WatchInfo wi = _watchInfoList[item.Index];
					if ((wi.WatchThread == null) || !wi.WatchThread.IsAlive) {
						AddThread(wi.PageURL, wi.PageAuth, wi.ImageAuth, wi.WaitSeconds, wi.OneTime, wi.SaveDir);
					}
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
			MessageBox.Show(String.Format("Chan Thread Watch{0}Version {1} ({2}){0}jart1126@yahoo.com{0}{3}",
				Environment.NewLine, General.Version, General.ReleaseDate, General.ProgramURL), "About",
				MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		private void lvThreads_KeyDown(object sender, KeyEventArgs e) {
			if (e.KeyCode == Keys.Delete) {
				RemoveThreads(false, true);
			}
		}

		private void lvThreads_MouseClick(object sender, MouseEventArgs e) {
			if (e.Button == MouseButtons.Right) {
				if (lvThreads.SelectedItems.Count != 0) {
					bool anyRunning = false;
					bool anyStopped = false;
					using (SoftLock.Obtain(_watchInfoList)) {
						foreach (ListViewItem item in lvThreads.SelectedItems) {
							WatchInfo wi = _watchInfoList[item.Index];
							bool isRunning = (wi.WatchThread != null) && wi.WatchThread.IsAlive;
							anyRunning |= isRunning;
							anyStopped |= !isRunning;
						}
					}
					miStop.Visible = anyRunning;
					miStart.Visible = anyStopped;
					miCheckNow.Visible = anyRunning;
					miCheckEvery.Visible = anyRunning;
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
			const int liDuplicate = -2;
			const int liNotFound = -1;
			WatchInfo watchInfo = new WatchInfo();
			int listIndex = liNotFound;

			using (SoftLock.Obtain(_watchInfoList)) {
				foreach (WatchInfo w in _watchInfoList) {
					if (String.Equals(w.PageURL, pageURL, StringComparison.OrdinalIgnoreCase)) {
						listIndex = ((w.WatchThread != null) && w.WatchThread.IsAlive) ? liDuplicate : w.ListIndex;
						break;
					}
				}
				if (listIndex != liDuplicate) {
					if (listIndex == liNotFound) {
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

			if (listIndex == liDuplicate) return false;

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
			using (SoftLock.Obtain(_promptSync)) {
				if (MessageBox.Show("Would you like to reload the list of active threads from the last run?",
					"Reload Threads", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
				{
					return;
				}
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

		private void CheckForUpdates() {
			Thread thread = new Thread(CheckForUpdateThread);
			thread.IsBackground = true;
			thread.Start();
		}

		private void CheckForUpdateThread() {
			string html;
			try {
				html = General.GetToString(General.ProgramURL);
			}
			catch {
				return;
			}
			Settings.LastUpdateCheck = DateTime.Now.Date;
			string openTag = "[LatestVersion]";
			string closeTag = "[/LatestVersion]";
			int start = html.IndexOf(openTag, StringComparison.OrdinalIgnoreCase);
			if (start == -1) return;
			start += openTag.Length;
			int end = html.IndexOf(closeTag, start, StringComparison.OrdinalIgnoreCase);
			if (end == -1) return;
			string latestStr = html.Substring(start, end - start).Trim();
			int latest = ParseVersionNumber(latestStr);
			if (latest == -1) return;
			int current = -1;
			if (!String.IsNullOrEmpty(Settings.LatestUpdateVersion)) {
				current = ParseVersionNumber(Settings.LatestUpdateVersion);
			}
			if (current == -1) {
				current = ParseVersionNumber(General.Version);
			}
			if (latest > current) {
				lock (_promptSync) {
					if (IsDisposed) return;
					Settings.LatestUpdateVersion = latestStr;
					Invoke((MethodInvoker)delegate() {
						if (MessageBox.Show("A newer version of Chan Thread Watch is available.  Would you like to open the Chan Thread Watch website?",
							"Newer Version Found", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
						{
							Process.Start(General.ProgramURL);
						}
					});
				}
			}
		}

		private int ParseVersionNumber(string str) {
			string[] split = str.Split('.');
			int num = 0;
			try {
				if (split.Length >= 1) num |= (Int32.Parse(split[0]) & 0x7F) << 24;
				if (split.Length >= 2) num |= (Int32.Parse(split[1]) & 0xFF) << 16;
				if (split.Length >= 3) num |= (Int32.Parse(split[2]) & 0xFF) <<  8;
				if (split.Length >= 4) num |= (Int32.Parse(split[3]) & 0xFF);
				return num;
			}
			catch {
				return -1;
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

		private void WatchThread(object p) {
			WatchInfo watchInfo = (WatchInfo)p;
			SiteHelper siteHelper;
			List<PageInfo> pageList = new List<PageInfo>();
			var completedImages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			var completedThumbs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			string pageURL = watchInfo.PageURL;
			string pageAuth = watchInfo.PageAuth;
			string imgAuth = watchInfo.ImageAuth;
			string page = null;
			string saveDir, saveFilename, savePath, saveThumbsDir, site, board, thread;
			int pageIndex;
			int numTries;
			const int maxTries = 3;
			long waitRemain;

			siteHelper = SiteHelper.GetInstance(pageURL);
			siteHelper.SetURL(pageURL);
			site = siteHelper.GetSiteName();
			board = siteHelper.GetBoardName();
			thread = siteHelper.GetThreadName();
			lock (_watchInfoList) {
				saveDir = watchInfo.SaveDir;
			}
			if (String.IsNullOrEmpty(saveDir)) {
				saveDir = Settings.DownloadFolder;
				if (Settings.DownloadFolderIsRelative == true) saveDir = Path.GetFullPath(saveDir);
				saveDir = Path.Combine(saveDir, General.CleanFilename(site + "_" + board + "_" + thread));
				if (!Directory.Exists(saveDir)) {
					try { Directory.CreateDirectory(saveDir); }
					catch {
						lock (_watchInfoList) {
							SetStatus(watchInfo, "Stopped, unable to create folder");
							return;
						}
					}
				}
				lock (_watchInfoList) {
					watchInfo.SaveDir = saveDir;
				}
			}
			saveThumbsDir = Path.Combine(saveDir, "thumbs");

			pageList.Add(new PageInfo { URL = pageURL });

			while (true) {
				Queue<ImageInfo> pendingImages = new Queue<ImageInfo>();
				Queue<ThumbnailInfo> pendingThumbs = new Queue<ThumbnailInfo>();

				pageIndex = 0;
				do {
					saveFilename = General.CleanFilename(thread) + ((pageIndex == 0) ? String.Empty : ("_" + (pageIndex + 1))) + ".html";
					savePath = Path.Combine(saveDir, saveFilename);

					PageInfo pageInfo = pageList[pageIndex];
					pageInfo.IsFresh = false;
					pageInfo.Path = savePath;
					pageInfo.ReplaceList = (Settings.SaveThumbnails == true) ? new List<ReplaceInfo>() : null;
					for (numTries = 1; numTries <= maxTries; numTries++) {
						lock (_watchInfoList) {
							if (watchInfo.Stop) {
								SetStatus(watchInfo, "Stopped by user");
								return;
							}
							SetStatus(watchInfo, String.Format("Downloading page{0}{1}",
								(pageIndex == 0) ? String.Empty : (" " + (pageIndex + 1)),
								(numTries == 1) ? String.Empty : (" (retry " + (numTries - 1) + ")")));
						}
						try {
							page = General.GetToString(pageInfo.URL, pageAuth, savePath, true, ref pageInfo.CacheTime,
								out pageInfo.Encoding, pageInfo.ReplaceList);
							pageInfo.IsFresh = true;
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
						catch (Exception ex) {
							if ((ex is DirectoryNotFoundException) || (ex is PathTooLongException) || (ex is UnauthorizedAccessException)) {
								lock (_watchInfoList) {
									SetStatus(watchInfo, "Stopped, unable to write file");
									return;
								}
							}
							page = null;
						}
					}

					if (page != null) {
						siteHelper.SetHTML(page);

						List<ThumbnailInfo> thumbs = new List<ThumbnailInfo>();
						List<ImageInfo> images = siteHelper.GetImages(pageInfo.ReplaceList, thumbs);
						if (completedImages.Count == 0) {
							var completedImageDiskNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
							foreach (ImageInfo image in images) {
								for (int iName = 0; iName < 2; iName++) {
									int iSuffix = 1;
									string filename;
									do {
										filename = ((iName == 0) ? image.FileName : image.OriginalFileName) +
											((iSuffix == 1) ? String.Empty : ("_" + iSuffix)) + image.Extension;
										iSuffix++;
									}
									while (completedImageDiskNames.ContainsKey(filename));
									if (File.Exists(Path.Combine(saveDir, filename))) {
										completedImageDiskNames[filename] = 0;
										completedImages[image.FileName] = filename;
										break;
									}
								}
							}
							foreach (ThumbnailInfo thumb in thumbs) {
								if (File.Exists(Path.Combine(saveThumbsDir, thumb.FileNameWithExt))) {
									completedThumbs[thumb.FileNameWithExt] = 0;
								}
							}
						}
						foreach (ImageInfo image in images) {
							if (!completedImages.ContainsKey(image.FileName)) {
								pendingImages.Enqueue(image);
							}
						}
						foreach (ThumbnailInfo thumb in thumbs) {
							if (!completedThumbs.ContainsKey(thumb.FileNameWithExt)) {
								pendingThumbs.Enqueue(thumb);
							}
						}

						string nextPageURL = siteHelper.GetNextPageURL();
						if (!String.IsNullOrEmpty(nextPageURL)) {
							PageInfo nextPageInfo = new PageInfo { URL = nextPageURL };
							if (pageIndex == pageList.Count - 1) {
								pageList.Add(nextPageInfo);
							}
							else if (pageList[pageIndex + 1].URL != nextPageURL) {
								pageList[pageIndex + 1] = nextPageInfo;
							}
						}
						else if (pageIndex < pageList.Count - 1) {
							pageList.RemoveRange(pageIndex + 1, pageList.Count - (pageIndex + 1));
						}

						page = null;
					}
				}
				while (++pageIndex < pageList.Count);

				lock (_watchInfoList) {
					watchInfo.NextCheck = TickCount.Now + (watchInfo.WaitSeconds * 1000);
				}

				int totalImageCount = completedImages.Count + pendingImages.Count;
				while (pendingImages.Count != 0) {
					ImageInfo image = pendingImages.Dequeue();
					bool pathTooLong = false;

				MakeImagePath:
					if ((Settings.UseOriginalFilenames == true) && !String.IsNullOrEmpty(image.OriginalFileName) && !pathTooLong) {
						saveFilename = image.OriginalFileName;
					}
					else if (!String.IsNullOrEmpty(image.FileName)) {
						saveFilename = image.FileName;
					}
					else {
						continue;
					}

					int iSuffix = 1;
					do {
						savePath = Path.Combine(saveDir, saveFilename + ((iSuffix == 1) ?
							String.Empty : ("_" + iSuffix)) + image.Extension) ;
						iSuffix++;
					}
					while (File.Exists(savePath));

					bool downloadCompleted = false;
					byte[] prevHash = null;
					for (numTries = 1; numTries <= maxTries; numTries++) {
						lock (_watchInfoList) {
							if (watchInfo.Stop) {
								SetStatus(watchInfo, "Stopped by user");
								return;
							}
							SetStatus(watchInfo, String.Format("Downloading image {0} of {1}{2}",
								totalImageCount - pendingImages.Count, totalImageCount,
								(numTries == 1) ? String.Empty : (" (retry " + (numTries - 1) + ")")));
						}
						try {
							HashType hashType = (Settings.VerifyImageHashes != false) ? image.HashType : HashType.None;
							byte[] hash = General.GetToFile(image.URL, imgAuth, image.Referer, savePath, hashType);
							if (hashType != HashType.None && !General.ArraysAreEqual(hash, image.Hash) &&
								(prevHash == null || !General.ArraysAreEqual(hash, prevHash)))
							{
								prevHash = hash;
								throw new Exception("Hash of downloaded file is incorrect.");
							}
							downloadCompleted = true;
							break;
						}
						catch (HTTP404Exception) {
							break;
						}
						catch (Exception ex) {
							if (ex is PathTooLongException) {
								if (!pathTooLong) {
									pathTooLong = true;
									goto MakeImagePath;
								}
								else {
									downloadCompleted = true;
									break;
								}
							}
							if ((ex is DirectoryNotFoundException) || (ex is UnauthorizedAccessException)) {
								lock (_watchInfoList) {
									SetStatus(watchInfo, "Stopped, unable to write file");
									return;
								}
							}
						}
					}
					if (downloadCompleted) {
						completedImages[image.FileName] = Path.GetFileName(savePath);
					}
				}

				if (Settings.SaveThumbnails == true) {
					if ((pendingThumbs.Count != 0) && !Directory.Exists(saveThumbsDir)) {
						try { Directory.CreateDirectory(saveThumbsDir); }
						catch {
							lock (_watchInfoList) {
								SetStatus(watchInfo, "Stopped, unable to create folder");
								return;
							}
						}
					}

					int totalThumbCount = completedThumbs.Count + pendingThumbs.Count;
					while (pendingThumbs.Count != 0) {
						ThumbnailInfo thumb = pendingThumbs.Dequeue();

						savePath = Path.Combine(saveThumbsDir, thumb.FileNameWithExt);

						bool downloadCompleted = false;
						for (numTries = 1; numTries <= maxTries; numTries++) {
							lock (_watchInfoList) {
								if (watchInfo.Stop) {
									SetStatus(watchInfo, "Stopped by user");
									return;
								}
								SetStatus(watchInfo, String.Format("Downloading thumbnail {0} of {1}{2}",
									totalThumbCount - pendingThumbs.Count, totalThumbCount,
									(numTries == 1) ? String.Empty : (" (retry " + (numTries - 1) + ")")));
							}
							try {
								General.GetToFile(thumb.URL, pageAuth, thumb.Referer, savePath, HashType.None);
								downloadCompleted = true;
								break;
							}
							catch (HTTP404Exception) {
								break;
							}
							catch (Exception ex) {
								if (ex is PathTooLongException) {
									downloadCompleted = true;
									break;
								}
								if ((ex is DirectoryNotFoundException) || (ex is UnauthorizedAccessException)) {
									lock (_watchInfoList) {
										SetStatus(watchInfo, "Stopped, unable to write file");
										return;
									}
								}
							}
						}
						if (downloadCompleted) {
							completedThumbs[thumb.FileNameWithExt] = 0;
						}
					}

					foreach (PageInfo pageInfo in pageList) {
						if (!pageInfo.IsFresh) continue;
						page = General.BytesToString(File.ReadAllBytes(pageInfo.Path), pageInfo.Encoding);
						for (int i = 0; i < pageInfo.ReplaceList.Count; i++) {
							ReplaceInfo replace = pageInfo.ReplaceList[i];
							if ((replace.Type == ReplaceType.ImageLinkHref) &&
								completedImages.TryGetValue(replace.Tag, out saveFilename))
							{
								replace.Value = "href=\"" + HttpUtility.HtmlAttributeEncode(saveFilename) + "\"";
							}
							if (replace.Type == ReplaceType.ImageSrc) {
								replace.Value = "src=\"thumbs/" + HttpUtility.HtmlAttributeEncode(replace.Tag) + "\"";
							}
						}
						General.AddOtherReplaces(page, pageInfo.ReplaceList);
						using (StreamWriter sw = new StreamWriter(pageInfo.Path, false, pageInfo.Encoding)) {
							General.WriteReplacedString(page, pageInfo.ReplaceList, sw);
						}
						if (General.FindElementClose(page, "html", 0) != -1 && File.Exists(pageInfo.Path + ".bak")) {
							try { File.Delete(pageInfo.Path + ".bak"); }
							catch { }
						}
					}
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
}
