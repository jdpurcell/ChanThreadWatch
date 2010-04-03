using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web;

namespace ChanThreadWatch {
	public static class General {
		public static string Version {
			get {
				Version ver = Assembly.GetExecutingAssembly().GetName().Version;
				return ver.Major + "." + ver.Minor + "." + ver.Revision;
			}
		}

		public static string ReleaseDate {
			get {
				return "2010-Jan-01";
			}
		}

		public static string ProgramURL {
			get {
				return "http://sites.google.com/site/chanthreadwatch/";
			}
		}

		private static Stream GetResponseStream(string url, string auth, string referer, ref DateTime? cacheTime, out string charSet) {
			HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
			req.UserAgent = (Settings.UseCustomUserAgent == true) ? Settings.CustomUserAgent : ("Chan Thread Watch " + Version);
			req.Referer = referer;
			if (cacheTime != null) {
				req.IfModifiedSince = cacheTime.Value;
			}
			if (!String.IsNullOrEmpty(auth)) {
				Encoding encoding = Encoding.GetEncoding("iso-8859-1");
				req.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(encoding.GetBytes(auth)));
			}
			HttpWebResponse resp;
			try {
				resp = (HttpWebResponse)req.GetResponse();
				cacheTime = null;
				if (resp.Headers["Last-Modified"] != null) {
					try {
						// Parse the time string ourself instead of using .LastModified because
						// older versions of Mono don't convert it from GMT to local.
						cacheTime = DateTime.ParseExact(resp.Headers["Last-Modified"], new string[] {
							"r", "dddd, dd-MMM-yy HH:mm:ss G\\MT", "ddd MMM d HH:mm:ss yyyy" },
							CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces |
							DateTimeStyles.AssumeUniversal);
					}
					catch { }
				}
			}
			catch (WebException ex) {
				if (ex.Status == WebExceptionStatus.ProtocolError) {
					HttpStatusCode code = ((HttpWebResponse)ex.Response).StatusCode;
					if (code == HttpStatusCode.NotFound) {
						throw new HTTP404Exception();
					}
					else if (code == HttpStatusCode.NotModified) {
						throw new HTTP304Exception();
					}
				}
				throw;
			}
			charSet = GetCharSetFromContentType(resp.ContentType);
			return resp.GetResponseStream();
		}

		private static Stream GetResponseStream(string url, string auth, string referer) {
			DateTime? cacheTime = null;
			string charSet;
			return GetResponseStream(url, auth, referer, ref cacheTime, out charSet);
		}

		private static void CopyStream(Stream srcStream, params Stream[] dstStreams) {
			byte[] data = new byte[8192];
			while (true) {
				int dataLen = srcStream.Read(data, 0, data.Length);
				if (dataLen == 0) break;
				foreach (Stream dstStream in dstStreams) {
					if (dstStream != null) {
						dstStream.Write(data, 0, dataLen);
					}
				}
			}
		}

		public static string GetToString(string url, string auth, string savePath, ref DateTime? cacheTime, out Encoding encoding, List<ReplaceInfo> replaceList) {
			Stream rs = null;
			MemoryStream ms = null;
			FileStream fs = null;
			try {
				string httpCharSet;
				rs = GetResponseStream(url, auth, null, ref cacheTime, out httpCharSet);
				ms = new MemoryStream();
				if (savePath != null) {
					fs = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.Read);
				}
				CopyStream(rs, ms, fs);
				byte[] bytes = ms.ToArray();
				encoding = GetEncoding(bytes, httpCharSet);
				return BytesToString(bytes, encoding, replaceList);
			}
			finally {
				if (rs != null) rs.Close();
				if (ms != null) ms.Close();
				if (fs != null) fs.Close();
			}
		}

		public static string GetToString(string url) {
			DateTime? cacheTime = null;
			Encoding encoding;
			return GetToString(url, null, null, ref cacheTime, out encoding, null);
		}

		public static byte[] GetToFile(string url, string auth, string referer, string path, HashType hashType) {
			Stream rs = null;
			FileStream fs = null;
			HashGeneratorStream hs = null;
			try {
				rs = GetResponseStream(url, auth, referer);
				fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
				if (hashType != HashType.None) {
					hs = new HashGeneratorStream(hashType);
				}
				CopyStream(rs, fs, hs);
				return (hs != null) ? hs.GetDataHash() : null;
			}
			finally {
				if (rs != null) rs.Close();
				if (fs != null) fs.Close();
				if (hs != null) hs.Close();
			}
		}

		// Converts all whitespace to regular spaces and only keeps 1 space consecutively.
		// This simplifies parsing, e.g. in FindElement.  Also removes null characters, which
		// helps DetectCharacterSet convert UTF16/UTF32 to an ASCII string.
		public static string BytesToString(byte[] bytes, Encoding encoding, List<ReplaceInfo> replaceList) {
			int preambleLen = encoding.GetPreamble().Length;
			char[] src = encoding.GetChars(bytes, preambleLen, bytes.Length - preambleLen);
			char[] dst = new char[src.Length];
			int iDst = 0;
			bool prevWasSpace = false;
			int prevNewLine = 0;
			for (int iSrc = 0; iSrc < src.Length; iSrc++) {
				if (Char.IsWhiteSpace(src[iSrc])) {
					if (!prevWasSpace) {
						dst[iDst++] = ' ';
					}
					if (((src[iSrc] == '\r') || (src[iSrc] == '\n')) && (iDst != prevNewLine) && (replaceList != null)) {
						replaceList.Add(
							new ReplaceInfo {
								Offset = iDst - 1,
								Length = 1,
								Value = Environment.NewLine,
								Type = ReplaceType.NewLine
							});
						prevNewLine = iDst;
					}
					prevWasSpace = true;
				}
				else if (src[iSrc] != 0) {
					dst[iDst++] = src[iSrc];
					prevWasSpace = false;
				}
			}
			return new string(dst, 0, iDst);
		}

		public static string BytesToString(byte[] bytes, Encoding encoding) {
			return BytesToString(bytes, encoding, null);
		}

		public static void WriteReplacedString(string str, List<ReplaceInfo> replaceList, TextWriter outStream) {
			int offset = 0;
			replaceList.Sort((x, y) => x.Offset.CompareTo(y.Offset));
			for (int iReplace = 0; iReplace < replaceList.Count; iReplace++) {
				ReplaceInfo replace = replaceList[iReplace];
				if (replace.Offset < offset || replace.Length < 0) continue;
				if (replace.Offset + replace.Length > str.Length) break;
				if (replace.Offset > offset) {
					outStream.Write(str.Substring(offset, replace.Offset - offset));
				}
				if (!String.IsNullOrEmpty(replace.Value)) {
					outStream.Write(replace.Value);
				}
				offset = replace.Offset + replace.Length;
			}
			if (str.Length > offset) {
				outStream.Write(str.Substring(offset));
			}
		}

		private static Encoding GetEncoding(byte[] bytes, string httpCharSet) {
			string charSet = httpCharSet ?? DetectCharacterSet(bytes);
			if (charSet != null) {
				if (IsUTF(charSet)) {
					int bomLength;
					bool? bomIsBE;
					bool? charSetIsBE;
					bool isBigEndian;
					bool hasBOM;

					bomLength = GetBOMLength(bytes, out bomIsBE);

					if (charSet.EndsWith("BE", StringComparison.OrdinalIgnoreCase)) charSetIsBE = true;
					else if (charSet.EndsWith("LE", StringComparison.OrdinalIgnoreCase)) charSetIsBE = false;
					else charSetIsBE = null;

					isBigEndian = bomIsBE ?? charSetIsBE ?? true;
					hasBOM = bomLength != 0;

					if (IsUTF8(charSet)) {
						return new UTF8Encoding(hasBOM);
					}
					else if (IsUTF16(charSet)) {
						return new UnicodeEncoding(isBigEndian, hasBOM);
					}
					else if (IsUTF32(charSet)) {
						return new UTF32Encoding(isBigEndian, hasBOM);
					}
				}
				else {
					try {
						return Encoding.GetEncoding(charSet);
					}
					catch { }
				}
			}
			return Encoding.GetEncoding("iso-8859-1");
		}

		private static string DetectCharacterSet(byte[] bytes) {
			string html = BytesToString(bytes, Encoding.ASCII);
			ElementInfo elem;
			string value;
			int headClose;
			elem = FindElement(html, "?xml", 0);
			if (elem != null && elem.Offset <= 4) { // Allow for 3 byte BOM and 1 space preceding
				value = General.GetAttributeValue(elem, "encoding");
				if (!String.IsNullOrEmpty(value)) {
					return value;
				}
			}
			headClose = FindElementClose(html, "head", 0);
			if (headClose != -1) {
				int offset = 0;
				while ((elem = FindElement(html, "meta", offset, headClose)) != null) {
					offset = elem.Offset + 1;
					value = GetAttributeValue(elem, "http-equiv");
					if (String.IsNullOrEmpty(value)) continue;
					if (!value.Trim().Equals("Content-Type", StringComparison.OrdinalIgnoreCase)) continue;
					value = GetAttributeValue(elem, "content");
					if (!String.IsNullOrEmpty(value)) {
						value = GetCharSetFromContentType(value);
						if (value != null) {
							return value;
						}
					}
					break;
				}
			}
			return null;
		}

		private static string GetCharSetFromContentType(string contentType) {
			foreach (string part in contentType.Split(';')) {
				if (part.TrimStart().StartsWith("charset", StringComparison.OrdinalIgnoreCase)) {
					int pos = part.IndexOf('=');
					if (pos != -1) {
						string value = part.Substring(pos + 1).Trim();
						if (value.Length != 0) {
							return value;
						}
					}
				}
			}
			return null;
		}

		private static int GetBOMLength(byte[] bytes, out bool? isBigEndian) {
			isBigEndian = null;
			if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) {
				return 3;
			}
			if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) {
				isBigEndian = true;
				return 2;
			}
			if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) {
				isBigEndian = false;
				return 2;
			}
			if (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF) {
				isBigEndian = true;
				return 4;
			}
			if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00) {
				isBigEndian = false;
				return 4;
			}
			return 0;
		}

		private static bool IsUTF8(string charSet) {
			return charSet.Equals("UTF-8", StringComparison.OrdinalIgnoreCase);
		}

		private static bool IsUTF16(string charSet) {
			return charSet.Equals("UTF-16", StringComparison.OrdinalIgnoreCase) ||
				   charSet.Equals("UTF-16BE", StringComparison.OrdinalIgnoreCase) ||
				   charSet.Equals("UTF-16LE", StringComparison.OrdinalIgnoreCase);
		}

		private static bool IsUTF32(string charSet) {
			return charSet.Equals("UTF-32", StringComparison.OrdinalIgnoreCase) ||
				   charSet.Equals("UTF-32BE", StringComparison.OrdinalIgnoreCase) ||
				   charSet.Equals("UTF-32LE", StringComparison.OrdinalIgnoreCase);
		}

		private static bool IsUTF(string charset) {
			return IsUTF8(charset) || IsUTF16(charset) || IsUTF32(charset);
		}

		public static bool ArraysAreEqual<T>(T[] a, T[] b) where T : IComparable {
			if (a.Length != b.Length) return false;
			for (int i = 0; i < a.Length; i++) {
				if (a[i].CompareTo(b[i]) != 0) return false;
			}
			return true;
		}

		public static string ProperURL(string absoluteURL) {
			try {
				UriBuilder ub = new UriBuilder(absoluteURL);
				ub.Fragment = String.Empty;
				return Uri.UnescapeDataString(ub.Uri.ToString());
			}
			catch {
				return null;
			}
		}

		public static string ProperURL(string baseURL, string relativeURL) {
			try {
				return ProperURL(new Uri(new Uri(baseURL), relativeURL).AbsoluteUri);
			}
			catch {
				return null;
			}
		}

		public static ElementInfo FindElement(string html, string name, int offset, int htmlLen) {
			ElementInfo elem = new ElementInfo();

			elem.Attributes = new List<AttributeInfo>();

			while (offset < htmlLen) {
				int pos;

				int elementStart = html.IndexOf('<', offset);
				if (elementStart == -1) break;

				pos = elementStart + 1;
				if (pos < htmlLen && html[pos] == ' ') pos++;
				int nameEnd = html.IndexOfAny(new[] { ' ', '>' }, pos);
				if (nameEnd == -1 || nameEnd - pos != name.Length || String.Compare(html, pos, name, 0, name.Length, StringComparison.OrdinalIgnoreCase) != 0) goto NextElement;

				elem.Offset = elementStart;
				elem.Name = html.Substring(pos, nameEnd - pos);

				pos = nameEnd;
				while (pos < htmlLen) {
					if (html[pos] == ' ') pos++;
					if (pos < htmlLen && html[pos] == '>') {
						elem.Length = (pos + 1) - elementStart;
						return elem;
					}

					int attrNameEnd = html.IndexOfAny(new[] { ' ', '=', '>' }, pos);
					if (attrNameEnd == -1) goto NextElement;

					AttributeInfo attrInfo = new AttributeInfo();
					attrInfo.Name = html.Substring(pos, attrNameEnd - pos);
					attrInfo.Value = String.Empty;
					attrInfo.Offset = pos;
					attrInfo.Length = attrNameEnd - attrInfo.Offset;

					pos = attrNameEnd;
					if (html[pos] == ' ') pos++;
					if (pos < htmlLen && html[pos] == '=') {
						pos++;
						if (pos < htmlLen && html[pos] == ' ') pos++;

						int attrValEnd;
						if (pos < htmlLen && html[pos] == '"') {
							pos++;
							attrValEnd = html.IndexOf('"', pos);
						}
						else if (pos < htmlLen && html[pos] == '\'') {
							pos++;
							attrValEnd = html.IndexOf('\'', pos);
						}
						else {
							attrValEnd = html.IndexOfAny(new[] { ' ', '>' }, pos);
						}
						if (attrValEnd == -1) goto NextElement;

						attrInfo.Value = html.Substring(pos, attrValEnd - pos);

						pos = attrValEnd;
						if (html[pos] == '"' || html[pos] == '\'') pos++;

						attrInfo.Length = pos - attrInfo.Offset;
					}

					elem.Attributes.Add(attrInfo);
				}

			NextElement:
				offset = elementStart + 1;
			}

			return null;
		}

		public static ElementInfo FindElement(string html, string name, int offset) {
			return FindElement(html, name, offset, html.Length);
		}

		public static int FindElementClose(string html, string name, int offset, int htmlLen) {
			while (offset < htmlLen) {
				int pos;

				int elementStart = html.IndexOf('<', offset);
				if (elementStart == -1) break;

				pos = elementStart + 1;
				if (pos < htmlLen && html[pos] == ' ') pos++;
				if (pos < htmlLen && html[pos] == '/') pos++;
				else goto NextElement;
				if (pos < htmlLen && html[pos] == ' ') pos++;
				if (htmlLen - pos >= name.Length &&
					String.Compare(html, pos, name, 0, name.Length, StringComparison.OrdinalIgnoreCase) == 0)
				{
					pos += name.Length;
				}
				else goto NextElement;
				if (pos < htmlLen && html[pos] == ' ') pos++;
				if (pos < htmlLen && html[pos] == '>') pos++;
				else goto NextElement;

				return elementStart;

			NextElement:
				offset = elementStart + 1;
			}

			return -1;
		}

		public static int FindElementClose(string html, string name, int offset) {
			return FindElementClose(html, name, offset, html.Length);
		}

		public static AttributeInfo GetAttribute(ElementInfo element, string attributeName) {
			foreach (AttributeInfo attr in element.Attributes) {
				if (attr.Name.Equals(attributeName, StringComparison.OrdinalIgnoreCase)) {
					return attr;
				}
			}
			return null;
		}

		public static string GetAttributeValue(ElementInfo element, string attributeName) {
			AttributeInfo attr = GetAttribute(element, attributeName);
			return (attr != null) ? attr.Value : null;
		}

		public static void AddOtherReplaces(string html, List<ReplaceInfo> replaceList) {
			ElementInfo elem;
			int offset;

			offset = 0;
			while ((elem = FindElement(html, "base", offset)) != null) {
				offset = elem.Offset + 1;
				replaceList.Add(
					new ReplaceInfo {
						Offset = elem.Offset,
						Length = elem.Length,
						Type = ReplaceType.Other,
						Value = String.Empty
					});
			}
		}

		public static string URLFilename(string url) {
			int pos = url.LastIndexOf("/");
			return (pos == -1) ? String.Empty : url.Substring(pos + 1);
		}

		public static string CleanFilename(string filename) {
			char[] src = filename.ToCharArray();
			char[] dst = new char[src.Length];
			char[] inv = Path.GetInvalidFileNameChars();
			int iSrc = 0;
			int iDst = 0;

			while (iSrc < src.Length) {
				char c = src[iSrc++];
				for (int j = 0; j < inv.Length; j++) {
					if (c == inv[j]) {
						c = (char)0;
						break;
					}
				}
				if (c != 0) {
					dst[iDst++] = c;
				}
			}

			return new string(dst, 0, iDst);
		}
	}
}
