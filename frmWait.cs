using System;
using System.Threading;
using System.Windows.Forms;

namespace JDP {
	public partial class frmWait : Form {
		private readonly object _sync;
		private bool _isWorkComplete;

		public frmWait(object sync) {
			InitializeComponent();
			GUI.SetFontAndScaling(this);

			_sync = sync;
		}

		private void frmWait_Shown(object sender, EventArgs e) {
			Monitor.Exit(_sync);
		}

		private void frmWait_FormClosing(object sender, FormClosingEventArgs e) {
			e.Cancel = !_isWorkComplete;
		}

		public void OnWorkComplete() {
			this.BeginInvoke(() => {
				_isWorkComplete = true;
				Close();
			});
		}

		// For work that should block the UI but not the UI thread. The work happens in a
		// new thread and the UI is blocked with a modal dialog.
		public static void RunWork(Form owner, Action action) {
			object sync = new object();
			using (var waitForm = new frmWait(sync)) {
				bool captured = false;
				Thread thread = new Thread(() => {
					action();
					lock (sync) {
						if (captured) {
							waitForm.OnWorkComplete();
						}
						captured = true;
					}
				});
				thread.Start();
				if (thread.Join(2)) return;
				Monitor.Enter(sync);
				if (captured) {
					Monitor.Exit(sync);
				}
				else {
					captured = true;
					waitForm.ShowDialog(owner);
				}
				thread.Join();
			}
		}
	}
}
