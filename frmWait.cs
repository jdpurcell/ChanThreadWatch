using System.Windows.Forms;

namespace JDP {
	public partial class frmWait : Form {
		private volatile bool _isWorkComplete;
		private volatile bool _hasShown;

		public frmWait() {
			InitializeComponent();
			GUI.SetFontAndScaling(this);
		}

		private void frmWait_Shown(object sender, System.EventArgs e) {
			_hasShown = true;
			if (_isWorkComplete) {
				Close();
			}
		}

		private void frmWait_FormClosing(object sender, FormClosingEventArgs e) {
			if (!_isWorkComplete) {
				e.Cancel = true;
			}
		}

		public void OnWorkComplete() {
			_isWorkComplete = true;
			if (!_hasShown) return;
			this.BeginInvoke(() => {
				Close();
			});
		}
	}
}
