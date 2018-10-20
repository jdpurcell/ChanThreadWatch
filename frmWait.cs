using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Forms;

namespace JDP {
	public partial class frmWait : Form {
		private readonly object _workSync;
		private readonly object _progressSync = new object();
		private bool _isWorkComplete;
		private double? _reportedProgress;
		private double? _displayedProgress;

		public frmWait(object workSync) {
			InitializeComponent();
			GUI.SetFontAndScaling(this);

			_workSync = workSync;
		}

		private void frmWait_Shown(object sender, EventArgs e) {
			Monitor.Exit(_workSync);
		}

		private void frmWait_FormClosing(object sender, FormClosingEventArgs e) {
			e.Cancel = !_isWorkComplete;
		}

		private void tmrUpdateProgress_Tick(object sender, EventArgs e) {
			lock (_progressSync) {
				if (_reportedProgress == _displayedProgress) return;
				_displayedProgress = _reportedProgress;
			}
			lblMessage.Text = _displayedProgress == null ? "Please wait..." : $"Please wait ({_displayedProgress * 100.0:0.0}%)...";
		}

		private void OnWorkComplete() {
			this.BeginInvoke(() => {
				_isWorkComplete = true;
				Close();
			});
		}

		private void OnProgress(double progress) {
			lock (_progressSync) {
				_reportedProgress = progress;
			}
		}

		// For work that should block the UI but not the UI thread. The work happens in a
		// new thread and the UI is blocked with a modal dialog.
		public static void RunWork(Form owner, Action<ProgressReporter> action) {
			object workSync = new object();
			using (var waitForm = new frmWait(workSync)) {
				ExceptionDispatchInfo exception = null;
				bool triggered = false;
				Thread thread = new Thread(() => {
					try {
						action(waitForm.OnProgress);
					}
					catch (Exception ex) {
						exception = ExceptionDispatchInfo.Capture(ex);
					}
					lock (workSync) {
						if (triggered) {
							waitForm.OnWorkComplete();
						}
						triggered = true;
					}
				});
				thread.Start();
				if (thread.Join(2)) return;
				Monitor.Enter(workSync);
				if (triggered) {
					Monitor.Exit(workSync);
				}
				else {
					triggered = true;
					waitForm.ShowDialog(owner);
				}
				thread.Join();
				exception?.Throw();
			}
		}
	}
}
