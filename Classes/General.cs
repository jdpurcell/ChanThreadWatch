using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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
				return "2011-Jan-02";
			}
		}

		public static string ProgramURL {
			get {
				return "http://sites.google.com/site/chanthreadwatch/";
			}
		}

		public static Action DownloadAsync(string url, string auth, string referer, string connectionGroupName, DateTime? cacheLastModifiedTime, Action<HttpWebResponse> onResponse, Action<byte[], int> onDownloadChunk, Action onComplete, Action<Exception> onException) {
			const int readBufferSize = 8192;
			const int requestTimeoutMS = 60000;
			const int readTimeoutMS = 60000;
			object sync = new object();
			bool aborting = false;
			HttpWebRequest request = null;
			HttpWebResponse response = null;
			Stream responseStream = null;
			Action cleanup = () => {
				if (request != null) {
					request.Abort();
					request = null;
				}
				if (responseStream != null) {
					try { responseStream.Close(); } catch { }
					responseStream = null;
				}
				if (response != null) {
					try { response.Close(); } catch { }
					response = null;
				}
			};
			Action<Exception> abortDownloadInternal = (ex) => {
				lock (sync) {
					if (aborting) return;
					aborting = true;
					cleanup();
					onException(ex);
				}
			};
			Action abortDownload = () => {
				ThreadPool.QueueUserWorkItem((s) => {
					abortDownloadInternal(new Exception("Download has been aborted."));
				});
			};
			lock (sync) {
				try {
					request = (HttpWebRequest)WebRequest.Create(url);
					if (connectionGroupName != null) {
						request.ConnectionGroupName = connectionGroupName;
					}
					request.UserAgent = (Settings.UseCustomUserAgent == true) ? Settings.CustomUserAgent : ("Chan Thread Watch " + Version);
					request.Referer = referer;
					if (cacheLastModifiedTime != null) {
						request.IfModifiedSince = cacheLastModifiedTime.Value;
					}
					if (!String.IsNullOrEmpty(auth)) {
						Encoding encoding = Encoding.GetEncoding("iso-8859-1");
						request.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(encoding.GetBytes(auth)));
					}
					// Unfortunately BeginGetResponse blocks until the DNS lookup has finished
					IAsyncResult requestResult = request.BeginGetResponse((requestResultParam) => {
						lock (sync) {
							try {
								if (aborting) return;
								response = (HttpWebResponse)request.EndGetResponse(requestResultParam);
								responseStream = response.GetResponseStream();
								onResponse(response);
								byte[] buff = new byte[readBufferSize];
								AsyncCallback readCallback = null;
								readCallback = (readResultParam) => {
									lock (sync) {
										try {
											if (aborting) return;
											if (readResultParam != null) {
												int bytesRead = responseStream.EndRead(readResultParam);
												if (bytesRead == 0) {
													request = null;
													onComplete();
													aborting = true;
													cleanup();
													return;
												}
												onDownloadChunk(buff, bytesRead);
											}
											IAsyncResult readResult = responseStream.BeginRead(buff, 0, buff.Length, readCallback, null);
											ThreadPool.RegisterWaitForSingleObject(readResult.AsyncWaitHandle,
												(state, timedOut) => {
													if (!timedOut) return;
													abortDownloadInternal(new Exception("Timed out while reading response."));
												}, null, readTimeoutMS, true);
										}
										catch (Exception ex) {
											abortDownloadInternal(ex);
										}
									}
								};
								readCallback(null);
							}
							catch (Exception ex) {
								if (ex is WebException) {
									WebException webEx = (WebException)ex;
									if (webEx.Status == WebExceptionStatus.ProtocolError) {
										HttpStatusCode code = ((HttpWebResponse)webEx.Response).StatusCode;
										if (code == HttpStatusCode.NotFound) {
											ex = new HTTP404Exception();
										}
										else if (code == HttpStatusCode.NotModified) {
											ex = new HTTP304Exception();
										}
									}
								}
								abortDownloadInternal(ex);
							}
						}
					}, null);
					ThreadPool.RegisterWaitForSingleObject(requestResult.AsyncWaitHandle,
						(state, timedOut) => {
							if (!timedOut) return;
							abortDownloadInternal(new Exception("Timed out while waiting for response."));
						}, null, requestTimeoutMS, true);
				}
				catch (Exception ex) {
					abortDownloadInternal(ex);
				}
			}
			return abortDownload;
		}

		public static string DownloadPageToString(string url) {
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
			request.UserAgent = "Chan Thread Watch " + Version;
			HttpWebResponse response = null;
			Stream responseStream = null;
			MemoryStream memoryStream = null;
			try {
				response = (HttpWebResponse)request.GetResponse();
				responseStream = response.GetResponseStream();
				memoryStream = new MemoryStream();
				CopyStream(responseStream, memoryStream);
				byte[] pageBytes = memoryStream.ToArray();
				Encoding encoding = DetectHTMLEncoding(pageBytes, GetCharSetFromContentType(response.ContentType));
				return HTMLBytesToString(pageBytes, encoding);
			}
			finally {
				if (responseStream != null) try { responseStream.Close(); } catch { }
				if (response != null) try { response.Close(); } catch { }
				if (memoryStream != null) try { memoryStream.Close(); } catch { }
			}
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

		// Converts all whitespace to regular spaces and only keeps 1 space consecutively.
		// This simplifies parsing, e.g. in FindElement.  Also removes null characters, which
		// helps DetectCharacterSet convert UTF16/UTF32 to an ASCII string.
		public static string HTMLBytesToString(byte[] bytes, Encoding encoding, List<ReplaceInfo> replaceList) {
			int preambleLen = encoding.GetPreamble().Length;
			char[] src = encoding.GetChars(bytes, preambleLen, bytes.Length - preambleLen);
			char[] dst = new char[src.Length];
			int iDst = 0;
			bool inWhiteSpace = false;
			bool inNewLine = false;
			for (int iSrc = 0; iSrc < src.Length; iSrc++) {
				if (src[iSrc] == ' ' || src[iSrc] == '\r' || src[iSrc] == '\n' || src[iSrc] == '\t' || src[iSrc] == '\f') {
					if (!inWhiteSpace) {
						dst[iDst++] = ' ';
					}
					inWhiteSpace = true;
					if ((src[iSrc] == '\r' || src[iSrc] == '\n') && !inNewLine && replaceList != null) {
						replaceList.Add(
							new ReplaceInfo {
								Offset = iDst - 1,
								Length = 1,
								Value = Environment.NewLine,
								Type = ReplaceType.NewLine
							});
						inNewLine = true;
					}
				}
				else if (src[iSrc] != 0) {
					dst[iDst++] = src[iSrc];
					inWhiteSpace = false;
					inNewLine = false;
				}
			}
			return new string(dst, 0, iDst);
		}

		public static string HTMLBytesToString(byte[] bytes, Encoding encoding) {
			return HTMLBytesToString(bytes, encoding, null);
		}

		public static DateTime? GetResponseLastModifiedTime(HttpWebResponse response) {
			DateTime? lastModified = null;
			if (response.Headers["Last-Modified"] != null) {
				try {
					// Parse the time string ourself instead of using .LastModified because
					// older versions of Mono don't convert it from GMT to local.
					lastModified = DateTime.ParseExact(response.Headers["Last-Modified"], new string[] {
						"r", "dddd, dd-MMM-yy HH:mm:ss G\\MT", "ddd MMM d HH:mm:ss yyyy" },
						CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces |
						DateTimeStyles.AssumeUniversal);
				}
				catch { }
			}
			return lastModified;
		}

		public static Encoding DetectHTMLEncoding(byte[] bytes, string httpCharSet) {
			string charSet = httpCharSet ?? DetectHTMLCharacterSet(bytes);
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

		private static string DetectHTMLCharacterSet(byte[] bytes) {
			string html = HTMLBytesToString(bytes, Encoding.ASCII);
			ElementInfo elem;
			string value;
			int headClose;
			elem = FindElement(html, 0, "?xml");
			if (elem != null && elem.Offset <= 4) { // Allow for 3 byte BOM and 1 space preceding
				value = elem.GetAttributeValue("encoding");
				if (!String.IsNullOrEmpty(value)) {
					return value;
				}
			}
			headClose = FindElementClose(html, 0, "head");
			if (headClose != -1) {
				int offset = 0;
				while ((elem = FindElement(html, offset, headClose, "meta")) != null) {
					offset = elem.Offset + 1;
					value = elem.GetAttributeValue("http-equiv");
					if (String.IsNullOrEmpty(value)) continue;
					if (!value.Trim().Equals("Content-Type", StringComparison.OrdinalIgnoreCase)) continue;
					value = elem.GetAttributeValue("content");
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

		public static string GetCharSetFromContentType(string contentType) {
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
			if (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF) {
				isBigEndian = true;
				return 4;
			}
			if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00) {
				isBigEndian = false;
				return 4;
			}
			if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) {
				isBigEndian = true;
				return 2;
			}
			if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) {
				isBigEndian = false;
				return 2;
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

		public static string GetRelativeDirectoryPath(string dir, string baseDir) {
			if (dir.Length != 0 && Path.IsPathRooted(dir)) {
				Uri baseDirUri = new Uri(Path.Combine(baseDir, "dummy.txt"));
				Uri targetDirUri = new Uri(Path.Combine(dir, "dummy.txt"));
				try {
					dir = Uri.UnescapeDataString(baseDirUri.MakeRelativeUri(targetDirUri).ToString());
				}
				catch (UriFormatException) {
					// Work-around for Mono when determining the relative URI of directories
					// on different drives in Windows.
					return dir;
				}
				dir = (dir.Length == 0) ? "." : Path.GetDirectoryName(dir.Replace('/', Path.DirectorySeparatorChar));
			}
			return dir;
		}

		public static string GetAbsoluteDirectoryPath(string dir, string baseDir) {
			if (dir.Length != 0 && !Path.IsPathRooted(dir)) {
				dir = Path.GetFullPath(Path.Combine(baseDir, dir));
			}
			return dir;
		}

		public static string GetRelativeFilePath(string filePath, string baseDir) {
			if (filePath.Length != 0 && Path.IsPathRooted(filePath)) {
				string dir = Path.GetDirectoryName(filePath);
				string fileName = Path.GetFileName(filePath);
				dir = GetRelativeDirectoryPath(dir, baseDir);
				filePath = (dir == ".") ? fileName : Path.Combine(dir, fileName);
			}
			return filePath;
		}

		public static string GetAbsoluteFilePath(string filePath, string baseDir) {
			if (filePath.Length != 0 && !Path.IsPathRooted(filePath)) {
				filePath = Path.GetFullPath(Path.Combine(baseDir, filePath));
			}
			return filePath;
		}

		public static string GetLastDirectory(string dir) {
			char[] separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
			dir = dir.TrimEnd(separators);
			int pos = dir.LastIndexOfAny(separators);
			return (pos == -1) ? dir : dir.Substring(pos + 1);
		}

		public static string RemoveLastDirectory(string dir) {
			char[] separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
			dir = dir.TrimEnd(separators);
			int pos = dir.LastIndexOfAny(separators);
			return (pos == -1) ? String.Empty : dir.Substring(0, pos);
		}

		public static int GetMaximumFileNameLength(string dir) {
			// Kind of a binary search except we only know whether the middle
			// item is <= or > the target rather than <, =, or >.
			int min = 0;
			int max = 4096;
			while (max >= min + 2) {
				int n = (min + max) / 2;
				if (IsFileNameTooLong(dir, n)) {
					max = n - 1;
				}
				else {
					min = n;
				}
			}
			if (max > min) {
				return IsFileNameTooLong(dir, max) ? min : max;
			}
			else {
				return min;
			}
		}

		public static bool IsFileNameTooLong(string dir, int fileNameLength) {
			if (!Directory.Exists(dir)) throw new DirectoryNotFoundException();
			string path = null;
			bool foundFreeFileName = false;
			for (char c = 'a'; c <= 'z'; c++) {
				path = Path.Combine(dir, new string(c, fileNameLength));
				if (!File.Exists(path)) {
					foundFreeFileName = true;
					break;
				}
			}
			if (!foundFreeFileName) {
				throw new Exception("Unable to determine if filename is too long.");
			}
			try {
				using (File.Create(path)) { }
				try { File.Delete(path); } catch { }
				return false;
			}
			catch (PathTooLongException) {
				return true;
			}
			catch (DirectoryNotFoundException) {
				// Work-around for Mono
				return true;
			}
		}

		public static void EnsureThreadPoolMaxThreads(int minWorkerThreads, int minCompletionPortThreads) {
			int workerThreads;
			int completionPortThreads;
			ThreadPool.GetMaxThreads(out workerThreads, out completionPortThreads);
			if (workerThreads < minWorkerThreads || completionPortThreads < minCompletionPortThreads) {
				ThreadPool.SetMaxThreads(Math.Max(workerThreads, minWorkerThreads), Math.Max(completionPortThreads, minCompletionPortThreads));
			}
		}

		public static ulong Calculate64BitMD5(byte[] bytes) {
			MD5CryptoServiceProvider hashAlgo = new MD5CryptoServiceProvider();
			return BytesTo64BitXor(hashAlgo.ComputeHash(bytes));
		}

		public static ulong BytesTo64BitXor(byte[] bytes) {
			ulong result = 0;
			for (int i = 0; i < bytes.Length; i++) {
				result ^= (ulong)bytes[i] << ((7 - (i % 8)) * 8);
			}
			return result;
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

		public static void AddOtherReplaces(string html, string url, List<ReplaceInfo> replaceList) {
			ElementInfo elem;
			int offset;

			offset = 0;
			while ((elem = FindElement(html, offset, "base", "link")) != null) {
				offset = elem.Offset + 1;
				if (elem.Name.Equals("base", StringComparison.OrdinalIgnoreCase)) {
					replaceList.Add(
						new ReplaceInfo {
							Offset = elem.Offset,
							Length = elem.Length,
							Type = ReplaceType.Other,
							Value = String.Empty
						});
				}
				else if (elem.Name.Equals("link", StringComparison.OrdinalIgnoreCase)) {
					AttributeInfo hrefAttr = elem.GetAttribute("href");
					if (hrefAttr != null) {
						replaceList.Add(
							new ReplaceInfo {
								Offset = hrefAttr.Offset,
								Length = hrefAttr.Length,
								Type = ReplaceType.Other,
								Value = "href=\"" + General.ProperURL(url, HttpUtility.HtmlDecode(hrefAttr.Value)) + "\""
							});
					}
				}
			}
		}

		public static ElementInfo FindElement(string html, int offset, int htmlLen, params string[] names) {
			ElementInfo elem = new ElementInfo();

			elem.Attributes = new List<AttributeInfo>();

			while (offset < htmlLen) {
				int pos;

				int elementStart = html.IndexOf('<', offset);
				if (elementStart == -1) break;

				pos = elementStart + 1;
				if (pos < htmlLen && html[pos] == ' ') pos++;
				int nameEnd = html.IndexOfAny(new[] { ' ', '>' }, pos);
				if (nameEnd == -1) goto NextElement;
				int nameLength = nameEnd - pos;
				bool nameIsMatch = Array.Exists(names, n => n.Length == nameLength &&
					String.Compare(html, pos, n, 0, n.Length, StringComparison.OrdinalIgnoreCase) == 0);
				if (!nameIsMatch) goto NextElement;

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

		public static ElementInfo FindElement(string html, int offset, params string[] names) {
			return FindElement(html, offset, html.Length, names);
		}

		public static int FindElementClose(string html, int offset, int htmlLen, string name) {
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

		public static int FindElementClose(string html, int offset, string name) {
			return FindElementClose(html, offset, html.Length, name);
		}

		public static string URLFileName(string url) {
			int pos = url.LastIndexOf("/");
			return (pos == -1) ? String.Empty : url.Substring(pos + 1);
		}

		public static string CleanFileName(string src) {
			char[] dst = new char[src.Length];
			char[] inv = Path.GetInvalidFileNameChars();
			int iDst = 0;
			for (int iSrc = 0; iSrc < src.Length; iSrc++) {
				char c = src[iSrc];
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

		public static int StrLen(byte[] bytes) {
			for (int i = 0; i < bytes.Length; i++) {
				if (bytes[i] == 0) return i;
			}
			return bytes.Length;
		}

		public static int StrLenW(byte[] bytes) {
			for (int i = 0; i < bytes.Length - 1; i += 2) {
				if (bytes[i] == 0 && bytes[i + 1] == 0) return i / 2;
			}
			return bytes.Length / 2;
		}
	}
}
