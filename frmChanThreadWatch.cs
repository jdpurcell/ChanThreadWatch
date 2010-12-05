using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web;
using System.Windows.Forms;

namespace ChanThreadWatch {
	public partial class frmChanThreadWatch : Form {
		private Dictionary<ThreadWatcher, int> _watcherListIndexes = new Dictionary<ThreadWatcher, int>();
		private object _startupPromptSync = new object();
		private bool _isExiting;

		// ReleaseDate property and version in AssemblyInfo.cs should be updated for each release.

		public frmChanThreadWatch() {
			InitializeComponent();
			int initialWidth = ClientSize.Width;
			General.SetFontAndScaling(this);
			float scaleFactorX = (float)ClientSize.Width / initialWidth;
			foreach (ColumnHeader columnHeader in lvThreads.Columns) {
				columnHeader.Width = Convert.ToInt32(columnHeader.Width * scaleFactorX);
			}
			General.EnableDoubleBuffering(lvThreads);

			// Souldn't matter since the limit is supposed to be per connection group
			ServicePointManager.DefaultConnectionLimit = Int32.MaxValue;

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

			if ((Settings.DownloadFolder == null) || !Directory.Exists(Settings.AbsoluteDownloadDir)) {
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
			LoadThreadList();
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

			SaveThreadList();

			_isExiting = true;
			foreach (ThreadWatcher watcher in ThreadWatchers) {
				watcher.Stop(StopReason.Exiting);
			}
			foreach (ThreadWatcher watcher in ThreadWatchers) {
				while (!watcher.WaitUntilStopped(50)) {
					Application.DoEvents();
				}
			}
			Program.ReleaseMutex();
		}

		private void txtPageURL_KeyDown(object sender, KeyEventArgs e) {
			if (e.KeyCode == Keys.Enter) {
				btnAdd_Click(txtPageURL, null);
			}
		}

		private void btnAdd_Click(object sender, EventArgs e) {
			if (_isExiting) return;
			if (txtPageURL.Text.Trim().Length == 0) return;
			string pageURL = FormatURLFromUser(txtPageURL.Text);
			if (pageURL == null) {
				MessageBox.Show("The specified URL is invalid.", "Invalid URL", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}
			if (!AddThread(pageURL)) {
				MessageBox.Show("The same thread is already being watched or downloaded.",
					"Duplicate Thread", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}
			txtPageURL.Clear();
			txtPageURL.Focus();
			SaveThreadList();
		}

		private void btnAddFromClipboard_Click(object sender, EventArgs e) {
			if (_isExiting) return;
			string text;
			try {
				text = Clipboard.GetText();
			}
			catch {
				return;
			}
			string[] urls = text.Split(new [] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
			for (int iURL = 0; iURL < urls.Length; iURL++) {
				string url = FormatURLFromUser(urls[iURL]);
				if (url == null) continue;
				AddThread(url);
			}
			SaveThreadList();
		}

		private void btnRemoveCompleted_Click(object sender, EventArgs e) {
			RemoveThreads(true, false);
		}

		private void miStop_Click(object sender, EventArgs e) {
			foreach (ThreadWatcher watcher in SelectedThreadWatchers) {
				watcher.Stop(StopReason.UserRequest);
			}
			SaveThreadList();
		}

		private void miStart_Click(object sender, EventArgs e) {
			if (_isExiting) return;
			foreach (ThreadWatcher watcher in SelectedThreadWatchers) {
				if (!watcher.IsRunning) {
					watcher.Start();
				}
			}
			SaveThreadList();
		}

		private void miOpenFolder_Click(object sender, EventArgs e) {
			foreach (ThreadWatcher watcher in SelectedThreadWatchers) {
				try {
					Process.Start(watcher.ThreadDownloadDirectory);
				}
				catch { }
			}
		}

		private void miOpenURL_Click(object sender, EventArgs e) {
			foreach (ThreadWatcher watcher in SelectedThreadWatchers) {
				try {
					Process.Start(watcher.PageURL);
				}
				catch { }
			}
		}

		private void miCopyURL_Click(object sender, EventArgs e) {
			StringBuilder sb = new StringBuilder();
			foreach (ThreadWatcher watcher in SelectedThreadWatchers) {
				if (sb.Length != 0) sb.Append(Environment.NewLine);
				sb.Append(watcher.PageURL);
			}
			try {
				Clipboard.Clear();
				Clipboard.SetText(sb.ToString());
			}
			catch (Exception ex) {
				MessageBox.Show("Unable to copy to clipboard: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void miRemove_Click(object sender, EventArgs e) {
			RemoveThreads(false, true);
		}

		private void miRemoveAndDeleteFolder_Click(object sender, EventArgs e) {
			if (MessageBox.Show("Are you sure you want to delete the selected threads and all associated files from disk?",
				"Delete From Disk", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
			{
				return;
			}
			RemoveThreads(false, true,
				(watcher) => {
					try {
						Directory.Delete(watcher.ThreadDownloadDirectory, true);
					}
					catch { }
				});
		}

		private void miCheckNow_Click(object sender, EventArgs e) {
			foreach (ThreadWatcher watcher in SelectedThreadWatchers) {
				watcher.MillisecondsUntilNextCheck = 0;
			}
		}

		private void miCheckEvery_Click(object sender, EventArgs e) {
			MenuItem menuItem = sender as MenuItem;
			if (menuItem != null) {
				int checkIntervalSeconds = Convert.ToInt32(menuItem.Tag) * 60;
				foreach (ThreadWatcher watcher in SelectedThreadWatchers) {
					watcher.CheckIntervalSeconds = checkIntervalSeconds;
				}
				UpdateWaitingWatcherStatuses();
			}
			SaveThreadList();
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
					foreach (ThreadWatcher watcher in SelectedThreadWatchers) {
						bool isRunning = watcher.IsRunning;
						anyRunning |= isRunning;
						anyStopped |= !isRunning;
					}
					miStop.Visible = anyRunning;
					miStart.Visible = anyStopped;
					miCheckNow.Visible = anyRunning;
					miCheckEvery.Visible = anyRunning;
					miRemove.Visible = anyStopped;
					miRemoveAndDeleteFolder.Visible = anyStopped;
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

		private void tmrUpdateWaitStatus_Tick(object sender, EventArgs e) {
			UpdateWaitingWatcherStatuses();
		}

		private void ThreadWatcher_DownloadStatus(ThreadWatcher watcher, DownloadStatusEventArgs args) {
			BeginInvoke((MethodInvoker)(() => {
				SetDownloadStatus(watcher, args.DownloadType, args.CompleteCount, args.TotalCount);
				SetupWaitTimer();
			}));
		}

		private void ThreadWatcher_WaitStatus(ThreadWatcher watcher, EventArgs args) {
			BeginInvoke((MethodInvoker)(() => {
				SetWaitStatus(watcher);
				SetupWaitTimer();
			}));
		}

		private void ThreadWatcher_StopStatus(ThreadWatcher watcher, StopStatusEventArgs args) {
			BeginInvoke((MethodInvoker)(() => {
				SetStopStatus(watcher, args.StopReason);
				SetupWaitTimer();
				if (args.StopReason != StopReason.UserRequest && args.StopReason != StopReason.Exiting) {
					SaveThreadList();
				}
			}));
		}

		private bool AddThread(string pageURL) {
			string pageAuth = (chkPageAuth.Checked && (txtPageAuth.Text.IndexOf(':') != -1)) ? txtPageAuth.Text : String.Empty;
			string imageAuth = (chkImageAuth.Checked && (txtImageAuth.Text.IndexOf(':') != -1)) ? txtImageAuth.Text : String.Empty;
			int waitSeconds = Int32.Parse((string)cboCheckEvery.SelectedItem) * 60;
			return AddThread(pageURL, pageAuth, imageAuth, waitSeconds, chkOneTime.Checked, null, null);
		}

		private bool AddThread(string pageURL, string pageAuth, string imageAuth, int checkInterval, bool oneTime, string saveDir, StopReason? stopReason) {
			ThreadWatcher watcher = null;

			foreach (ThreadWatcher existingWatcher in ThreadWatchers) {
				if (String.Equals(existingWatcher.PageURL, pageURL, StringComparison.OrdinalIgnoreCase)) {
					if (existingWatcher.IsRunning) return false;
					watcher = existingWatcher;
					break;
				}
			}

			if (watcher == null) {
				watcher = new ThreadWatcher(pageURL);
				watcher.ThreadDownloadDirectory = saveDir;
				watcher.DownloadStatus += ThreadWatcher_DownloadStatus;
				watcher.WaitStatus += ThreadWatcher_WaitStatus;
				watcher.StopStatus += ThreadWatcher_StopStatus;

				ListViewItem item = new ListViewItem(pageURL);
				item.Tag = watcher;
				item.SubItems.Add(String.Empty);
				lvThreads.Items.Add(item);

				_watcherListIndexes[watcher] = lvThreads.Items.Count - 1;
			}

			watcher.PageAuth = pageAuth;
			watcher.ImageAuth = imageAuth;
			watcher.CheckIntervalSeconds = checkInterval;
			watcher.OneTimeDownload = oneTime;

			if (stopReason == null) {
				watcher.Start();
			}
			else {
				watcher.StopReason = stopReason.Value;
				SetStopStatus(watcher, stopReason.Value);
			}

			return true;
		}

		private string FormatURLFromUser(string url) {
			url = url.Trim();
			if (url.Length == 0) return null;
			if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
				!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			{
				url = "http://" + url;
			}
			return General.ProperURL(HttpUtility.HtmlDecode(url));
		}

		private void RemoveThreads(bool removeCompleted, bool removeSelected) {
			RemoveThreads(removeCompleted, removeSelected, null);
		}

		private void RemoveThreads(bool removeCompleted, bool removeSelected, Action<ThreadWatcher> preRemoveAction) {
			int i = 0;
			while (i < lvThreads.Items.Count) {
				ThreadWatcher watcher = (ThreadWatcher)lvThreads.Items[i].Tag;
				if ((removeCompleted || (removeSelected && lvThreads.Items[i].Selected)) && !watcher.IsRunning) {
					if (preRemoveAction != null) {
						try { preRemoveAction(watcher); }
						catch { }
					}
					lvThreads.Items.RemoveAt(i);
					_watcherListIndexes.Remove(watcher);
				}
				else {
					_watcherListIndexes[watcher] = i;
					i++;
				}
			}
			SaveThreadList();
		}

		private void SetupWaitTimer() {
			bool anyWaiting = false;
			foreach (ThreadWatcher watcher in WaitingThreadWatchers) {
				anyWaiting = true;
				break;
			}
			if (!tmrUpdateWaitStatus.Enabled && anyWaiting) {
				tmrUpdateWaitStatus.Start();
			}
			else if (tmrUpdateWaitStatus.Enabled && !anyWaiting) {
				tmrUpdateWaitStatus.Stop();
			}
		}

		private void UpdateWaitingWatcherStatuses() {
			foreach (ThreadWatcher watcher in WaitingThreadWatchers) {
				SetWaitStatus(watcher);
			}
		}

		private void SetStatus(ThreadWatcher watcher, string status) {
			int listIndex;
			if (!_watcherListIndexes.TryGetValue(watcher, out listIndex)) return;
			var subItem = lvThreads.Items[listIndex].SubItems[1];
			if (subItem.Text != status) {
				subItem.Text = status;
			}
		}

		private void SetDownloadStatus(ThreadWatcher watcher, DownloadType downloadType, int completeCount, int totalCount) {
			string type;
			bool hideDetail = false;
			switch (downloadType) {
				case DownloadType.Page:
					type = totalCount == 1 ? "page" : "pages";
					hideDetail = totalCount == 1;
					break;
				case DownloadType.Image:
					type = "images";
					break;
				case DownloadType.Thumbnail:
					type = "thumbnails";
					break;
				default:
					return;
			}
			string status = hideDetail ? "Downloading " + type :
				String.Format("Downloading {0}: {1} of {2} completed", type, completeCount, totalCount);
			SetStatus(watcher, status);
		}

		private void SetWaitStatus(ThreadWatcher watcher) {
			int remainingSeconds = (watcher.MillisecondsUntilNextCheck + 999) / 1000;
			SetStatus(watcher, String.Format("Waiting {0} seconds", remainingSeconds));
		}

		private void SetStopStatus(ThreadWatcher watcher, StopReason stopReason) {
			string status = "Stopped: ";
			switch (stopReason) {
				case StopReason.UserRequest:
					status += "User requested";
					break;
				case StopReason.Exiting:
					status += "Exiting";
					break;
				case StopReason.PageNotFound:
					status += "Page not found";
					break;
				case StopReason.DownloadComplete:
					status += "Download complete";
					break;
				case StopReason.IOError:
					status += "Error writing to disk";
					break;
				default:
					status += "Unknown error";
					break;
			}
			SetStatus(watcher, status);
		}

		private void SaveThreadList() {
			if (_isExiting) return;
			try {
				using (StreamWriter sw = new StreamWriter(Path.Combine(Settings.GetSettingsDir(), Settings.ThreadsFileName))) {
					sw.WriteLine("2"); // File version
					foreach (ThreadWatcher watcher in ThreadWatchers) {
						sw.WriteLine(watcher.PageURL);
						sw.WriteLine(watcher.PageAuth);
						sw.WriteLine(watcher.ImageAuth);
						sw.WriteLine(watcher.CheckIntervalSeconds.ToString());
						sw.WriteLine(watcher.OneTimeDownload ? "1" : "0");
						sw.WriteLine(General.GetRelativeDirectoryPath(watcher.ThreadDownloadDirectory, watcher.MainDownloadDirectory));
						sw.WriteLine(watcher.IsRunning ? String.Empty : ((int)watcher.StopReason).ToString());
					}
				}
			}
			catch { }
		}

		private void LoadThreadList() {
			try {
				string path = Path.Combine(Settings.GetSettingsDir(), Settings.ThreadsFileName);
				if (!File.Exists(path)) return;
				string[] lines = File.ReadAllLines(path);
				if (lines.Length < 1) return;
				int fileVersion = Int32.Parse(lines[0]);
				int linesPerThread;
				switch (fileVersion) {
					case 1: linesPerThread = 6; break;
					case 2: linesPerThread = 7; break;
					default: return;
				}
				if (lines.Length < (1 + linesPerThread)) return;
				int i = 1;
				while (i <= lines.Length - linesPerThread) {
					string pageURL = lines[i++];
					string pageAuth = lines[i++];
					string imageAuth = lines[i++];
					int checkIntervalSeconds = Math.Max(Int32.Parse(lines[i++]), 60);
					bool oneTimeDownload = lines[i++] == "1";
					string saveDir = General.GetAbsoluteDirectoryPath(lines[i++], Settings.AbsoluteDownloadDir);
					StopReason? stopReason = null;
					if (fileVersion >= 2) {
						string stopReasonLine = lines[i++];
						if (stopReasonLine.Length != 0) {
							stopReason = (StopReason)Int32.Parse(stopReasonLine);
						}
					}
					AddThread(pageURL, pageAuth, imageAuth, checkIntervalSeconds, oneTimeDownload, saveDir, stopReason);
				}
			}
			catch { }
		}

		private void CheckForUpdates() {
			Thread thread = new Thread(CheckForUpdateThread);
			thread.IsBackground = true;
			thread.Start();
		}

		private void CheckForUpdateThread() {
			string html;
			try {
				html = General.DownloadPageToString(General.ProgramURL);
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
			int current = ParseVersionNumber(General.Version);
			if (!String.IsNullOrEmpty(Settings.LatestUpdateVersion)) {
				current = Math.Max(current, ParseVersionNumber(Settings.LatestUpdateVersion));
			}
			if (latest > current) {
				lock (_startupPromptSync) {
					if (IsDisposed) return;
					Settings.LatestUpdateVersion = latestStr;
					Invoke((MethodInvoker)(() => {
						if (MessageBox.Show("A newer version of Chan Thread Watch is available.  Would you like to open the Chan Thread Watch website?",
							"Newer Version Found", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
						{
							Process.Start(General.ProgramURL);
						}
					}));
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

		private IEnumerable<ThreadWatcher> ThreadWatchers {
			get {
				foreach (ListViewItem item in lvThreads.Items) {
					yield return (ThreadWatcher)item.Tag;
				}
			}
		}

		private IEnumerable<ThreadWatcher> SelectedThreadWatchers {
			get {
				foreach (ListViewItem item in lvThreads.SelectedItems) {
					yield return (ThreadWatcher)item.Tag;
				}
			}
		}

		private IEnumerable<ThreadWatcher> WaitingThreadWatchers {
			get {
				foreach (KeyValuePair<ThreadWatcher, int> kvp in _watcherListIndexes) {
					if (kvp.Key.IsWaiting) {
						yield return kvp.Key;
					}
				}
			}
		}
	}
}
