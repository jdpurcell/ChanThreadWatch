using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ChanThreadWatch {
	public partial class frmSettings : Form {
		public frmSettings() {
			InitializeComponent();
			General.SetFontAndScaling(this);
		}

		private void frmSettings_Load(object sender, EventArgs e) {
			txtDownloadFolder.Text = Settings.DownloadFolder;
			chkRelativePath.Checked = Settings.DownloadFolderIsRelative ?? false;
			chkCustomUserAgent.Checked = Settings.UseCustomUserAgent ?? false;
			txtCustomUserAgent.Text = Settings.CustomUserAgent ?? String.Empty;
			chkSaveThumbnails.Checked = Settings.SaveThumbnails ?? false;
			chkUseOriginalFilenames.Checked = Settings.UseOriginalFilenames ?? false;
			chkVerifyImageHashes.Checked = Settings.VerifyImageHashes ?? true;
			chkCheckForUpdates.Checked = Settings.CheckForUpdates ?? false;
			if (Settings.UseExeDirForSettings == true) {
				rbSettingsInExeFolder.Checked = true;
			}
			else {
				rbSettingsInAppDataFolder.Checked = true;
			}
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
				Settings.DownloadFolderIsRelative = chkRelativePath.Checked;
				Settings.UseCustomUserAgent = chkCustomUserAgent.Checked;
				Settings.CustomUserAgent = txtCustomUserAgent.Text;
				Settings.SaveThumbnails = chkSaveThumbnails.Checked;
				Settings.UseOriginalFilenames = chkUseOriginalFilenames.Checked;
				Settings.VerifyImageHashes = chkVerifyImageHashes.Checked;
				Settings.CheckForUpdates = chkCheckForUpdates.Checked;
				Settings.UseExeDirForSettings = rbSettingsInExeFolder.Checked;

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
				SetDownloadFolderTextBox(dialog.SelectedPath);
			}
		}

		private void chkRelativePath_CheckedChanged(object sender, EventArgs e) {
			SetDownloadFolderTextBox(txtDownloadFolder.Text.Trim());
		}

		private void chkCustomUserAgent_CheckedChanged(object sender, EventArgs e) {
			txtCustomUserAgent.Enabled = chkCustomUserAgent.Checked;
		}

		private void SetDownloadFolderTextBox(string path) {
			if (path.Length == 0) {
			}
			else if (Path.IsPathRooted(path) && chkRelativePath.Checked) {
				Uri appDirUri = new Uri(Path.Combine(Settings.ExeDir, "dummy.bin"));
				Uri downloadDirUri = new Uri(Path.Combine(path, "dummy.bin"));
				path = Uri.UnescapeDataString(appDirUri.MakeRelativeUri(downloadDirUri).ToString());
				path = (path.Length == 0) ? "." : Path.GetDirectoryName(path.Replace('/', Path.DirectorySeparatorChar));
			}
			else if (!Path.IsPathRooted(path) && !chkRelativePath.Checked) {
				path = Path.GetFullPath(path);
			}
			txtDownloadFolder.Text = path;
		}
	}
}
