﻿namespace ChanThreadWatch {
	partial class frmSettings {
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing) {
			if (disposing && (components != null)) {
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent() {
			this.txtDownloadFolder = new System.Windows.Forms.TextBox();
			this.lblDownloadFolder = new System.Windows.Forms.Label();
			this.btnBrowse = new System.Windows.Forms.Button();
			this.btnOK = new System.Windows.Forms.Button();
			this.btnCancel = new System.Windows.Forms.Button();
			this.chkCustomUserAgent = new System.Windows.Forms.CheckBox();
			this.txtCustomUserAgent = new System.Windows.Forms.TextBox();
			this.chkRelativePath = new System.Windows.Forms.CheckBox();
			this.lblSettingsLocation = new System.Windows.Forms.Label();
			this.rbSettingsInAppDataFolder = new System.Windows.Forms.RadioButton();
			this.rbSettingsInExeFolder = new System.Windows.Forms.RadioButton();
			this.chkUseOriginalFilenames = new System.Windows.Forms.CheckBox();
			this.chkVerifyImageHashes = new System.Windows.Forms.CheckBox();
			this.chkCheckForUpdates = new System.Windows.Forms.CheckBox();
			this.SuspendLayout();
			// 
			// txtDownloadFolder
			// 
			this.txtDownloadFolder.Location = new System.Drawing.Point(108, 8);
			this.txtDownloadFolder.Name = "txtDownloadFolder";
			this.txtDownloadFolder.Size = new System.Drawing.Size(412, 21);
			this.txtDownloadFolder.TabIndex = 1;
			// 
			// lblDownloadFolder
			// 
			this.lblDownloadFolder.AutoSize = true;
			this.lblDownloadFolder.Location = new System.Drawing.Point(8, 12);
			this.lblDownloadFolder.Name = "lblDownloadFolder";
			this.lblDownloadFolder.Size = new System.Drawing.Size(89, 13);
			this.lblDownloadFolder.TabIndex = 0;
			this.lblDownloadFolder.Text = "Download folder:";
			// 
			// btnBrowse
			// 
			this.btnBrowse.Location = new System.Drawing.Point(528, 8);
			this.btnBrowse.Name = "btnBrowse";
			this.btnBrowse.Size = new System.Drawing.Size(80, 23);
			this.btnBrowse.TabIndex = 2;
			this.btnBrowse.Text = "Browse...";
			this.btnBrowse.UseVisualStyleBackColor = true;
			this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
			// 
			// btnOK
			// 
			this.btnOK.Location = new System.Drawing.Point(568, 144);
			this.btnOK.Name = "btnOK";
			this.btnOK.Size = new System.Drawing.Size(60, 23);
			this.btnOK.TabIndex = 12;
			this.btnOK.Text = "OK";
			this.btnOK.UseVisualStyleBackColor = true;
			this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
			// 
			// btnCancel
			// 
			this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.btnCancel.Location = new System.Drawing.Point(636, 144);
			this.btnCancel.Name = "btnCancel";
			this.btnCancel.Size = new System.Drawing.Size(68, 23);
			this.btnCancel.TabIndex = 13;
			this.btnCancel.Text = "Cancel";
			this.btnCancel.UseVisualStyleBackColor = true;
			// 
			// chkCustomUserAgent
			// 
			this.chkCustomUserAgent.AutoSize = true;
			this.chkCustomUserAgent.Location = new System.Drawing.Point(10, 42);
			this.chkCustomUserAgent.Name = "chkCustomUserAgent";
			this.chkCustomUserAgent.Size = new System.Drawing.Size(121, 17);
			this.chkCustomUserAgent.TabIndex = 4;
			this.chkCustomUserAgent.Text = "Custom user agent:";
			this.chkCustomUserAgent.UseVisualStyleBackColor = true;
			this.chkCustomUserAgent.CheckedChanged += new System.EventHandler(this.chkCustomUserAgent_CheckedChanged);
			// 
			// txtCustomUserAgent
			// 
			this.txtCustomUserAgent.Enabled = false;
			this.txtCustomUserAgent.Location = new System.Drawing.Point(140, 40);
			this.txtCustomUserAgent.Name = "txtCustomUserAgent";
			this.txtCustomUserAgent.Size = new System.Drawing.Size(564, 21);
			this.txtCustomUserAgent.TabIndex = 5;
			// 
			// chkRelativePath
			// 
			this.chkRelativePath.AutoSize = true;
			this.chkRelativePath.Location = new System.Drawing.Point(616, 12);
			this.chkRelativePath.Name = "chkRelativePath";
			this.chkRelativePath.Size = new System.Drawing.Size(90, 17);
			this.chkRelativePath.TabIndex = 3;
			this.chkRelativePath.Text = "Relative path";
			this.chkRelativePath.UseVisualStyleBackColor = true;
			this.chkRelativePath.CheckedChanged += new System.EventHandler(this.chkRelativePath_CheckedChanged);
			// 
			// lblSettingsLocation
			// 
			this.lblSettingsLocation.AutoSize = true;
			this.lblSettingsLocation.Location = new System.Drawing.Point(8, 152);
			this.lblSettingsLocation.Name = "lblSettingsLocation";
			this.lblSettingsLocation.Size = new System.Drawing.Size(87, 13);
			this.lblSettingsLocation.TabIndex = 9;
			this.lblSettingsLocation.Text = "Save settings in:";
			// 
			// rbSettingsInAppDataFolder
			// 
			this.rbSettingsInAppDataFolder.AutoSize = true;
			this.rbSettingsInAppDataFolder.Location = new System.Drawing.Point(108, 150);
			this.rbSettingsInAppDataFolder.Name = "rbSettingsInAppDataFolder";
			this.rbSettingsInAppDataFolder.Size = new System.Drawing.Size(134, 17);
			this.rbSettingsInAppDataFolder.TabIndex = 10;
			this.rbSettingsInAppDataFolder.TabStop = true;
			this.rbSettingsInAppDataFolder.Text = "Application Data folder";
			this.rbSettingsInAppDataFolder.UseVisualStyleBackColor = true;
			// 
			// rbSettingsInExeFolder
			// 
			this.rbSettingsInExeFolder.AutoSize = true;
			this.rbSettingsInExeFolder.Location = new System.Drawing.Point(252, 150);
			this.rbSettingsInExeFolder.Name = "rbSettingsInExeFolder";
			this.rbSettingsInExeFolder.Size = new System.Drawing.Size(109, 17);
			this.rbSettingsInExeFolder.TabIndex = 11;
			this.rbSettingsInExeFolder.TabStop = true;
			this.rbSettingsInExeFolder.Text = "Executable folder";
			this.rbSettingsInExeFolder.UseVisualStyleBackColor = true;
			// 
			// chkUseOriginalFilenames
			// 
			this.chkUseOriginalFilenames.AutoSize = true;
			this.chkUseOriginalFilenames.Location = new System.Drawing.Point(10, 96);
			this.chkUseOriginalFilenames.Name = "chkUseOriginalFilenames";
			this.chkUseOriginalFilenames.Size = new System.Drawing.Size(282, 17);
			this.chkUseOriginalFilenames.TabIndex = 7;
			this.chkUseOriginalFilenames.Text = "Use original filenames (only supported for some sites)";
			this.chkUseOriginalFilenames.UseVisualStyleBackColor = true;
			// 
			// chkVerifyImageHashes
			// 
			this.chkVerifyImageHashes.AutoSize = true;
			this.chkVerifyImageHashes.Location = new System.Drawing.Point(10, 120);
			this.chkVerifyImageHashes.Name = "chkVerifyImageHashes";
			this.chkVerifyImageHashes.Size = new System.Drawing.Size(275, 17);
			this.chkVerifyImageHashes.TabIndex = 8;
			this.chkVerifyImageHashes.Text = "Verify image hashes (only supported for some sites)";
			this.chkVerifyImageHashes.UseVisualStyleBackColor = true;
			// 
			// chkCheckForUpdates
			// 
			this.chkCheckForUpdates.AutoSize = true;
			this.chkCheckForUpdates.Location = new System.Drawing.Point(10, 72);
			this.chkCheckForUpdates.Name = "chkCheckForUpdates";
			this.chkCheckForUpdates.Size = new System.Drawing.Size(364, 17);
			this.chkCheckForUpdates.TabIndex = 6;
			this.chkCheckForUpdates.Text = "Automatically check for and notify of updated versions of this program";
			this.chkCheckForUpdates.UseVisualStyleBackColor = true;
			// 
			// frmSettings
			// 
			this.Name = "frmSettings";
			this.Text = "Settings";
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.ShowIcon = false;
			this.ShowInTaskbar = false;
			this.AcceptButton = this.btnOK;
			this.CancelButton = this.btnCancel;
			this.ClientSize = new System.Drawing.Size(714, 177);
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.Controls.Add(this.chkCheckForUpdates);
			this.Controls.Add(this.chkVerifyImageHashes);
			this.Controls.Add(this.chkUseOriginalFilenames);
			this.Controls.Add(this.rbSettingsInExeFolder);
			this.Controls.Add(this.rbSettingsInAppDataFolder);
			this.Controls.Add(this.lblSettingsLocation);
			this.Controls.Add(this.chkRelativePath);
			this.Controls.Add(this.txtCustomUserAgent);
			this.Controls.Add(this.chkCustomUserAgent);
			this.Controls.Add(this.btnCancel);
			this.Controls.Add(this.btnOK);
			this.Controls.Add(this.btnBrowse);
			this.Controls.Add(this.lblDownloadFolder);
			this.Controls.Add(this.txtDownloadFolder);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.Load += new System.EventHandler(this.frmSettings_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.TextBox txtDownloadFolder;
		private System.Windows.Forms.Label lblDownloadFolder;
		private System.Windows.Forms.Button btnBrowse;
		private System.Windows.Forms.Button btnOK;
		private System.Windows.Forms.Button btnCancel;
		private System.Windows.Forms.CheckBox chkCustomUserAgent;
		private System.Windows.Forms.TextBox txtCustomUserAgent;
		private System.Windows.Forms.CheckBox chkRelativePath;
		private System.Windows.Forms.Label lblSettingsLocation;
		private System.Windows.Forms.RadioButton rbSettingsInAppDataFolder;
		private System.Windows.Forms.RadioButton rbSettingsInExeFolder;
		private System.Windows.Forms.CheckBox chkUseOriginalFilenames;
		private System.Windows.Forms.CheckBox chkVerifyImageHashes;
		private System.Windows.Forms.CheckBox chkCheckForUpdates;
	}
}