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
			this.SuspendLayout();
			// 
			// txtDownloadFolder
			// 
			this.txtDownloadFolder.Location = new System.Drawing.Point(108, 8);
			this.txtDownloadFolder.Name = "txtDownloadFolder";
			this.txtDownloadFolder.Size = new System.Drawing.Size(432, 21);
			this.txtDownloadFolder.TabIndex = 1;
			// 
			// lblDownloadFolder
			// 
			this.lblDownloadFolder.AutoSize = true;
			this.lblDownloadFolder.Location = new System.Drawing.Point(8, 12);
			this.lblDownloadFolder.Name = "lblDownloadFolder";
			this.lblDownloadFolder.Size = new System.Drawing.Size(91, 13);
			this.lblDownloadFolder.TabIndex = 0;
			this.lblDownloadFolder.Text = "Download Folder:";
			// 
			// btnBrowse
			// 
			this.btnBrowse.Location = new System.Drawing.Point(548, 8);
			this.btnBrowse.Name = "btnBrowse";
			this.btnBrowse.Size = new System.Drawing.Size(80, 23);
			this.btnBrowse.TabIndex = 2;
			this.btnBrowse.Text = "Browse...";
			this.btnBrowse.UseVisualStyleBackColor = true;
			this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
			// 
			// btnOK
			// 
			this.btnOK.Location = new System.Drawing.Point(251, 72);
			this.btnOK.Name = "btnOK";
			this.btnOK.Size = new System.Drawing.Size(60, 23);
			this.btnOK.TabIndex = 5;
			this.btnOK.Text = "OK";
			this.btnOK.UseVisualStyleBackColor = true;
			this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
			// 
			// btnCancel
			// 
			this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.btnCancel.Location = new System.Drawing.Point(319, 72);
			this.btnCancel.Name = "btnCancel";
			this.btnCancel.Size = new System.Drawing.Size(68, 23);
			this.btnCancel.TabIndex = 6;
			this.btnCancel.Text = "Cancel";
			this.btnCancel.UseVisualStyleBackColor = true;
			// 
			// chkCustomUserAgent
			// 
			this.chkCustomUserAgent.AutoSize = true;
			this.chkCustomUserAgent.Location = new System.Drawing.Point(10, 42);
			this.chkCustomUserAgent.Name = "chkCustomUserAgent";
			this.chkCustomUserAgent.Size = new System.Drawing.Size(123, 17);
			this.chkCustomUserAgent.TabIndex = 3;
			this.chkCustomUserAgent.Text = "Custom User Agent:";
			this.chkCustomUserAgent.UseVisualStyleBackColor = true;
			this.chkCustomUserAgent.CheckedChanged += new System.EventHandler(this.chkCustomUserAgent_CheckedChanged);
			// 
			// txtCustomUserAgent
			// 
			this.txtCustomUserAgent.Enabled = false;
			this.txtCustomUserAgent.Location = new System.Drawing.Point(140, 40);
			this.txtCustomUserAgent.Name = "txtCustomUserAgent";
			this.txtCustomUserAgent.Size = new System.Drawing.Size(488, 21);
			this.txtCustomUserAgent.TabIndex = 4;
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
			this.ClientSize = new System.Drawing.Size(638, 105);
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
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
	}
}