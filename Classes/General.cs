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
				return "2009-Dec-28";
			}
		}

		public static string ProgramURL {
			get {
				return "http://sites.google.com/site/chanthreadwatch/";
			}
		}

		private static Stream GetResponseStream(string url, string auth, string referer, ref DateTime? cacheTime) {
			HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
			req.UserAgent = (Settings.UseCustomUserAgent == true) ? Settings.CustomUserAgent : ("Chan Thread Watch " + Version);
			req.Referer = referer;
			if (cacheTime != null) {
				req.IfModifiedSince = cacheTime.Value;
			}
			if (!String.IsNullOrEmpty(auth)) {
				Encoding encoding;
				try {
					encoding = Encoding.GetEncoding("iso-8859-1");
				}
				catch {
					encoding = Encoding.ASCII;
				}
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
			return resp.GetResponseStream();
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

		public static string GetToString(string url, string auth, string savePath, ref DateTime? cacheTime) {
			Stream rs = null;
			MemoryStream ms = null;
			FileStream fs = null;
			try {
				rs = GetResponseStream(url, auth, null, ref cacheTime);
				ms = new MemoryStream();
				if (savePath != null) {
					fs = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.Read);
				}
				CopyStream(rs, ms, fs);
				return BytesToString(ms.ToArray());
			}
			finally {
				if (rs != null) rs.Close();
				if (ms != null) ms.Close();
				if (fs != null) fs.Close();
			}
		}

		public static string GetToString(string url) {
			DateTime? cacheTime = null;
			return GetToString(url, null, null, ref cacheTime);
		}

		public static byte[] GetToFile(string url, string auth, string referer, string path, HashType hashType) {
			DateTime? cacheTime = null;
			Stream rs = null;
			FileStream fs = null;
			HashGeneratorStream hs = null;
			try {
				rs = GetResponseStream(url, auth, referer, ref cacheTime);
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

		private static string BytesToString(byte[] bytes) {
			char[] src = Encoding.UTF8.GetChars(bytes);
			char[] dst = new char[src.Length];
			bool prevWasSpace = false;
			int iDst = 0;
			for (int iSrc = 0; iSrc < src.Length; iSrc++) {
				if (Char.IsWhiteSpace(src[iSrc])) {
					if (!prevWasSpace) {
						dst[iDst++] = ' ';
					}
					prevWasSpace = true;
				}
				else {
					dst[iDst++] = src[iSrc];
					prevWasSpace = false;
				}
			}
			return new string(dst, 0, iDst);
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
				UriBuilder ub = new UriBuilder(HttpUtility.HtmlDecode(absoluteURL));
				ub.Fragment = String.Empty;
				return Uri.UnescapeDataString(ub.Uri.ToString());
			}
			catch {
				return null;
			}
		}

		public static string ProperURL(string baseURL, string relativeURL) {
			try {
				return ProperURL(new Uri(new Uri(baseURL), HttpUtility.HtmlDecode(relativeURL)).AbsoluteUri);
			}
			catch {
				return null;
			}
		}

		public static ElementInfo FindElement(string html, string name, int offset, int htmlLen) {
			ElementInfo elem = new ElementInfo();

			elem.Attributes = new List<KeyValuePair<string, string>>();

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

					string attrName = html.Substring(pos, attrNameEnd - pos);

					string attrVal = String.Empty;
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

						attrVal = html.Substring(pos, attrValEnd - pos);

						pos = attrValEnd;
						if (html[pos] == '"' || html[pos] == '\'') pos++;
					}
					elem.Attributes.Add(new KeyValuePair<string, string>(attrName, attrVal));
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

		public static string GetAttributeValue(ElementInfo element, string attributeName) {
			foreach (var attr in element.Attributes) {
				if (attr.Key.Equals(attributeName, StringComparison.OrdinalIgnoreCase)) {
					return attr.Value;
				}
			}
			return null;
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
