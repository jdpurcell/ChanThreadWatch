// --------------------------------------------------------------------------------
// Copyright (c) J.D. Purcell
//
// Licensed under the MIT License (see LICENSE.txt)
// --------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using JDP.Properties;

namespace JDP {
	public partial class frmChanThreadWatch : Form {
		private const string _uiDateTimeFormat = "yyyy/MM/dd HH:mm:ss";

		private readonly Dictionary<long, DownloadProgressInfo> _downloadProgresses = new Dictionary<long, DownloadProgressInfo>();
		private frmDownloads _downloadForm;
		private bool _isExiting;
		private bool _saveThreadList;

		public frmChanThreadWatch() {
			InitializeComponent();
			Icon = Resources.ChanThreadWatchIcon;
			GUI.SetFontAndScaling(this);
			GUI.ScaleColumns(lvThreads);
			GUI.EnableDoubleBuffering(lvThreads);

			Settings.Load();

			BindCheckEveryList();
			BuildCheckEverySubMenu();
			BuildColumnHeaderMenu();

			if (String.IsNullOrEmpty(Settings.DownloadFolder) || !Directory.Exists(Settings.AbsoluteDownloadDirectory)) {
				Settings.DownloadFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Watched Threads");
				Settings.DownloadFolderIsRelative = false;
			}

			chkPageAuth.Checked = Settings.UsePageAuth;
			txtPageAuth.Text = Settings.PageAuth;
			chkImageAuth.Checked = Settings.UseImageAuth;
			txtImageAuth.Text = Settings.ImageAuth;
			chkOneTime.Checked = Settings.OneTimeDownload;
			cboCheckEvery.SelectedValue = Settings.CheckEvery;
			if (cboCheckEvery.SelectedIndex == -1) cboCheckEvery.SelectedValue = 3;
			ThreadDoubleClickAction = Settings.ThreadDoubleClickAction;
		}

		public Dictionary<long, DownloadProgressInfo> DownloadProgresses => _downloadProgresses;

		private ThreadDoubleClickAction ThreadDoubleClickAction {
			get {
				return
					rbEditDescription.Checked ? ThreadDoubleClickAction.EditDescription :
					rbOpenURL.Checked ? ThreadDoubleClickAction.OpenURL :
					ThreadDoubleClickAction.OpenFolder;
			}
			set {
				var item =
					value == ThreadDoubleClickAction.EditDescription ? rbEditDescription :
					value == ThreadDoubleClickAction.OpenURL ? rbOpenURL :
					rbOpenFolder;
				item.Checked = true;
			}
		}

		private void frmChanThreadWatch_Shown(object sender, EventArgs e) {
			GUI.EnsureScrollBarVisible(lvThreads);

			LoadThreadList();
		}

		private void frmChanThreadWatch_FormClosed(object sender, FormClosedEventArgs e) {
			Settings.UsePageAuth = chkPageAuth.Checked;
			Settings.PageAuth = txtPageAuth.Text;
			Settings.UseImageAuth = chkImageAuth.Checked;
			Settings.ImageAuth = txtImageAuth.Text;
			Settings.OneTimeDownload = chkOneTime.Checked;
			Settings.CheckEvery = (int)cboCheckEvery.SelectedValue;
			Settings.ThreadDoubleClickAction = ThreadDoubleClickAction;
			try {
				Settings.Save();
			}
			catch { }

			foreach (ThreadWatcher watcher in ThreadList.Items) {
				watcher.Stop(StopReason.Exiting);
			}

			// Save before waiting in addition to after in case the wait hangs or is interrupted
			SaveThreadList();

			_isExiting = true;
			foreach (ThreadWatcher watcher in ThreadList.Items) {
				while (!watcher.WaitUntilStopped(10)) {
					Application.DoEvents();
				}
			}

			SaveThreadList();
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
			url = General.CleanPageURL(url);
			if (url != null) {
				AddThread(url, silent: true);
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
			string pageURL = General.CleanPageURL(txtPageURL.Text);
			if (pageURL == null) {
				ShowErrorMessage("The specified URL is invalid.", "Invalid URL");
				return;
			}
			if (!AddThread(pageURL)) {
				return;
			}
			txtPageURL.Clear();
			txtPageURL.Focus();
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
			string[] urls = General.NormalizeNewLines(text).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
			for (int iURL = 0; iURL < urls.Length; iURL++) {
				string url = General.CleanPageURL(urls[iURL]);
				if (url == null) continue;
				AddThread(url, silent: true);
			}
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
			if (_isExiting) return;
			foreach (ThreadWatcher watcher in SelectedThreadWatchers) {
				using (frmThreadDescription descriptionForm = new frmThreadDescription()) {
					descriptionForm.Description = watcher.Description;
					if (descriptionForm.ShowDialog(this) == DialogResult.OK) {
						watcher.Description = descriptionForm.Description;
						DisplayDescription(watcher);
						_saveThreadList = true;
					}
				}
				break;
			}
		}

		private void miOpenFolder_Click(object sender, EventArgs e) {
			int selectedCount = lvThreads.SelectedItems.Count;
			if (selectedCount > 5 && MessageBox.Show(this, $"Do you want to open the folders of all {selectedCount} selected items?",
				"Open Folders", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
			{
				return;
			}
			foreach (ThreadWatcher watcher in SelectedThreadWatchers) {
				string dir = watcher.ThreadDownloadDirectory;
				if (dir == null) continue;
				ThreadPool.QueueUserWorkItem((s) => {
					try {
						using (Process.Start(dir)) { }
					}
					catch { }
				});
			}
		}

		private void miOpenURL_Click(object sender, EventArgs e) {
			int selectedCount = lvThreads.SelectedItems.Count;
			if (selectedCount > 5 && MessageBox.Show(this, $"Do you want to open the URLs of all {selectedCount} selected items?",
				"Open URLs", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
			{
				return;
			}
			foreach (ThreadWatcher watcher in SelectedThreadWatchers) {
				string url = watcher.PageURL;
				ThreadPool.QueueUserWorkItem((s) => {
					try {
						using (Process.Start(url)) { }
					}
					catch { }
				});
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
				ShowErrorMessage("Unable to copy to clipboard: " + ex.Message, "Error");
			}
		}

		private void miRemove_Click(object sender, EventArgs e) {
			RemoveThreads(false, true);
		}

		private void miRemoveAndDeleteFolder_Click(object sender, EventArgs e) {
			if (MessageBox.Show(this, "Are you sure you want to delete the selected threads and all associated files from disk?",
				"Delete From Disk", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
			{
				return;
			}
			RemoveThreads(false, true,
				(watcher) => {
					string dir = watcher.ThreadDownloadDirectory;
					if (dir == null) return;
					Directory.Delete(watcher.ThreadDownloadDirectory, true);
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

		private void miPostprocessFiles_Click(object sender, EventArgs e) {
			var tasks = new List<FilePostprocessingTask>();

			foreach (ThreadWatcher watcher in SelectedThreadWatchers) {
				if (watcher.IsRunning) continue;
				IFilePostprocessor siteHelper = SiteHelper.CreateByHost(watcher.PageHost) as IFilePostprocessor;
				if (siteHelper == null) continue;
				string downloadDirectory = watcher.ThreadDownloadDirectory;
				if (downloadDirectory == null) continue;
				tasks.Add(new FilePostprocessingTask {
					SiteHelper = siteHelper,
					DownloadDirectory = downloadDirectory
				});
			}

			bool anyFailed = false;
			frmWait.RunWork(this, () => {
				foreach (FilePostprocessingTask task in tasks) {
					try {
						task.SiteHelper.PostprocessFiles(task.DownloadDirectory);
					}
					catch {
						anyFailed = true;
					}
				}
			});
			if (anyFailed) {
				ShowErrorMessage("File post-processing failed.", "Error");
			}
		}

		private void btnDownloads_Click(object sender, EventArgs e) {
			if (_downloadForm != null && !_downloadForm.IsDisposed) {
				_downloadForm.Activate();
			}
			else {
				_downloadForm = new frmDownloads(this);
				GUI.CenterChildForm(this, _downloadForm);
				_downloadForm.Show(this);
			}
		}

		private void btnSettings_Click(object sender, EventArgs e) {
			if (_isExiting) return;
			using (frmSettings settingsForm = new frmSettings()) {
				settingsForm.ShowDialog(this);
			}
		}

		private void btnAbout_Click(object sender, EventArgs e) {
			MessageBox.Show(this, String.Format("Chan Thread Watch{0}Version {1}{0}Author: J.D. Purcell",
				Environment.NewLine, General.Version), "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
					bool anyCanPostprocess = false;
					foreach (ThreadWatcher watcher in SelectedThreadWatchers) {
						bool isRunning = watcher.IsRunning;
						anyRunning |= isRunning;
						anyStopped |= !isRunning;
						anyCanPostprocess |= !isRunning && SiteHelper.CreateByHost(watcher.PageHost) is IFilePostprocessor;
					}
					miEditDescription.Visible = selectedCount == 1;
					miStop.Visible = anyRunning;
					miStart.Visible = anyStopped;
					miCheckNow.Visible = anyRunning;
					miCheckEvery.Visible = anyRunning;
					miRemove.Visible = anyStopped;
					miRemoveAndDeleteFolder.Visible = anyStopped;
					miPostprocessFiles.Visible = anyCanPostprocess;
					cmThreads.Show(lvThreads, e.Location);
				}
			}
		}

		private void lvThreads_MouseDoubleClick(object sender, MouseEventArgs e) {
			if (ThreadDoubleClickAction == ThreadDoubleClickAction.EditDescription) {
				miEditDescription_Click(null, null);
			}
			else if (ThreadDoubleClickAction == ThreadDoubleClickAction.OpenFolder) {
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
			if (_saveThreadList && !_isExiting) {
				SaveThreadList();
				_saveThreadList = false;
			}
		}

		private void tmrUpdateWaitStatus_Tick(object sender, EventArgs e) {
			UpdateWaitingWatcherStatuses();
		}

		private void tmrMaintenance_Tick(object sender, EventArgs e) {
			lock (_downloadProgresses) {
				if (_downloadProgresses.Count == 0) return;
				List<long> oldDownloadIDs = new List<long>();
				long ticksNow = TickCount.Now;
				foreach (DownloadProgressInfo info in _downloadProgresses.Values) {
					if (info.EndTicks != null && ticksNow - info.EndTicks.Value > 5000) {
						oldDownloadIDs.Add(info.DownloadID);
					}
				}
				foreach (long downloadID in oldDownloadIDs) {
					_downloadProgresses.Remove(downloadID);
				}
			}
		}

		private void ThreadWatcher_FoundNewImage(ThreadWatcher watcher, EventArgs args) {
			this.BeginInvoke(() => {
				DisplayLastImageOn(watcher);
				_saveThreadList = true;
			});
		}

		private void ThreadWatcher_DownloadStatus(ThreadWatcher watcher, DownloadStatusEventArgs args) {
			this.BeginInvoke(() => {
				SetDownloadStatus(watcher, args.DownloadType, args.CompleteCount, args.TotalCount);
				SetupWaitTimer();
			});
		}

		private void ThreadWatcher_WaitStatus(ThreadWatcher watcher, EventArgs args) {
			this.BeginInvoke(() => {
				SetWaitStatus(watcher);
				SetupWaitTimer();
			});
		}

		private void ThreadWatcher_StopStatus(ThreadWatcher watcher, EventArgs args) {
			this.BeginInvoke(() => {
				SetStopStatus(watcher);
				SetupWaitTimer();
				if (watcher.StopReason != StopReason.UserRequest && watcher.StopReason != StopReason.Exiting) {
					_saveThreadList = true;
				}
			});
		}

		private void ThreadWatcher_ThreadDownloadDirectoryRename(ThreadWatcher watcher, EventArgs args) {
			this.BeginInvoke(() => {
				_saveThreadList = true;
			});
		}

		private void ThreadWatcher_DownloadStart(ThreadWatcher watcher, DownloadStartEventArgs args) {
			DownloadProgressInfo info = new DownloadProgressInfo();
			info.DownloadID = args.DownloadID;
			info.URL = args.URL;
			info.TryNumber = args.TryNumber;
			info.StartTicks = TickCount.Now;
			info.TotalSize = args.TotalSize;
			lock (_downloadProgresses) {
				_downloadProgresses[args.DownloadID] = info;
			}
		}

		private void ThreadWatcher_DownloadProgress(ThreadWatcher watcher, DownloadProgressEventArgs args) {
			lock (_downloadProgresses) {
				DownloadProgressInfo info;
				if (!_downloadProgresses.TryGetValue(args.DownloadID, out info)) return;
				info.DownloadedSize = args.DownloadedSize;
				_downloadProgresses[args.DownloadID] = info;
			}
		}

		private void ThreadWatcher_DownloadEnd(ThreadWatcher watcher, DownloadEndEventArgs args) {
			lock (_downloadProgresses) {
				DownloadProgressInfo info;
				if (!_downloadProgresses.TryGetValue(args.DownloadID, out info)) return;
				info.EndTicks = TickCount.Now;
				info.DownloadedSize = args.DownloadedSize;
				info.TotalSize = args.DownloadedSize;
				_downloadProgresses[args.DownloadID] = info;
			}
		}

		private void ShowErrorMessage(string message, string title) {
			MessageBox.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
		}

		private bool AddThread(string pageURL, bool silent = false) {
			string pageAuth = chkPageAuth.Checked && txtPageAuth.Text.IndexOf(':') != -1 ? txtPageAuth.Text : "";
			string imageAuth = chkImageAuth.Checked && txtImageAuth.Text.IndexOf(':') != -1 ? txtImageAuth.Text : "";
			int checkIntervalSeconds = (int)cboCheckEvery.SelectedValue * 60;

			bool wasTransformSuccessful = false;
			frmWait.RunWork(this, () => {
				try {
					pageURL = URLTransformer.Transform(pageURL, pageAuth);
					wasTransformSuccessful = true;
				}
				catch { }
			});
			if (!wasTransformSuccessful) {
				if (!silent) ShowErrorMessage("Unable to transform the URL.", "Error");
				return false;
			}

			bool wasAdded = AddThread(pageURL, pageAuth, imageAuth, checkIntervalSeconds, chkOneTime.Checked);
			if (!wasAdded) {
				if (!silent) ShowErrorMessage("The same thread is already being watched or downloaded.", "Duplicate Thread");
				return false;
			}

			return true;
		}

		private bool AddThread(string pageURL, string pageAuth, string imageAuth, int checkIntervalSeconds, bool oneTimeDownload) {
			string globalThreadID = SiteHelper.CreateByURL(pageURL).GetGlobalThreadID();
			ThreadWatcher watcher = ThreadList.Items.FirstOrDefault(w => w.GlobalThreadID.Equals(globalThreadID, StringComparison.OrdinalIgnoreCase));

			if (watcher == null) {
				watcher = ThreadWatcher.Create(pageURL, pageAuth, imageAuth, oneTimeDownload, checkIntervalSeconds);

				AttachWatcherToUI(watcher);
				DisplayDescription(watcher);
				DisplayAddedOn(watcher);

				ThreadList.Add(watcher);
			}
			else {
				if (watcher.IsRunning) {
					return false;
				}
				watcher.PageAuth = pageAuth;
				watcher.ImageAuth = imageAuth;
				watcher.CheckIntervalSeconds = checkIntervalSeconds;
				watcher.OneTimeDownload = oneTimeDownload;
			}

			watcher.Start();

			_saveThreadList = true;

			return true;
		}

		private void AttachWatcherToUI(ThreadWatcher watcher) {
			if (watcher.Tag != null) {
				throw new Exception("Watcher's tag is already set.");
			}

			watcher.FoundNewImage += ThreadWatcher_FoundNewImage;
			watcher.DownloadStatus += ThreadWatcher_DownloadStatus;
			watcher.WaitStatus += ThreadWatcher_WaitStatus;
			watcher.StopStatus += ThreadWatcher_StopStatus;
			watcher.ThreadDownloadDirectoryRename += ThreadWatcher_ThreadDownloadDirectoryRename;
			watcher.DownloadStart += ThreadWatcher_DownloadStart;
			watcher.DownloadProgress += ThreadWatcher_DownloadProgress;
			watcher.DownloadEnd += ThreadWatcher_DownloadEnd;

			ListViewItem newListViewItem = new ListViewItem("");
			for (int i = 1; i < lvThreads.Columns.Count; i++) {
				newListViewItem.SubItems.Add("");
			}
			newListViewItem.Tag = watcher;
			lvThreads.Items.Add(newListViewItem);

			watcher.Tag = newListViewItem;
		}

		private void RemoveThreads(bool removeCompleted, bool removeSelected, Action<ThreadWatcher> preRemoveAction = null) {
			int i = 0;
			while (i < lvThreads.Items.Count) {
				ThreadWatcher watcher = (ThreadWatcher)lvThreads.Items[i].Tag;
				if ((removeCompleted || (removeSelected && lvThreads.Items[i].Selected)) && !watcher.IsRunning) {
					try { preRemoveAction?.Invoke(watcher); }
					catch { }
					lvThreads.Items.RemoveAt(i);
					ThreadList.Remove(watcher);
				}
				else {
					i++;
				}
			}
			_saveThreadList = true;
		}

		private void BindCheckEveryList() {
			cboCheckEvery.ValueMember = "Value";
			cboCheckEvery.DisplayMember = "Text";
			cboCheckEvery.DataSource = new[] {
				new ListItemInt32(0, "1 or <"),
				new ListItemInt32(2, "2"),
				new ListItemInt32(3, "3"),
				new ListItemInt32(5, "5"),
				new ListItemInt32(10, "10"),
				new ListItemInt32(60, "60")
			};
		}

		private void BuildCheckEverySubMenu() {
			for (int i = 0; i < cboCheckEvery.Items.Count; i++) {
				int minutes = ((ListItemInt32)cboCheckEvery.Items[i]).Value;
				MenuItem menuItem = new MenuItem {
					Index = i,
					Tag = minutes,
					Text = minutes > 0 ? minutes + " Minutes" : "1 Minute or <"
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
					if (column.Tag == null) {
						column.Tag = column.Width;
						column.Width = 0;
					}
					else {
						column.Width = (int)column.Tag;
						column.Tag = null;
					}
				};
				contextMenu.MenuItems.Add(menuItem);
			}
			ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
			contextMenuStrip.Opening += (s, e) => {
				e.Cancel = true;
				Point pos = lvThreads.PointToClient(MousePosition);
				if (pos.X < 0 || pos.X > lvThreads.ClientSize.Width || pos.Y < 0 || pos.Y >= GUI.GetHeaderHeight(lvThreads)) return;
				contextMenu.Show(lvThreads, pos);
			};
			lvThreads.ContextMenuStrip = contextMenuStrip;
		}

		private void SetupWaitTimer() {
			bool anyWaiting = ThreadList.Items.Any(w => w.IsWaiting);
			if (!tmrUpdateWaitStatus.Enabled && anyWaiting) {
				tmrUpdateWaitStatus.Start();
			}
			else if (tmrUpdateWaitStatus.Enabled && !anyWaiting) {
				tmrUpdateWaitStatus.Stop();
			}
		}

		private void UpdateWaitingWatcherStatuses() {
			foreach (ThreadWatcher watcher in ThreadList.Items) {
				if (watcher.IsWaiting) {
					SetWaitStatus(watcher);
				}
			}
		}

		private void SetSubItemText(ThreadWatcher watcher, ColumnIndex columnIndex, string text) {
			ListViewItem item = (ListViewItem)watcher.Tag;
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
			SetSubItemText(watcher, ColumnIndex.AddedOn, watcher.AddedOn.ToLocalTime().ToString(_uiDateTimeFormat));
		}

		private void DisplayLastImageOn(ThreadWatcher watcher) {
			SetSubItemText(watcher, ColumnIndex.LastImageOn, watcher.LastImageOn?.ToLocalTime().ToString(_uiDateTimeFormat));
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
			string status = hideDetail ? $"Downloading {type}" : $"Downloading {type}: {completeCount} of {totalCount} completed";
			DisplayStatus(watcher, status);
		}

		private void SetWaitStatus(ThreadWatcher watcher) {
			int remainingSeconds = (watcher.MillisecondsUntilNextCheck + 999) / 1000;
			DisplayStatus(watcher, $"Waiting {remainingSeconds} seconds");
		}

		private void SetStopStatus(ThreadWatcher watcher) {
			string status = "Stopped: ";
			switch (watcher.StopReason) {
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
				ThreadList.Save();
			}
			catch { }
		}

		private void LoadThreadList() {
			ThreadList.Load((watcher) => {
				AttachWatcherToUI(watcher);
				DisplayDescription(watcher);
				DisplayAddedOn(watcher);
				DisplayLastImageOn(watcher);
				if (watcher.IsStopping) {
					SetStopStatus(watcher);
				}
			});
		}

		private IEnumerable<ThreadWatcher> SelectedThreadWatchers =>
			lvThreads.SelectedItems.Cast<ListViewItem>().Select(n => (ThreadWatcher)n.Tag);

		private enum ColumnIndex {
			Description = 0,
			Status = 1,
			LastImageOn = 2,
			AddedOn = 3
		}
	}
}
