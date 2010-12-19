using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web;
using System.Windows.Forms;
using ChanThreadWatch.Properties;

namespace ChanThreadWatch {
	public partial class frmChanThreadWatch : Form {
		private object _startupPromptSync = new object();
		private bool _isExiting;
		private bool _saveThreadList;
		private int _itemAreaY;
		private int[] _columnWidths;

		// ReleaseDate property and version in AssemblyInfo.cs should be updated for each release.

		public frmChanThreadWatch() {
			InitializeComponent();
			Icon = Resources.ChanThreadWatchIcon;
			int initialWidth = ClientSize.Width;
			General.SetFontAndScaling(this);
			float scaleFactorX = (float)ClientSize.Width / initialWidth;
			_columnWidths = new int[lvThreads.Columns.Count];
			for (int iColumn = 0; iColumn < lvThreads.Columns.Count; iColumn++) {
				ColumnHeader column = lvThreads.Columns[iColumn];
				column.Width = Convert.ToInt32(column.Width * scaleFactorX);
				_columnWidths[iColumn] = column.Width;
			}
			General.EnableDoubleBuffering(lvThreads);

			// Shouldn't matter since the limit is supposed to be per connection group
			ServicePointManager.DefaultConnectionLimit = Int32.MaxValue;

			Settings.Load();

			BuildCheckEverySubMenu();
			BuildColumnHeaderMenu();

			if ((Settings.DownloadFolder == null) || !Directory.Exists(Settings.AbsoluteDownloadDir)) {
				Settings.DownloadFolder = Path.Combine(Environment.GetFolderPath(
					Environment.SpecialFolder.MyDocuments), "Watched Threads");
				Settings.DownloadFolderIsRelative = false;
			}
			if (Settings.OnThreadDoubleClick == null) {
				Settings.OnThreadDoubleClick = ThreadDoubleClickAction.OpenFolder;
			}
			if (Settings.SaveThumbnails == null) {
				Settings.SaveThumbnails = true;
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
				if (rbEditDescription.Checked)
					return ThreadDoubleClickAction.EditDescription;
				else if (rbOpenURL.Checked)
					return ThreadDoubleClickAction.OpenURL;
				else
					return ThreadDoubleClickAction.OpenFolder;
			}
			set {
				if (value == ThreadDoubleClickAction.EditDescription)
					rbEditDescription.Checked = true;
				else if (value == ThreadDoubleClickAction.OpenURL)
					rbOpenURL.Checked = true;
				else
					rbOpenFolder.Checked = true;
			}
		}

		private void frmChanThreadWatch_Shown(object sender, EventArgs e) {
			lvThreads.Items.Add(new ListViewItem());
			_itemAreaY = lvThreads.GetItemRect(0).Y;
			lvThreads.Items.RemoveAt(0);

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

			// Send the stop notifications before the final save of the thread list so that
			// the watchers know not to change anything (e.g. rename thread download folder)
			foreach (ThreadWatcher watcher in ThreadWatchers) {
				watcher.Stop(StopReason.Exiting);
			}

			SaveThreadList();

			_isExiting = true;
			foreach (ThreadWatcher watcher in ThreadWatchers) {
				while (!watcher.WaitUntilStopped(10)) {
					Application.DoEvents();
				}
			}

			Program.ReleaseMutex();
		}

		private void frmChanThreadWatch_DragEnter(object sender, DragEventArgs e) {
			if (e.Data.GetDataPresent("UniformResourceLocatorW") ||
				e.Data.GetDataPresent("UniformResourceLocator"))
			{
				if ((e.AllowedEffect & DragDropEffects.Copy) != 0) {
					e.Effect = DragDropEffects.Copy;
				}
				else if ((e.AllowedEffect & DragDropEffects.Link) != 0) {
					e.Effect = DragDropEffects.Link;
				}
			}
		}

		private void frmChanThreadWatch_DragDrop(object sender, DragEventArgs e) {
			if (_isExiting) return;
			string url = null;
			if (e.Data.GetDataPresent("UniformResourceLocatorW")) {
				byte[] data = ((MemoryStream)e.Data.GetData("UniformResourceLocatorW")).ToArray();
				url = Encoding.Unicode.GetString(data, 0, General.StrLenW(data) * 2);
			}
			else if (e.Data.GetDataPresent("UniformResourceLocator")) {
				byte[] data = ((MemoryStream)e.Data.GetData("UniformResourceLocator")).ToArray();
				url = Encoding.Default.GetString(data, 0, General.StrLen(data));
			}
			url = FormatURLFromUser(url);
			if (url != null) {
				AddThread(url);
				_saveThreadList = true;
			}
		}

		private void txtPageURL_KeyDown(object sender, KeyEventArgs e) {
			if (e.KeyCode == Keys.Enter) {
				btnAdd_Click(txtPageURL, null);
				e.SuppressKeyPress = true;
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
			_saveThreadList = true;
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
			_saveThreadList = true;
		}

		private void btnRemoveCompleted_Click(object sender, EventArgs e) {
			RemoveThreads(true, false);
		}

		private void miStop_Click(object sender, EventArgs e) {
			foreach (ThreadWatcher watcher in SelectedThreadWatchers) {
				watcher.Stop(StopReason.UserRequest);
			}
			_saveThreadList = true;
		}

		private void miStart_Click(object sender, EventArgs e) {
			if (_isExiting) return;
			foreach (ThreadWatcher watcher in SelectedThreadWatchers) {
				if (!watcher.IsRunning) {
					watcher.Start();
				}
			}
			_saveThreadList = true;
		}

		private void miEditDescription_Click(object sender, EventArgs e) {
			foreach (ThreadWatcher watcher in SelectedThreadWatchers) {
				using (frmThreadDescription descriptionForm = new frmThreadDescription()) {
					descriptionForm.Description = watcher.Description;
					if (descriptionForm.ShowDialog() == DialogResult.OK) {
						watcher.Description = descriptionForm.Description;
						DisplayDescription(watcher);
						_saveThreadList = true;
					}
				}
				break;
			}
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
			_saveThreadList = true;
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
			else if (e.Control && e.KeyCode == Keys.A) {
				foreach (ListViewItem item in lvThreads.Items) {
					item.Selected = true;
				}
			}
			else if (e.Control && e.KeyCode == Keys.I) {
				foreach (ListViewItem item in lvThreads.Items) {
					item.Selected = !item.Selected;
				}
			}
		}

		private void lvThreads_MouseClick(object sender, MouseEventArgs e) {
			if (e.Button == MouseButtons.Right) {
				int selectedCount = lvThreads.SelectedItems.Count;
				if (selectedCount != 0) {
					bool anyRunning = false;
					bool anyStopped = false;
					foreach (ThreadWatcher watcher in SelectedThreadWatchers) {
						bool isRunning = watcher.IsRunning;
						anyRunning |= isRunning;
						anyStopped |= !isRunning;
					}
					miEditDescription.Visible = selectedCount == 1;
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
			if (OnThreadDoubleClick == ThreadDoubleClickAction.EditDescription) {
				miEditDescription_Click(null, null);
			}
			else if (OnThreadDoubleClick == ThreadDoubleClickAction.OpenFolder) {
				miOpenFolder_Click(null, null);
			}
			else {
				miOpenURL_Click(null, null);
			}
		}

		private void lvThreads_ColumnClick(object sender, ColumnClickEventArgs e) {
			ListViewItemSorter sorter = (ListViewItemSorter)lvThreads.ListViewItemSorter;
			if (sorter == null) {
				sorter = new ListViewItemSorter(e.Column);
				lvThreads.ListViewItemSorter = sorter;
			}
			else if (e.Column != sorter.Column) {
				sorter.Column = e.Column;
				sorter.Ascending = true;
			}
			else {
				sorter.Ascending = !sorter.Ascending;
			}
			lvThreads.Sort();
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

		private void tmrSaveThreadList_Tick(object sender, EventArgs e) {
			if (_saveThreadList) {
				SaveThreadList();
				_saveThreadList = false;
			}
		}

		private void tmrUpdateWaitStatus_Tick(object sender, EventArgs e) {
			UpdateWaitingWatcherStatuses();
		}

		private void ThreadWatcher_DownloadStatus(ThreadWatcher watcher, DownloadStatusEventArgs args) {
			WatcherExtraData extraData = (WatcherExtraData)watcher.Tag;
			bool isInitialPageDownload = false;
			bool isFirstImageUpdate = false;
			if (args.DownloadType == DownloadType.Page) {
				if (!extraData.HasDownloadedPage) {
					extraData.HasDownloadedPage = true;
					isInitialPageDownload = true;
				}
				extraData.PreviousDownloadWasPage = true;
			}
			if (args.DownloadType == DownloadType.Image && extraData.PreviousDownloadWasPage) {
				extraData.LastImageOn = DateTime.Now;
				extraData.PreviousDownloadWasPage = false;
				isFirstImageUpdate = true;
			}
			BeginInvoke(() => {
				SetDownloadStatus(watcher, args.DownloadType, args.CompleteCount, args.TotalCount);
				if (isInitialPageDownload) {
					DisplayDescription(watcher);
					_saveThreadList = true;
				}
				if (isFirstImageUpdate) {
					DisplayLastImageOn(watcher);
					_saveThreadList = true;
				}
				SetupWaitTimer();
			});
		}

		private void ThreadWatcher_WaitStatus(ThreadWatcher watcher, EventArgs args) {
			BeginInvoke(() => {
				SetWaitStatus(watcher);
				SetupWaitTimer();
			});
		}

		private void ThreadWatcher_StopStatus(ThreadWatcher watcher, StopStatusEventArgs args) {
			BeginInvoke(() => {
				SetStopStatus(watcher, args.StopReason);
				SetupWaitTimer();
				if (args.StopReason != StopReason.UserRequest && args.StopReason != StopReason.Exiting) {
					_saveThreadList = true;
				}
			});
		}

		private void ThreadWatcher_ThreadDownloadDirectoryRename(ThreadWatcher watcher, EventArgs args) {
			BeginInvoke(() => {
				_saveThreadList = true;
			});
		}

		private bool AddThread(string pageURL) {
			string pageAuth = (chkPageAuth.Checked && (txtPageAuth.Text.IndexOf(':') != -1)) ? txtPageAuth.Text : String.Empty;
			string imageAuth = (chkImageAuth.Checked && (txtImageAuth.Text.IndexOf(':') != -1)) ? txtImageAuth.Text : String.Empty;
			int waitSeconds = Int32.Parse((string)cboCheckEvery.SelectedItem) * 60;
			return AddThread(pageURL, pageAuth, imageAuth, waitSeconds, chkOneTime.Checked, null, String.Empty, null, null);
		}

		private bool AddThread(string pageURL, string pageAuth, string imageAuth, int checkInterval, bool oneTime, string saveDir, string description, StopReason? stopReason, WatcherExtraData extraData) {
			ThreadWatcher watcher = null;
			ListViewItem newListViewItem = null;

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
				watcher.Description = description;
				watcher.DownloadStatus += ThreadWatcher_DownloadStatus;
				watcher.WaitStatus += ThreadWatcher_WaitStatus;
				watcher.StopStatus += ThreadWatcher_StopStatus;
				watcher.ThreadDownloadDirectoryRename += ThreadWatcher_ThreadDownloadDirectoryRename;

				newListViewItem = new ListViewItem(String.Empty);
				for (int i = 1; i < lvThreads.Columns.Count; i++) {
					newListViewItem.SubItems.Add(String.Empty);
				}
				newListViewItem.Tag = watcher;
				lvThreads.Items.Add(newListViewItem);
			}

			watcher.PageAuth = pageAuth;
			watcher.ImageAuth = imageAuth;
			watcher.CheckIntervalSeconds = checkInterval;
			watcher.OneTimeDownload = oneTime;

			if (extraData == null) {
				extraData = watcher.Tag as WatcherExtraData;
				if (extraData == null) {
					extraData = new WatcherExtraData {
						AddedOn = DateTime.Now
					};
				}
			}
			if (newListViewItem != null) {
				extraData.ListViewItem = newListViewItem;
			}
			watcher.Tag = extraData;

			DisplayDescription(watcher);
			DisplayAddedOn(watcher);
			DisplayLastImageOn(watcher);
			if (stopReason == null) {
				watcher.Start();
			}
			else {
				watcher.Stop(stopReason.Value);
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
			if (url.IndexOf('/', url.IndexOf("//") + 2) == -1) return null;
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
				}
				else {
					i++;
				}
			}
			_saveThreadList = true;
		}

		private void BuildCheckEverySubMenu() {
			for (int i = 0; i < cboCheckEvery.Items.Count; i++) {
				int minutes = Int32.Parse((string)cboCheckEvery.Items[i]);
				MenuItem menuItem = new MenuItem {
					Index = i,
					Tag = minutes,
					Text = minutes + " Minute" + ((minutes != 1) ? "s" : String.Empty)
				};
				menuItem.Click += miCheckEvery_Click;
				miCheckEvery.MenuItems.Add(menuItem);
			}
		}

		private void BuildColumnHeaderMenu() {
			ContextMenu contextMenu = new ContextMenu();
			contextMenu.Popup += (s, e) => {
				for (int i = 0; i < lvThreads.Columns.Count; i++) {
					contextMenu.MenuItems[i].Checked = lvThreads.Columns[i].Width != 0;
				}
			};
			for (int i = 0; i < lvThreads.Columns.Count; i++) {
				MenuItem menuItem = new MenuItem {
					Index = i,
					Tag = i,
					Text = lvThreads.Columns[i].Text
				};
				menuItem.Click += (s, e) => {
					int iColumn = (int)((MenuItem)s).Tag;
					ColumnHeader column = lvThreads.Columns[iColumn];
					if (column.Width != 0) {
						_columnWidths[iColumn] = column.Width;
						column.Width = 0;
					}
					else {
						column.Width = _columnWidths[iColumn];
					}
				};
				contextMenu.MenuItems.Add(menuItem);
			}
			ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
			contextMenuStrip.Opening += (s, e) => {
				e.Cancel = true;
				Point pos = lvThreads.PointToClient(Control.MousePosition);
				if (pos.Y >= _itemAreaY) return;
				contextMenu.Show(lvThreads, pos);
			};
			lvThreads.ContextMenuStrip = contextMenuStrip;
		}

		private void SetupWaitTimer() {
			bool anyWaiting = false;
			foreach (ThreadWatcher watcher in ThreadWatchers) {
				if (watcher.IsWaiting) {
					anyWaiting = true;
					break;
				}
			}
			if (!tmrUpdateWaitStatus.Enabled && anyWaiting) {
				tmrUpdateWaitStatus.Start();
			}
			else if (tmrUpdateWaitStatus.Enabled && !anyWaiting) {
				tmrUpdateWaitStatus.Stop();
			}
		}

		private void UpdateWaitingWatcherStatuses() {
			foreach (ThreadWatcher watcher in ThreadWatchers) {
				if (watcher.IsWaiting) {
					SetWaitStatus(watcher);
				}
			}
		}

		private void SetSubItemText(ThreadWatcher watcher, ColumnIndex columnIndex, string text) {
			ListViewItem item = ((WatcherExtraData)watcher.Tag).ListViewItem;
			var subItem = item.SubItems[(int)columnIndex];
			if (subItem.Text != text) {
				subItem.Text = text;
			}
		}

		private void DisplayDescription(ThreadWatcher watcher) {
			SetSubItemText(watcher, ColumnIndex.Description, watcher.Description);
		}

		private void DisplayStatus(ThreadWatcher watcher, string status) {
			SetSubItemText(watcher, ColumnIndex.Status, status);
		}

		private void DisplayAddedOn(ThreadWatcher watcher) {
			DateTime time = ((WatcherExtraData)watcher.Tag).AddedOn;
			SetSubItemText(watcher, ColumnIndex.AddedOn, time.ToString("yyyy/MM/dd HH:mm:ss"));
		}

		private void DisplayLastImageOn(ThreadWatcher watcher) {
			DateTime? time = ((WatcherExtraData)watcher.Tag).LastImageOn;
			SetSubItemText(watcher, ColumnIndex.LastImageOn, time != null ? time.Value.ToString("yyyy/MM/dd HH:mm:ss") : String.Empty);
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
			DisplayStatus(watcher, status);
		}

		private void SetWaitStatus(ThreadWatcher watcher) {
			int remainingSeconds = (watcher.MillisecondsUntilNextCheck + 999) / 1000;
			DisplayStatus(watcher, String.Format("Waiting {0} seconds", remainingSeconds));
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
			DisplayStatus(watcher, status);
		}

		private void SaveThreadList() {
			try {
				using (StreamWriter sw = new StreamWriter(Path.Combine(Settings.GetSettingsDir(), Settings.ThreadsFileName))) {
					sw.WriteLine("3"); // File version
					foreach (ThreadWatcher watcher in ThreadWatchers) {
						WatcherExtraData extraData = (WatcherExtraData)watcher.Tag;
						sw.WriteLine(watcher.PageURL);
						sw.WriteLine(watcher.PageAuth);
						sw.WriteLine(watcher.ImageAuth);
						sw.WriteLine(watcher.CheckIntervalSeconds.ToString());
						sw.WriteLine(watcher.OneTimeDownload ? "1" : "0");
						sw.WriteLine(watcher.ThreadDownloadDirectory != null ? General.GetRelativeDirectoryPath(watcher.ThreadDownloadDirectory, watcher.MainDownloadDirectory) : String.Empty);
						sw.WriteLine((watcher.IsStopping && watcher.StopReason != StopReason.Exiting) ? ((int)watcher.StopReason).ToString() : String.Empty);
						sw.WriteLine(watcher.Description);
						sw.WriteLine(extraData.AddedOn.ToUniversalTime().Ticks.ToString());
						sw.WriteLine(extraData.LastImageOn != null ? extraData.LastImageOn.Value.ToUniversalTime().Ticks.ToString() : String.Empty);
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
					case 3: linesPerThread = 10; break;
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
					string saveDir = lines[i++];
					saveDir = saveDir.Length != 0 ? General.GetAbsoluteDirectoryPath(saveDir, Settings.AbsoluteDownloadDir) : null;
					string description;
					StopReason? stopReason = null;
					WatcherExtraData extraData = new WatcherExtraData();
					if (fileVersion >= 2) {
						string stopReasonLine = lines[i++];
						if (stopReasonLine.Length != 0) {
							stopReason = (StopReason)Int32.Parse(stopReasonLine);
						}
					}
					if (fileVersion >= 3) {
						description = lines[i++];
						extraData.AddedOn = new DateTime(Int64.Parse(lines[i++]), DateTimeKind.Utc).ToLocalTime();
						string lastImageOn = lines[i++];
						if (lastImageOn.Length != 0) {
							extraData.LastImageOn = new DateTime(Int64.Parse(lastImageOn), DateTimeKind.Utc).ToLocalTime();
						}
					}
					else {
						description = String.Empty;
						extraData.AddedOn = DateTime.Now;
					}
					AddThread(pageURL, pageAuth, imageAuth, checkIntervalSeconds, oneTimeDownload, saveDir, description, stopReason, extraData);
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
					Invoke(() => {
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

		private IAsyncResult BeginInvoke(MethodInvoker method) {
			return BeginInvoke((Delegate)method);
		}

		private object Invoke(MethodInvoker method) {
			return Invoke((Delegate)method);
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

		private class ListViewItemSorter : IComparer {
			public int Column { get; set; }
			public bool Ascending { get; set; }

			public ListViewItemSorter(int column) {
				Column = column;
				Ascending = true;
			}

			public int Compare(object x, object y) {
				int cmp = String.Compare(((ListViewItem)x).SubItems[Column].Text, ((ListViewItem)y).SubItems[Column].Text);
				return Ascending ? cmp : -cmp;
			}
		}

		private enum ColumnIndex {
			Description = 0,
			Status = 1,
			LastImageOn = 2,
			AddedOn = 3
		}
	}
}
