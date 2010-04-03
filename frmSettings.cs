using System;
using System.IO;
using System.Windows.Forms;

namespace ChanThreadWatch {
	public partial class frmSettings : Form {
		public frmSettings() {
			InitializeComponent();
		}

		private void frmSettings_Load(object sender, EventArgs e) {
			txtDownloadFolder.Text = Settings.DownloadFolder;
			chkCustomUserAgent.Checked = Settings.UseCustomUserAgent ?? false;
			txtCustomUserAgent.Text = Settings.CustomUserAgent ?? String.Empty;
		}

		private void btnOK_Click(object sender, EventArgs e) {
			try {
				string downloadFolder = txtDownloadFolder.Text.Trim();

				if (downloadFolder.Length == 0) {
					throw new Exception("You must enter a download folder.");
				}
				if (!Directory.Exists(downloadFolder)) {
					try {
						Directory.CreateDirectory(downloadFolder);
					}
					catch {
						throw new Exception("Unable to create the download folder.");
					}
				}

				Settings.DownloadFolder = downloadFolder;
				Settings.UseCustomUserAgent = chkCustomUserAgent.Checked;
				Settings.CustomUserAgent = txtCustomUserAgent.Text;
				DialogResult = DialogResult.OK;
			}
			catch (Exception ex) {
				MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void btnBrowse_Click(object sender, EventArgs e) {
			FolderBrowserDialog dialog = new FolderBrowserDialog();
			dialog.Description = "Select the download location.";
			dialog.ShowNewFolderButton = true;
			if (dialog.ShowDialog() == DialogResult.OK) {
				txtDownloadFolder.Text = dialog.SelectedPath;
			}
		}

		private void chkCustomUserAgent_CheckedChanged(object sender, EventArgs e) {
			txtCustomUserAgent.Enabled = chkCustomUserAgent.Checked;
		}
	}
}
