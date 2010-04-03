namespace ChanThreadWatch {
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
			this.lvThreads = new System.Windows.Forms.ListView();
			this.chURL = new System.Windows.Forms.ColumnHeader();
			this.chStatus = new System.Windows.Forms.ColumnHeader();
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
			this.btnStopSelected = new System.Windows.Forms.Button();
			this.btnRemoveCompleted = new System.Windows.Forms.Button();
			this.btnAbout = new System.Windows.Forms.Button();
			this.btnOpenSelectedFolder = new System.Windows.Forms.Button();
			this.grpAddThread.SuspendLayout();
			this.SuspendLayout();
			// 
			// lvThreads
			// 
			this.lvThreads.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.chURL,
            this.chStatus});
			this.lvThreads.FullRowSelect = true;
			this.lvThreads.HideSelection = false;
			this.lvThreads.Location = new System.Drawing.Point(8, 8);
			this.lvThreads.Name = "lvThreads";
			this.lvThreads.Size = new System.Drawing.Size(620, 164);
			this.lvThreads.TabIndex = 0;
			this.lvThreads.UseCompatibleStateImageBehavior = false;
			this.lvThreads.View = System.Windows.Forms.View.Details;
			// 
			// chURL
			// 
			this.chURL.Text = "URL";
			this.chURL.Width = 300;
			// 
			// chStatus
			// 
			this.chStatus.Text = "Status";
			this.chStatus.Width = 300;
			// 
			// grpAddThread
			// 
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
			this.grpAddThread.Location = new System.Drawing.Point(8, 176);
			this.grpAddThread.Name = "grpAddThread";
			this.grpAddThread.Size = new System.Drawing.Size(360, 157);
			this.grpAddThread.TabIndex = 1;
			this.grpAddThread.TabStop = false;
			this.grpAddThread.Text = "Add Thread";
			// 
			// lblCheckEvery
			// 
			this.lblCheckEvery.AutoSize = true;
			this.lblCheckEvery.Location = new System.Drawing.Point(12, 128);
			this.lblCheckEvery.Name = "lblCheckEvery";
			this.lblCheckEvery.Size = new System.Drawing.Size(119, 13);
			this.lblCheckEvery.TabIndex = 7;
			this.lblCheckEvery.Text = "Check every (minutes):";
			// 
			// cboCheckEvery
			// 
			this.cboCheckEvery.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.cboCheckEvery.FormattingEnabled = true;
			this.cboCheckEvery.Items.AddRange(new object[] {
            "1",
            "2",
            "3",
            "5",
            "10"});
			this.cboCheckEvery.Location = new System.Drawing.Point(140, 124);
			this.cboCheckEvery.Name = "cboCheckEvery";
			this.cboCheckEvery.Size = new System.Drawing.Size(52, 21);
			this.cboCheckEvery.TabIndex = 8;
			// 
			// txtImageAuth
			// 
			this.txtImageAuth.Enabled = false;
			this.txtImageAuth.Location = new System.Drawing.Point(164, 72);
			this.txtImageAuth.Name = "txtImageAuth";
			this.txtImageAuth.Size = new System.Drawing.Size(184, 21);
			this.txtImageAuth.TabIndex = 5;
			// 
			// txtPageAuth
			// 
			this.txtPageAuth.Enabled = false;
			this.txtPageAuth.Location = new System.Drawing.Point(164, 44);
			this.txtPageAuth.Name = "txtPageAuth";
			this.txtPageAuth.Size = new System.Drawing.Size(184, 21);
			this.txtPageAuth.TabIndex = 3;
			// 
			// chkImageAuth
			// 
			this.chkImageAuth.AutoSize = true;
			this.chkImageAuth.Location = new System.Drawing.Point(12, 74);
			this.chkImageAuth.Name = "chkImageAuth";
			this.chkImageAuth.Size = new System.Drawing.Size(143, 17);
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
			this.chkPageAuth.Size = new System.Drawing.Size(137, 17);
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
			this.chkOneTime.Size = new System.Drawing.Size(185, 17);
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
			this.lblURL.Size = new System.Drawing.Size(30, 13);
			this.lblURL.TabIndex = 0;
			this.lblURL.Text = "URL:";
			// 
			// txtPageURL
			// 
			this.txtPageURL.Location = new System.Drawing.Point(48, 18);
			this.txtPageURL.Name = "txtPageURL";
			this.txtPageURL.Size = new System.Drawing.Size(300, 21);
			this.txtPageURL.TabIndex = 1;
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
			// btnStopSelected
			// 
			this.btnStopSelected.Location = new System.Drawing.Point(536, 180);
			this.btnStopSelected.Name = "btnStopSelected";
			this.btnStopSelected.Size = new System.Drawing.Size(92, 23);
			this.btnStopSelected.TabIndex = 2;
			this.btnStopSelected.Text = "Stop Selected";
			this.btnStopSelected.UseVisualStyleBackColor = true;
			this.btnStopSelected.Click += new System.EventHandler(this.btnStopSelected_Click);
			// 
			// btnRemoveCompleted
			// 
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
			this.btnAbout.Location = new System.Drawing.Point(568, 310);
			this.btnAbout.Name = "btnAbout";
			this.btnAbout.Size = new System.Drawing.Size(60, 23);
			this.btnAbout.TabIndex = 5;
			this.btnAbout.Text = "About";
			this.btnAbout.UseVisualStyleBackColor = true;
			this.btnAbout.Click += new System.EventHandler(this.btnAbout_Click);
			// 
			// btnOpenSelectedFolder
			// 
			this.btnOpenSelectedFolder.Location = new System.Drawing.Point(496, 244);
			this.btnOpenSelectedFolder.Name = "btnOpenSelectedFolder";
			this.btnOpenSelectedFolder.Size = new System.Drawing.Size(132, 23);
			this.btnOpenSelectedFolder.TabIndex = 4;
			this.btnOpenSelectedFolder.Text = "Open Selected Folder";
			this.btnOpenSelectedFolder.UseVisualStyleBackColor = true;
			this.btnOpenSelectedFolder.Click += new System.EventHandler(this.btnOpenSelectedFolder_Click);
			// 
			// frmChanThreadWatch
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(636, 341);
			this.Controls.Add(this.btnOpenSelectedFolder);
			this.Controls.Add(this.btnAbout);
			this.Controls.Add(this.btnRemoveCompleted);
			this.Controls.Add(this.btnStopSelected);
			this.Controls.Add(this.grpAddThread);
			this.Controls.Add(this.lvThreads);
			this.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
			this.MaximizeBox = false;
			this.Name = "frmChanThreadWatch";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "Chan Thread Watch";
			this.Load += new System.EventHandler(this.frmChanThreadWatch_Load);
			this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.frmChanThreadWatch_FormClosed);
			this.grpAddThread.ResumeLayout(false);
			this.grpAddThread.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.ListView lvThreads;
		private System.Windows.Forms.ColumnHeader chURL;
		private System.Windows.Forms.ColumnHeader chStatus;
		private System.Windows.Forms.GroupBox grpAddThread;
		private System.Windows.Forms.CheckBox chkOneTime;
		private System.Windows.Forms.Label lblURL;
		private System.Windows.Forms.TextBox txtPageURL;
		private System.Windows.Forms.Button btnAdd;
		private System.Windows.Forms.Button btnStopSelected;
		private System.Windows.Forms.Button btnRemoveCompleted;
		private System.Windows.Forms.Button btnAbout;
		private System.Windows.Forms.TextBox txtPageAuth;
		private System.Windows.Forms.CheckBox chkImageAuth;
		private System.Windows.Forms.CheckBox chkPageAuth;
		private System.Windows.Forms.ComboBox cboCheckEvery;
		private System.Windows.Forms.TextBox txtImageAuth;
		private System.Windows.Forms.Label lblCheckEvery;
		private System.Windows.Forms.Button btnOpenSelectedFolder;
	}
}

