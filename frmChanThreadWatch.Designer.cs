﻿namespace JDP {
	partial class frmChanThreadWatch {
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
			this.components = new System.ComponentModel.Container();
			this.lvThreads = new System.Windows.Forms.ListView();
			this.chDescription = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.chStatus = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.chLastImageOn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.chAddedOn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.grpAddThread = new System.Windows.Forms.GroupBox();
			this.lblCheckEvery = new System.Windows.Forms.Label();
			this.cboCheckEvery = new System.Windows.Forms.ComboBox();
			this.txtImageAuth = new System.Windows.Forms.TextBox();
			this.txtPageAuth = new System.Windows.Forms.TextBox();
			this.chkImageAuth = new System.Windows.Forms.CheckBox();
			this.chkPageAuth = new System.Windows.Forms.CheckBox();
			this.chkOneTime = new System.Windows.Forms.CheckBox();
			this.lblURL = new System.Windows.Forms.Label();
			this.txtPageURL = new System.Windows.Forms.TextBox();
			this.btnAdd = new System.Windows.Forms.Button();
			this.btnRemoveCompleted = new System.Windows.Forms.Button();
			this.btnAbout = new System.Windows.Forms.Button();
			this.btnSettings = new System.Windows.Forms.Button();
			this.cmThreads = new System.Windows.Forms.ContextMenu();
			this.miEditDescription = new System.Windows.Forms.MenuItem();
			this.miOpenFolder = new System.Windows.Forms.MenuItem();
			this.miOpenURL = new System.Windows.Forms.MenuItem();
			this.miStop = new System.Windows.Forms.MenuItem();
			this.miStart = new System.Windows.Forms.MenuItem();
			this.miCopyURL = new System.Windows.Forms.MenuItem();
			this.miRemove = new System.Windows.Forms.MenuItem();
			this.miRemoveAndDeleteFolder = new System.Windows.Forms.MenuItem();
			this.miCheckNow = new System.Windows.Forms.MenuItem();
			this.miCheckEvery = new System.Windows.Forms.MenuItem();
			this.miPostprocessFiles = new System.Windows.Forms.MenuItem();
			this.grpDoubleClick = new System.Windows.Forms.GroupBox();
			this.rbEditDescription = new System.Windows.Forms.RadioButton();
			this.rbOpenURL = new System.Windows.Forms.RadioButton();
			this.rbOpenFolder = new System.Windows.Forms.RadioButton();
			this.tmrUpdateWaitStatus = new System.Windows.Forms.Timer(this.components);
			this.btnAddFromClipboard = new System.Windows.Forms.Button();
			this.tmrSaveThreadList = new System.Windows.Forms.Timer(this.components);
			this.btnDownloads = new System.Windows.Forms.Button();
			this.tmrMaintenance = new System.Windows.Forms.Timer(this.components);
			this.grpAddThread.SuspendLayout();
			this.grpDoubleClick.SuspendLayout();
			this.SuspendLayout();
			// 
			// lvThreads
			// 
			this.lvThreads.AllowColumnReorder = true;
			this.lvThreads.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.lvThreads.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.chDescription,
            this.chStatus,
            this.chLastImageOn,
            this.chAddedOn});
			this.lvThreads.FullRowSelect = true;
			this.lvThreads.HideSelection = false;
			this.lvThreads.Location = new System.Drawing.Point(8, 8);
			this.lvThreads.Name = "lvThreads";
			this.lvThreads.Size = new System.Drawing.Size(620, 196);
			this.lvThreads.TabIndex = 0;
			this.lvThreads.UseCompatibleStateImageBehavior = false;
			this.lvThreads.View = System.Windows.Forms.View.Details;
			this.lvThreads.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.lvThreads_ColumnClick);
			this.lvThreads.KeyDown += new System.Windows.Forms.KeyEventHandler(this.lvThreads_KeyDown);
			this.lvThreads.MouseClick += new System.Windows.Forms.MouseEventHandler(this.lvThreads_MouseClick);
			this.lvThreads.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.lvThreads_MouseDoubleClick);
			// 
			// chDescription
			// 
			this.chDescription.Text = "Description";
			this.chDescription.Width = 220;
			// 
			// chStatus
			// 
			this.chStatus.Text = "Status";
			this.chStatus.Width = 250;
			// 
			// chLastImageOn
			// 
			this.chLastImageOn.Text = "Last Image On";
			this.chLastImageOn.Width = 130;
			// 
			// chAddedOn
			// 
			this.chAddedOn.Text = "Added On";
			this.chAddedOn.Width = 130;
			// 
			// grpAddThread
			// 
			this.grpAddThread.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.grpAddThread.Controls.Add(this.lblCheckEvery);
			this.grpAddThread.Controls.Add(this.cboCheckEvery);
			this.grpAddThread.Controls.Add(this.txtImageAuth);
			this.grpAddThread.Controls.Add(this.txtPageAuth);
			this.grpAddThread.Controls.Add(this.chkImageAuth);
			this.grpAddThread.Controls.Add(this.chkPageAuth);
			this.grpAddThread.Controls.Add(this.chkOneTime);
			this.grpAddThread.Controls.Add(this.lblURL);
			this.grpAddThread.Controls.Add(this.txtPageURL);
			this.grpAddThread.Controls.Add(this.btnAdd);
			this.grpAddThread.Location = new System.Drawing.Point(8, 208);
			this.grpAddThread.Name = "grpAddThread";
			this.grpAddThread.Size = new System.Drawing.Size(360, 157);
			this.grpAddThread.TabIndex = 1;
			this.grpAddThread.TabStop = false;
			this.grpAddThread.Text = "Add Thread";
			// 
			// lblCheckEvery
			// 
			this.lblCheckEvery.AutoSize = true;
			this.lblCheckEvery.Location = new System.Drawing.Point(10, 128);
			this.lblCheckEvery.Name = "lblCheckEvery";
			this.lblCheckEvery.Size = new System.Drawing.Size(115, 13);
			this.lblCheckEvery.TabIndex = 7;
			this.lblCheckEvery.Text = "Check every (minutes):";
			// 
			// cboCheckEvery
			// 
			this.cboCheckEvery.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.cboCheckEvery.FormattingEnabled = true;
			this.cboCheckEvery.Location = new System.Drawing.Point(140, 124);
			this.cboCheckEvery.Name = "cboCheckEvery";
			this.cboCheckEvery.Size = new System.Drawing.Size(64, 21);
			this.cboCheckEvery.TabIndex = 8;
			// 
			// txtImageAuth
			// 
			this.txtImageAuth.Enabled = false;
			this.txtImageAuth.Location = new System.Drawing.Point(164, 72);
			this.txtImageAuth.Name = "txtImageAuth";
			this.txtImageAuth.Size = new System.Drawing.Size(184, 20);
			this.txtImageAuth.TabIndex = 5;
			// 
			// txtPageAuth
			// 
			this.txtPageAuth.Enabled = false;
			this.txtPageAuth.Location = new System.Drawing.Point(164, 44);
			this.txtPageAuth.Name = "txtPageAuth";
			this.txtPageAuth.Size = new System.Drawing.Size(184, 20);
			this.txtPageAuth.TabIndex = 3;
			// 
			// chkImageAuth
			// 
			this.chkImageAuth.AutoSize = true;
			this.chkImageAuth.Location = new System.Drawing.Point(12, 74);
			this.chkImageAuth.Name = "chkImageAuth";
			this.chkImageAuth.Size = new System.Drawing.Size(136, 17);
			this.chkImageAuth.TabIndex = 4;
			this.chkImageAuth.Text = "Image auth (user:pass):";
			this.chkImageAuth.UseVisualStyleBackColor = true;
			this.chkImageAuth.CheckedChanged += new System.EventHandler(this.chkImageAuth_CheckedChanged);
			// 
			// chkPageAuth
			// 
			this.chkPageAuth.AutoSize = true;
			this.chkPageAuth.Location = new System.Drawing.Point(12, 46);
			this.chkPageAuth.Name = "chkPageAuth";
			this.chkPageAuth.Size = new System.Drawing.Size(132, 17);
			this.chkPageAuth.TabIndex = 2;
			this.chkPageAuth.Text = "Page auth (user:pass):";
			this.chkPageAuth.UseVisualStyleBackColor = true;
			this.chkPageAuth.CheckedChanged += new System.EventHandler(this.chkPageAuth_CheckedChanged);
			// 
			// chkOneTime
			// 
			this.chkOneTime.AutoSize = true;
			this.chkOneTime.Location = new System.Drawing.Point(12, 100);
			this.chkOneTime.Name = "chkOneTime";
			this.chkOneTime.Size = new System.Drawing.Size(181, 17);
			this.chkOneTime.TabIndex = 6;
			this.chkOneTime.Text = "Don\'t watch (one-time download)";
			this.chkOneTime.UseVisualStyleBackColor = true;
			this.chkOneTime.CheckedChanged += new System.EventHandler(this.chkOneTime_CheckedChanged);
			// 
			// lblURL
			// 
			this.lblURL.AutoSize = true;
			this.lblURL.Location = new System.Drawing.Point(10, 22);
			this.lblURL.Name = "lblURL";
			this.lblURL.Size = new System.Drawing.Size(32, 13);
			this.lblURL.TabIndex = 0;
			this.lblURL.Text = "URL:";
			// 
			// txtPageURL
			// 
			this.txtPageURL.Location = new System.Drawing.Point(48, 18);
			this.txtPageURL.Name = "txtPageURL";
			this.txtPageURL.Size = new System.Drawing.Size(300, 20);
			this.txtPageURL.TabIndex = 1;
			this.txtPageURL.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtPageURL_KeyDown);
			// 
			// btnAdd
			// 
			this.btnAdd.Location = new System.Drawing.Point(264, 122);
			this.btnAdd.Name = "btnAdd";
			this.btnAdd.Size = new System.Drawing.Size(84, 23);
			this.btnAdd.TabIndex = 9;
			this.btnAdd.Text = "Add Thread";
			this.btnAdd.UseVisualStyleBackColor = true;
			this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
			// 
			// btnRemoveCompleted
			// 
			this.btnRemoveCompleted.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnRemoveCompleted.Location = new System.Drawing.Point(508, 212);
			this.btnRemoveCompleted.Name = "btnRemoveCompleted";
			this.btnRemoveCompleted.Size = new System.Drawing.Size(120, 23);
			this.btnRemoveCompleted.TabIndex = 3;
			this.btnRemoveCompleted.Text = "Remove Completed";
			this.btnRemoveCompleted.UseVisualStyleBackColor = true;
			this.btnRemoveCompleted.Click += new System.EventHandler(this.btnRemoveCompleted_Click);
			// 
			// btnAbout
			// 
			this.btnAbout.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnAbout.Location = new System.Drawing.Point(568, 342);
			this.btnAbout.Name = "btnAbout";
			this.btnAbout.Size = new System.Drawing.Size(60, 23);
			this.btnAbout.TabIndex = 7;
			this.btnAbout.Text = "About";
			this.btnAbout.UseVisualStyleBackColor = true;
			this.btnAbout.Click += new System.EventHandler(this.btnAbout_Click);
			// 
			// btnSettings
			// 
			this.btnSettings.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnSettings.Location = new System.Drawing.Point(492, 342);
			this.btnSettings.Name = "btnSettings";
			this.btnSettings.Size = new System.Drawing.Size(67, 23);
			this.btnSettings.TabIndex = 6;
			this.btnSettings.Text = "Settings";
			this.btnSettings.UseVisualStyleBackColor = true;
			this.btnSettings.Click += new System.EventHandler(this.btnSettings_Click);
			// 
			// cmThreads
			// 
			this.cmThreads.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.miEditDescription,
            this.miOpenFolder,
            this.miOpenURL,
            this.miStop,
            this.miStart,
            this.miCopyURL,
            this.miRemove,
            this.miRemoveAndDeleteFolder,
            this.miCheckNow,
            this.miCheckEvery,
            this.miPostprocessFiles});
			// 
			// miEditDescription
			// 
			this.miEditDescription.Index = 0;
			this.miEditDescription.Text = "Edit Description";
			this.miEditDescription.Click += new System.EventHandler(this.miEditDescription_Click);
			// 
			// miOpenFolder
			// 
			this.miOpenFolder.Index = 1;
			this.miOpenFolder.Text = "Open Folder";
			this.miOpenFolder.Click += new System.EventHandler(this.miOpenFolder_Click);
			// 
			// miOpenURL
			// 
			this.miOpenURL.Index = 2;
			this.miOpenURL.Text = "Open URL";
			this.miOpenURL.Click += new System.EventHandler(this.miOpenURL_Click);
			// 
			// miStop
			// 
			this.miStop.Index = 3;
			this.miStop.Text = "Stop";
			this.miStop.Click += new System.EventHandler(this.miStop_Click);
			// 
			// miStart
			// 
			this.miStart.Index = 4;
			this.miStart.Text = "Start";
			this.miStart.Click += new System.EventHandler(this.miStart_Click);
			// 
			// miCopyURL
			// 
			this.miCopyURL.Index = 5;
			this.miCopyURL.Text = "Copy URL";
			this.miCopyURL.Click += new System.EventHandler(this.miCopyURL_Click);
			// 
			// miRemove
			// 
			this.miRemove.Index = 6;
			this.miRemove.Text = "Remove";
			this.miRemove.Click += new System.EventHandler(this.miRemove_Click);
			// 
			// miRemoveAndDeleteFolder
			// 
			this.miRemoveAndDeleteFolder.Index = 7;
			this.miRemoveAndDeleteFolder.Text = "Remove and Delete Folder";
			this.miRemoveAndDeleteFolder.Click += new System.EventHandler(this.miRemoveAndDeleteFolder_Click);
			// 
			// miCheckNow
			// 
			this.miCheckNow.Index = 8;
			this.miCheckNow.Text = "Check Now";
			this.miCheckNow.Click += new System.EventHandler(this.miCheckNow_Click);
			// 
			// miCheckEvery
			// 
			this.miCheckEvery.Index = 9;
			this.miCheckEvery.Text = "Check Every";
			// 
			// miPostprocessFiles
			// 
			this.miPostprocessFiles.Index = 10;
			this.miPostprocessFiles.Text = "Postprocess Files";
			this.miPostprocessFiles.Click += new System.EventHandler(this.miPostprocess_Click);
			// 
			// grpDoubleClick
			// 
			this.grpDoubleClick.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.grpDoubleClick.Controls.Add(this.rbEditDescription);
			this.grpDoubleClick.Controls.Add(this.rbOpenURL);
			this.grpDoubleClick.Controls.Add(this.rbOpenFolder);
			this.grpDoubleClick.Location = new System.Drawing.Point(376, 208);
			this.grpDoubleClick.Name = "grpDoubleClick";
			this.grpDoubleClick.Size = new System.Drawing.Size(124, 84);
			this.grpDoubleClick.TabIndex = 2;
			this.grpDoubleClick.TabStop = false;
			this.grpDoubleClick.Text = "On Double Click";
			// 
			// rbEditDescription
			// 
			this.rbEditDescription.Location = new System.Drawing.Point(12, 58);
			this.rbEditDescription.Name = "rbEditDescription";
			this.rbEditDescription.Size = new System.Drawing.Size(100, 17);
			this.rbEditDescription.TabIndex = 2;
			this.rbEditDescription.TabStop = true;
			this.rbEditDescription.Text = "Edit Description";
			this.rbEditDescription.UseVisualStyleBackColor = true;
			// 
			// rbOpenURL
			// 
			this.rbOpenURL.Location = new System.Drawing.Point(12, 38);
			this.rbOpenURL.Name = "rbOpenURL";
			this.rbOpenURL.Size = new System.Drawing.Size(100, 17);
			this.rbOpenURL.TabIndex = 1;
			this.rbOpenURL.TabStop = true;
			this.rbOpenURL.Text = "Open URL";
			this.rbOpenURL.UseVisualStyleBackColor = true;
			// 
			// rbOpenFolder
			// 
			this.rbOpenFolder.Location = new System.Drawing.Point(12, 18);
			this.rbOpenFolder.Name = "rbOpenFolder";
			this.rbOpenFolder.Size = new System.Drawing.Size(100, 17);
			this.rbOpenFolder.TabIndex = 0;
			this.rbOpenFolder.TabStop = true;
			this.rbOpenFolder.Text = "Open Folder";
			this.rbOpenFolder.UseVisualStyleBackColor = true;
			// 
			// tmrUpdateWaitStatus
			// 
			this.tmrUpdateWaitStatus.Interval = 500;
			this.tmrUpdateWaitStatus.Tick += new System.EventHandler(this.tmrUpdateWaitStatus_Tick);
			// 
			// btnAddFromClipboard
			// 
			this.btnAddFromClipboard.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnAddFromClipboard.Location = new System.Drawing.Point(508, 244);
			this.btnAddFromClipboard.Name = "btnAddFromClipboard";
			this.btnAddFromClipboard.Size = new System.Drawing.Size(120, 23);
			this.btnAddFromClipboard.TabIndex = 4;
			this.btnAddFromClipboard.Text = "Add From Clipboard";
			this.btnAddFromClipboard.UseVisualStyleBackColor = true;
			this.btnAddFromClipboard.Click += new System.EventHandler(this.btnAddFromClipboard_Click);
			// 
			// tmrSaveThreadList
			// 
			this.tmrSaveThreadList.Enabled = true;
			this.tmrSaveThreadList.Interval = 60000;
			this.tmrSaveThreadList.Tick += new System.EventHandler(this.tmrSaveThreadList_Tick);
			// 
			// btnDownloads
			// 
			this.btnDownloads.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnDownloads.Location = new System.Drawing.Point(400, 342);
			this.btnDownloads.Name = "btnDownloads";
			this.btnDownloads.Size = new System.Drawing.Size(84, 23);
			this.btnDownloads.TabIndex = 5;
			this.btnDownloads.Text = "Downloads";
			this.btnDownloads.UseVisualStyleBackColor = true;
			this.btnDownloads.Click += new System.EventHandler(this.btnDownloads_Click);
			// 
			// tmrMaintenance
			// 
			this.tmrMaintenance.Enabled = true;
			this.tmrMaintenance.Interval = 1000;
			this.tmrMaintenance.Tick += new System.EventHandler(this.tmrMaintenance_Tick);
			// 
			// frmChanThreadWatch
			// 
			this.AllowDrop = true;
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.ClientSize = new System.Drawing.Size(636, 373);
			this.Controls.Add(this.btnDownloads);
			this.Controls.Add(this.btnAddFromClipboard);
			this.Controls.Add(this.grpDoubleClick);
			this.Controls.Add(this.btnSettings);
			this.Controls.Add(this.btnAbout);
			this.Controls.Add(this.btnRemoveCompleted);
			this.Controls.Add(this.grpAddThread);
			this.Controls.Add(this.lvThreads);
			this.MinimumSize = new System.Drawing.Size(644, 300);
			this.Name = "frmChanThreadWatch";
			this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "Chan Thread Watch";
			this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.frmChanThreadWatch_FormClosed);
			this.Shown += new System.EventHandler(this.frmChanThreadWatch_Shown);
			this.DragDrop += new System.Windows.Forms.DragEventHandler(this.frmChanThreadWatch_DragDrop);
			this.DragEnter += new System.Windows.Forms.DragEventHandler(this.frmChanThreadWatch_DragEnter);
			this.grpAddThread.ResumeLayout(false);
			this.grpAddThread.PerformLayout();
			this.grpDoubleClick.ResumeLayout(false);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.ListView lvThreads;
		private System.Windows.Forms.ColumnHeader chStatus;
		private System.Windows.Forms.GroupBox grpAddThread;
		private System.Windows.Forms.CheckBox chkOneTime;
		private System.Windows.Forms.Label lblURL;
		private System.Windows.Forms.TextBox txtPageURL;
		private System.Windows.Forms.Button btnAdd;
		private System.Windows.Forms.Button btnRemoveCompleted;
		private System.Windows.Forms.Button btnAbout;
		private System.Windows.Forms.TextBox txtPageAuth;
		private System.Windows.Forms.CheckBox chkImageAuth;
		private System.Windows.Forms.CheckBox chkPageAuth;
		private System.Windows.Forms.ComboBox cboCheckEvery;
		private System.Windows.Forms.TextBox txtImageAuth;
		private System.Windows.Forms.Label lblCheckEvery;
		private System.Windows.Forms.Button btnSettings;
		private System.Windows.Forms.ContextMenu cmThreads;
		private System.Windows.Forms.MenuItem miOpenFolder;
		private System.Windows.Forms.MenuItem miOpenURL;
		private System.Windows.Forms.MenuItem miCheckNow;
		private System.Windows.Forms.MenuItem miStop;
		private System.Windows.Forms.MenuItem miCopyURL;
		private System.Windows.Forms.MenuItem miCheckEvery;
		private System.Windows.Forms.MenuItem miPostprocessFiles;
		private System.Windows.Forms.GroupBox grpDoubleClick;
		private System.Windows.Forms.RadioButton rbOpenURL;
		private System.Windows.Forms.RadioButton rbOpenFolder;
		private System.Windows.Forms.MenuItem miStart;
		private System.Windows.Forms.Timer tmrUpdateWaitStatus;
		private System.Windows.Forms.Button btnAddFromClipboard;
		private System.Windows.Forms.MenuItem miRemove;
		private System.Windows.Forms.MenuItem miRemoveAndDeleteFolder;
		private System.Windows.Forms.ColumnHeader chAddedOn;
		private System.Windows.Forms.ColumnHeader chLastImageOn;
		private System.Windows.Forms.ColumnHeader chDescription;
		private System.Windows.Forms.MenuItem miEditDescription;
		private System.Windows.Forms.RadioButton rbEditDescription;
		private System.Windows.Forms.Timer tmrSaveThreadList;
		private System.Windows.Forms.Button btnDownloads;
		private System.Windows.Forms.Timer tmrMaintenance;
	}
}

