namespace JDP {
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
			this.components = new System.ComponentModel.Container();
			this.lvDownloads = new System.Windows.Forms.ListView();
			this.chUrl = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.chSize = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.chPercent = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.chSpeed = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.chTry = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.tmrUpdateList = new System.Windows.Forms.Timer(this.components);
			this.SuspendLayout();
			// 
			// lvDownloads
			// 
			this.lvDownloads.AllowColumnReorder = true;
			this.lvDownloads.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.lvDownloads.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.chUrl,
            this.chSize,
            this.chPercent,
            this.chSpeed,
            this.chTry});
			this.lvDownloads.FullRowSelect = true;
			this.lvDownloads.HideSelection = false;
			this.lvDownloads.Location = new System.Drawing.Point(8, 8);
			this.lvDownloads.Name = "lvDownloads";
			this.lvDownloads.Size = new System.Drawing.Size(620, 196);
			this.lvDownloads.TabIndex = 0;
			this.lvDownloads.UseCompatibleStateImageBehavior = false;
			this.lvDownloads.View = System.Windows.Forms.View.Details;
			// 
			// chUrl
			// 
			this.chUrl.Text = "URL";
			this.chUrl.Width = 355;
			// 
			// chSize
			// 
			this.chSize.Text = "Size";
			this.chSize.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
			this.chSize.Width = 70;
			// 
			// chPercent
			// 
			this.chPercent.Text = "Progress";
			this.chPercent.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
			// 
			// chSpeed
			// 
			this.chSpeed.Text = "Speed";
			this.chSpeed.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
			this.chSpeed.Width = 80;
			// 
			// chTry
			// 
			this.chTry.Text = "Try";
			this.chTry.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
			this.chTry.Width = 35;
			// 
			// tmrUpdateList
			// 
			this.tmrUpdateList.Enabled = true;
			this.tmrUpdateList.Tick += new System.EventHandler(this.tmrUpdateList_Tick);
			// 
			// frmDownloads
			// 
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.ClientSize = new System.Drawing.Size(636, 213);
			this.Controls.Add(this.lvDownloads);
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.MinimumSize = new System.Drawing.Size(260, 140);
			this.Name = "frmDownloads";
			this.ShowIcon = false;
			this.ShowInTaskbar = false;
			this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
			this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
			this.Text = "Downloads";
			this.Shown += new System.EventHandler(this.frmDownloads_Shown);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.ListView lvDownloads;
		private System.Windows.Forms.ColumnHeader chUrl;
		private System.Windows.Forms.ColumnHeader chPercent;
		private System.Windows.Forms.ColumnHeader chSize;
		private System.Windows.Forms.ColumnHeader chSpeed;
		private System.Windows.Forms.ColumnHeader chTry;
		private System.Windows.Forms.Timer tmrUpdateList;
	}
}