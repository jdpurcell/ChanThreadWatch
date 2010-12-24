namespace ChanThreadWatch {
	partial class frmDownloads {
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
			this.lvDownloads = new System.Windows.Forms.ListView();
			this.chURL = new System.Windows.Forms.ColumnHeader();
			this.chPercent = new System.Windows.Forms.ColumnHeader();
			this.chKilobytes = new System.Windows.Forms.ColumnHeader();
			this.SuspendLayout();
			// 
			// lvDownloads
			// 
			this.lvDownloads.AllowColumnReorder = true;
			this.lvDownloads.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
						| System.Windows.Forms.AnchorStyles.Left)
						| System.Windows.Forms.AnchorStyles.Right)));
			this.lvDownloads.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.chURL,
            this.chPercent,
            this.chKilobytes});
			this.lvDownloads.FullRowSelect = true;
			this.lvDownloads.HideSelection = false;
			this.lvDownloads.Location = new System.Drawing.Point(8, 8);
			this.lvDownloads.Name = "lvDownloads";
			this.lvDownloads.Size = new System.Drawing.Size(510, 196);
			this.lvDownloads.TabIndex = 0;
			this.lvDownloads.UseCompatibleStateImageBehavior = false;
			this.lvDownloads.View = System.Windows.Forms.View.Details;
			// 
			// chURL
			// 
			this.chURL.Text = "URL";
			this.chURL.Width = 360;
			// 
			// chPercent
			// 
			this.chPercent.Text = "Percent";
			this.chPercent.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
			// 
			// chKilobytes
			// 
			this.chKilobytes.Text = "Kilobytes";
			this.chKilobytes.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
			this.chKilobytes.Width = 70;
			// 
			// frmDownloads
			// 
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.ClientSize = new System.Drawing.Size(526, 213);
			this.Controls.Add(this.lvDownloads);
			this.MinimizeBox = false;
			this.MinimumSize = new System.Drawing.Size(260, 140);
			this.Name = "frmDownloads";
			this.ShowIcon = false;
			this.ShowInTaskbar = false;
			this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Downloads";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.frmDownloads_FormClosing);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.ListView lvDownloads;
		private System.Windows.Forms.ColumnHeader chURL;
		private System.Windows.Forms.ColumnHeader chPercent;
		private System.Windows.Forms.ColumnHeader chKilobytes;
	}
}