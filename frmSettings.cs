﻿using System;
using System.IO;
using System.Windows.Forms;

namespace JDP {
	public partial class frmSettings : Form {
		public frmSettings() {
			InitializeComponent();
			GUI.SetFontAndScaling(this);
		}

		private void frmSettings_Load(object sender, EventArgs e) {
			txtDownloadFolder.Text = Settings.DownloadFolder;
			chkRelativePath.Checked = Settings.DownloadFolderIsRelative ?? false;
			chkCustomUserAgent.Checked = Settings.UseCustomUserAgent ?? false;
			txtCustomUserAgent.Text = Settings.CustomUserAgent ?? String.Empty;
			chkSaveThumbnails.Checked = Settings.SaveThumbnails ?? true;
			chkRenameDownloadFolderWithDescription.Checked = Settings.RenameDownloadFolderWithDescription ?? false;
			chkUseOriginalFileNames.Checked = Settings.UseOriginalFileNames ?? false;
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

				string oldSettingsFolder = Settings.GetSettingsDir();
				string newSettingsFolder = Settings.GetSettingsDir(rbSettingsInExeFolder.Checked);
				if (!String.Equals(newSettingsFolder, oldSettingsFolder, StringComparison.OrdinalIgnoreCase)) {
					if (!Program.ObtainMutex(newSettingsFolder)) {
						MessageBox.Show("Another instance of this program is using the same settings folder.",
							"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
						return;
					}
					try {
						foreach (string fileName in new[] { Settings.SettingsFileName, Settings.ThreadsFileName }) {
							string oldPath = Path.Combine(oldSettingsFolder, fileName);
							string newPath = Path.Combine(newSettingsFolder, fileName);
							if (!File.Exists(oldPath)) continue;
							byte[] contents = File.ReadAllBytes(oldPath);
							File.WriteAllBytes(newPath, contents);
							try { File.Delete(oldPath); } catch { }
						}
					}
					catch {
						MessageBox.Show("Unable to move the settings files.",
							"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
						return;
					}
				}

				string oldAbsoluteDownloadFolder = Settings.AbsoluteDownloadDir;

				Settings.DownloadFolder = downloadFolder;
				Settings.DownloadFolderIsRelative = chkRelativePath.Checked;
				Settings.UseCustomUserAgent = chkCustomUserAgent.Checked;
				Settings.CustomUserAgent = txtCustomUserAgent.Text;
				Settings.SaveThumbnails = chkSaveThumbnails.Checked;
				Settings.RenameDownloadFolderWithDescription = chkRenameDownloadFolderWithDescription.Checked;
				Settings.UseOriginalFileNames = chkUseOriginalFileNames.Checked;
				Settings.VerifyImageHashes = chkVerifyImageHashes.Checked;
				Settings.CheckForUpdates = chkCheckForUpdates.Checked;
				Settings.UseExeDirForSettings = rbSettingsInExeFolder.Checked;

				try {
					Settings.Save();
				}
				catch { }

				if (!String.Equals(Settings.AbsoluteDownloadDir, oldAbsoluteDownloadFolder, StringComparison.OrdinalIgnoreCase)) {
					MessageBox.Show("The new download folder will not affect threads currently being watched until the program is restared.  " +
						"If you are still watching the threads at next run, make sure you have moved their download folders into the new download folder.",
						"Download Folder Changed", MessageBoxButtons.OK, MessageBoxIcon.Information);
				}

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
			txtDownloadFolder.Text = chkRelativePath.Checked ?
				General.GetRelativeDirectoryPath(path, Settings.ExeDir) :
				General.GetAbsoluteDirectoryPath(path, Settings.ExeDir);
		}
	}
}
