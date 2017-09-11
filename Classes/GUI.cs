using System;
using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace JDP {
	public struct DownloadProgressInfo {
		public long DownloadID { get; set; }
		public string URL { get; set; }
		public int TryNumber { get; set; }
		public long StartTicks { get; set; }
		public long? EndTicks { get; set; }
		public long? TotalSize { get; set; }
		public long DownloadedSize { get; set; }
	}

	public class DownloadedSizeSnapshot {
		public long Ticks { get; }
		public long DownloadedSize { get; }

		public DownloadedSizeSnapshot(long ticks, long downloadedSize) {
			Ticks = ticks;
			DownloadedSize = downloadedSize;
		}
	}

	public class ListItemInt32 {
		public int Value { get; }
		public string Text { get; }

		public ListItemInt32(int value, string text) {
			Value = value;
			Text = text;
		}
	}

	public class ListViewItemSorter : IComparer {
		public int Column { get; set; }
		public bool Ascending { get; set; }

		public ListViewItemSorter(int column) {
			Column = column;
			Ascending = true;
		}

		public int Compare(object x, object y) {
			int cmp = String.Compare(((ListViewItem)x).SubItems[Column].Text, ((ListViewItem)y).SubItems[Column].Text);
			return Ascending ? cmp : -cmp;
		}
	}

	public static class GUI {
		static GUI() {
			using (var form = new Form { AutoScaleMode = AutoScaleMode }) {
				AutoScaleFactorX = form.CurrentAutoScaleDimensions.Width / AutoScaleBaseSize.Width;
				AutoScaleFactorY = form.CurrentAutoScaleDimensions.Height / AutoScaleBaseSize.Height;
			}
		}

		public static AutoScaleMode AutoScaleMode { get; } = AutoScaleMode.Font;

		public static SizeF AutoScaleBaseSize { get; } = new SizeF(6F, 13F);

		public static float AutoScaleFactorX { get; }

		public static float AutoScaleFactorY { get; }

		public static int ScaleX(int size) => Convert.ToInt32(size * AutoScaleFactorX);

		public static int ScaleY(int size) => Convert.ToInt32(size * AutoScaleFactorY);

		public static void ScaleColumns(ListView control) {
			foreach (ColumnHeader column in control.Columns) {
				column.Width = ScaleX(column.Width);
			}
		}

		// Workaround for horizontal scroll bar not showing initially if no items have been added
		public static void EnsureScrollBarVisible(ListView control) {
			if (control.Items.Count != 0) return;
			control.Items.Add(new ListViewItem());
			control.Items.RemoveAt(0);
		}

		public static int GetHeaderHeight(ListView control) {
			bool addItem = control.Items.Count == 0;
			if (addItem) control.Items.Add(new ListViewItem());
			int headerHeight = control.GetItemRect(0).Y;
			if (addItem) control.Items.RemoveAt(0);
			return headerHeight;
		}

		public static void CenterChildForm(Form parent, Form child) {
			int centerX = ((parent.Left * 2) + parent.Width ) / 2;
			int centerY = ((parent.Top  * 2) + parent.Height) / 2;
			int formX   = ((parent.Left * 2) + parent.Width  - child.Width ) / 2;
			int formY   = ((parent.Top  * 2) + parent.Height - child.Height) / 2;

			Rectangle formRect = new Rectangle(formX, formY, child.Width, child.Height);
			Rectangle maxRect = Screen.GetWorkingArea(new Point(centerX, centerY));

			if (formRect.Right > maxRect.Right) {
				formRect.X -= formRect.Right - maxRect.Right;
			}
			if (formRect.Bottom > maxRect.Bottom) {
				formRect.Y -= formRect.Bottom - maxRect.Bottom;
			}
			if (formRect.X < maxRect.X) {
				formRect.X = maxRect.X;
			}
			if (formRect.Y < maxRect.Y) {
				formRect.Y = maxRect.Y;
			}

			child.Location = formRect.Location;
		}

		public static void EnableDoubleBuffering<T>(T control) where T : Control {
			typeof(T).InvokeMember(
				"DoubleBuffered",
				BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty,
				null,
				control,
				new object[] { true });
		}

		public static void SetFontAndScaling(Form form) {
			form.SuspendLayout();
			form.Font = new Font("Tahoma", 8.25F);
			if (form.Font.Name != "Tahoma") form.Font = new Font("Arial", 8.25F);
			form.AutoScaleMode = AutoScaleMode;
			form.AutoScaleDimensions = AutoScaleBaseSize;
			form.ResumeLayout(false);
		}
	}
}
