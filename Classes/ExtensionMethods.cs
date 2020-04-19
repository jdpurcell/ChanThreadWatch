using System;
using System.Text;
using System.Windows.Forms;

namespace JDP {
	public static class ExtensionMethods {
		private static string Substring(string str, string delim, bool lastDelim, bool afterDelim, StringComparison comparisonType, string defaultValue) {
			int pos = lastDelim ? str.LastIndexOf(delim, comparisonType) : str.IndexOf(delim, comparisonType);
			if (pos == -1) return defaultValue;
			return afterDelim ? str.Substring(pos + delim.Length) : str.Substring(0, pos);
		}

		public static string SubstringBeforeFirst(this string str, string delim, StringComparison comparisonType = StringComparison.CurrentCulture, string defaultValue = "") {
			return Substring(str, delim, false, false, comparisonType, defaultValue);
		}

		public static string SubstringBeforeLast(this string str, string delim, StringComparison comparisonType = StringComparison.CurrentCulture, string defaultValue = "") {
			return Substring(str, delim, true, false, comparisonType, defaultValue);
		}

		public static string SubstringAfterFirst(this string str, string delim, StringComparison comparisonType = StringComparison.CurrentCulture, string defaultValue = "") {
			return Substring(str, delim, false, true, comparisonType, defaultValue);
		}

		public static string SubstringAfterLast(this string str, string delim, StringComparison comparisonType = StringComparison.CurrentCulture, string defaultValue = "") {
			return Substring(str, delim, true, true, comparisonType, defaultValue);
		}

		public static int? TryParseInt32(this string str) {
			return Int32.TryParse(str, out int n) ? n : (int?)null;
		}

		public static long? TryParseInt64(this string str) {
			return Int64.TryParse(str, out long n) ? n : (long?)null;
		}

		public static string NullIfEmpty(this string str) {
			return str.Length != 0 ? str : null;
		}

		public static string ToHexString(this byte[] bytes, bool upperCase) {
			var sb = new StringBuilder(bytes.Length * 2);
			foreach (byte b in bytes) {
				sb.Append(b.ToString(upperCase ? "X2" : "x2"));
			}
			return sb.ToString();
		}

		public static IAsyncResult BeginInvoke(this Control control, Action action) {
			return control.BeginInvoke(action);
		}

		public static object Invoke(this Control control, Action action) {
			return control.Invoke(action);
		}

		public static void TryBeginInvoke(this Control control, Action action) {
			try {
				control.BeginInvoke(action);
			}
			catch (InvalidOperationException) { }
		}

		public static void TryInvoke(this Control control, Action action) {
			try {
				control.Invoke(action);
			}
			catch (InvalidOperationException) { }
		}
	}
}
