﻿using System;

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

		public static string[] Split(this string str, params string[] separator) {
			return str.Split(separator, StringSplitOptions.None);
		}

		public static int ParseInt32(this string str) {
			return Int32.Parse(str);
		}
	}
}
