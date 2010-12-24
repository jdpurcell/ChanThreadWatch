using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ChanThreadWatch {
	public partial class frmDownloads : Form {
		private Dictionary<string, ListViewItem> _itemsByURL = new Dictionary<string, ListViewItem>(StringComparer.OrdinalIgnoreCase);

		public frmDownloads() {
			InitializeComponent();
			General.SetFontAndScaling(this);
			General.EnableDoubleBuffering(lvDownloads);
		}

		private void frmDownloads_FormClosing(object sender, FormClosingEventArgs e) {
			if (e.CloseReason == CloseReason.UserClosing) {
				e.Cancel = true;
				Hide();
			}
		}

		public void ThreadWatcher_DownloadProgress(ThreadWatcher watcher, DownloadProgressEventArgs args) {
			ListViewItem item;
			bool isNewItem = false;
			lock (_itemsByURL) {
				if (!_itemsByURL.TryGetValue(args.URL, out item)) {
					item = new ListViewItem(String.Empty);
					for (int i = 1; i < lvDownloads.Columns.Count; i++) {
						item.SubItems.Add(String.Empty);
					}
					_itemsByURL[args.URL] = item;
					isNewItem = true;
				}
			}
			BeginInvoke((MethodInvoker)(() => {
				if (isNewItem) {
					item.SubItems[(int)ColumnIndex.URL].Text = args.URL;
					item.SubItems[(int)ColumnIndex.Kilobytes].Text = GetKilobytesString(args.TotalSize);
					lvDownloads.Items.Add(item);
				}
				if (args.TotalSize != null) {
					item.SubItems[(int)ColumnIndex.Percent].Text = (args.DownloadedSize * 100 / args.TotalSize.Value).ToString();
				}
				else {
					item.SubItems[(int)ColumnIndex.Kilobytes].Text = GetKilobytesString(args.DownloadedSize);
				}
				if (args.DownloadedSize == args.TotalSize) {
					lvDownloads.Items.Remove(item);
					lock (_itemsByURL) {
						_itemsByURL.Remove(args.URL);
					}
				}
			}));
		}

		private string GetKilobytesString(long? byteSize) {
			if (byteSize == null) return String.Empty;
			return (byteSize.Value / 1024).ToString("#,##0");
		}

		private enum ColumnIndex {
			URL = 0,
			Percent = 1,
			Kilobytes = 2
		}
	}
}
