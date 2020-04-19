// --------------------------------------------------------------------------------
// Copyright (c) J.D. Purcell
//
// Licensed under the MIT License (see LICENSE.txt)
// --------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web;

namespace JDP {
	public static class General {
		public const string BuildDate = "20200419";
		public const string Version = "1.7.0.6"; // Frozen

		static General() {
			// HttpWebRequest uses ThreadPool for asynchronous calls
			EnsureThreadPoolMaxThreads(500, 1000);

			// Shouldn't matter since the limit is supposed to be per connection group
			ServicePointManager.DefaultConnectionLimit = Int32.MaxValue;

			// Ignore invalid certificates (workaround for Mono)
			ServicePointManager.ServerCertificateValidationCallback = (s, cert, chain, errors) => true;
		}

		public static string UserAgent {
			get => Settings.UseCustomUserAgent ? Settings.CustomUserAgent : $"Chan Thread Watch {BuildDate}";
		}

		public static string NormalizeNewLines(string str) {
			if (str.IndexOf('\r') == -1) {
				return str;
			}
			char[] dst = new char[str.Length];
			int iDst = 0;
			for (int iSrc = 0; iSrc < str.Length; iSrc++) {
				char c = str[iSrc];
				if (c == '\n' && iSrc >= 1 && str[iSrc - 1] == '\r') {
					// Skip line feed following carriage return
					continue;
				}
				if (c == '\r') {
					// Convert carriage return to line feed
					c = '\n';
				}
				dst[iDst++] = c;
			}
			return new string(dst, 0, iDst);
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
			void Cleanup() {
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
			}
			void AbortDownloadInternal(Exception ex) {
				lock (sync) {
					if (aborting) return;
					aborting = true;
					Cleanup();
					onException(ex);
				}
			}
			void AbortDownload() {
				ThreadPool.QueueUserWorkItem((s) => {
					AbortDownloadInternal(new Exception("Download has been aborted."));
				});
			}
			lock (sync) {
				try {
					request = (HttpWebRequest)WebRequest.Create(url);
					if (connectionGroupName != null) {
						request.ConnectionGroupName = connectionGroupName;
					}
					request.UserAgent = UserAgent;
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
								void ReadCallback(IAsyncResult readResultParam) {
									lock (sync) {
										try {
											if (aborting) return;
											if (readResultParam != null) {
												int bytesRead = responseStream.EndRead(readResultParam);
												if (bytesRead == 0) {
													request = null;
													onComplete();
													aborting = true;
													Cleanup();
													return;
												}
												onDownloadChunk(buff, bytesRead);
											}
											IAsyncResult readResult = responseStream.BeginRead(buff, 0, buff.Length, ReadCallback, null);
											ThreadPool.RegisterWaitForSingleObject(readResult.AsyncWaitHandle,
												(state, timedOut) => {
													if (!timedOut) return;
													AbortDownloadInternal(new Exception("Timed out while reading response."));
												}, null, readTimeoutMS, true);
										}
										catch (Exception ex) {
											AbortDownloadInternal(ex);
										}
									}
								};
								ReadCallback(null);
							}
							catch (Exception ex) {
								if (ex is WebException) {
									WebException webEx = (WebException)ex;
									if (webEx.Status == WebExceptionStatus.ProtocolError) {
										HttpStatusCode code = ((HttpWebResponse)webEx.Response).StatusCode;
										if (code == HttpStatusCode.NotFound) {
											ex = new Http404Exception();
										}
										else if (code == HttpStatusCode.NotModified) {
											ex = new Http304Exception();
										}
									}
								}
								AbortDownloadInternal(ex);
							}
						}
					}, null);
					ThreadPool.RegisterWaitForSingleObject(requestResult.AsyncWaitHandle,
						(state, timedOut) => {
							if (!timedOut) return;
							AbortDownloadInternal(new Exception("Timed out while waiting for response."));
						}, null, requestTimeoutMS, true);
				}
				catch (Exception ex) {
					AbortDownloadInternal(ex);
				}
			}
			return AbortDownload;
		}

		public static string DownloadPageToString(string url, Action<HttpWebRequest> withRequest = null) {
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
			request.UserAgent = UserAgent;
			withRequest?.Invoke(request);
			HttpWebResponse response = null;
			Stream responseStream = null;
			MemoryStream memoryStream = null;
			try {
				response = (HttpWebResponse)request.GetResponse();
				responseStream = response.GetResponseStream();
				memoryStream = new MemoryStream();
				responseStream.CopyTo(memoryStream);
				byte[] pageBytes = memoryStream.ToArray();
				Encoding encoding = DetectHtmlEncoding(pageBytes, response.ContentType);
				return encoding.GetString(pageBytes);
			}
			finally {
				if (responseStream != null) try { responseStream.Close(); } catch { }
				if (response != null) try { response.Close(); } catch { }
				if (memoryStream != null) try { memoryStream.Close(); } catch { }
			}
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

		public static Encoding DetectHtmlEncoding(byte[] bytes, string httpContentType) {
			string charSet =
				GetCharSetFromContentType(httpContentType) ??
				DetectCharacterSetFromBom(bytes) ??
				DetectCharacterSetFromContent(bytes, httpContentType);
			if (charSet != null) {
				if (IsUtf8(charSet)) {
					return new UTF8Encoding(HasBom(bytes));
				}
				else if (IsUtf16(charSet)) {
					return new UnicodeEncoding(IsUtfBigEndian(charSet) ?? false, HasBom(bytes));
				}
				else {
					try {
						return Encoding.GetEncoding(charSet);
					}
					catch { }
				}
			}
			return Encoding.GetEncoding("Windows-1252");
		}

		private static string DetectCharacterSetFromBom(byte[] bytes) {
			switch (GetBomType(bytes)) {
				case BomType.Utf8: return "UTF-8";
				case BomType.Utf16LE: return "UTF-16LE";
				case BomType.Utf16BE: return "UTF-16BE";
				default: return null;
			}
		}

		private static string DetectCharacterSetFromContent(byte[] bytes, string httpContentType) {
			string text = UnknownEncodingToString(bytes, 4096);
			HtmlParser htmlParser = new HtmlParser(text);
			string mimeType = GetMimeTypeFromContentType(httpContentType) ?? "";
			string charSet;

			if (mimeType.Equals("application/xhtml+xml", StringComparison.OrdinalIgnoreCase) ||
				mimeType.Equals("application/xml", StringComparison.OrdinalIgnoreCase) ||
				mimeType.Equals("text/xml", StringComparison.OrdinalIgnoreCase))
			{
				if (text.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)) {
					// XML declaration
					HtmlParser xmlParser = new HtmlParser("<" + text.Substring(2));
					HtmlTag xmlTag = xmlParser.Tags.Count >= 1 ? xmlParser.Tags[0] : null;
					if (xmlTag != null && xmlTag.NameEquals("xml") && xmlTag.Offset == 0) {
						charSet = xmlTag.GetAttributeValue("encoding");
						if (!String.IsNullOrEmpty(charSet)) return charSet;
					}
				}

				// Default
				return "UTF-8";
			}

			foreach (HtmlTag tag in htmlParser.FindStartTags("meta")) {
				// charset attribute
				charSet = tag.GetAttributeValue("charset");
				if (!String.IsNullOrEmpty(charSet)) return charSet;

				// http-equiv and content attributes
				if (tag.GetAttributeValueOrEmpty("http-equiv").Trim().Equals("Content-Type", StringComparison.OrdinalIgnoreCase)) {
					charSet = GetCharSetFromContentType(tag.GetAttributeValue("content"));
					if (!String.IsNullOrEmpty(charSet)) return charSet;
				}
			}

			return null;
		}

		public static string GetMimeTypeFromContentType(string contentType) {
			if (contentType == null) return null;
			int pos = contentType.IndexOf(';');
			if (pos != -1) {
				contentType = contentType.Substring(0, pos);
			}
			contentType = contentType.Trim();
			return contentType.Length != 0 ? contentType : null;
		}

		public static string GetCharSetFromContentType(string contentType) {
			if (contentType == null) return null;
			foreach (string part in contentType.Split(';')) {
				int pos = part.IndexOf('=');
				if (pos == -1) continue;
				string name = part.Substring(0, pos).Trim();
				if (!name.Equals("charset", StringComparison.OrdinalIgnoreCase)) continue;
				string value = part.Substring(pos + 1).Trim();
				bool isQuoted = value.Length >= 1 && (value[0] == '"' || value[0] == '\'');
				if (isQuoted) {
					pos = value.IndexOf(value[0], 1);
					if (pos == -1) pos = value.Length;
					value = value.Substring(1, pos - 1).Trim();
				}
				return value.Length != 0 ? value : null;
			}
			return null;
		}

		public static string UnknownEncodingToString(byte[] src, int maxLength) {
			byte[] dst = new byte[maxLength > 0 ? Math.Min(maxLength, src.Length) : src.Length];
			int iDst = 0;
			for (int iSrc = 0; iSrc < src.Length; iSrc++) {
				if (src[iSrc] == 0) continue;
				dst[iDst++] = src[iSrc];
				if (iDst >= dst.Length) break;
			}
			return Encoding.ASCII.GetString(dst, 0, iDst);
		}

		private static BomType GetBomType(byte[] bytes) {
			if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) return BomType.Utf8;
			if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) return BomType.Utf16LE;
			if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) return BomType.Utf16BE;
			return BomType.None;
		}

		private static bool HasBom(byte[] bytes) {
			return GetBomType(bytes) != BomType.None;
		}

		private static bool IsUtf8(string charSet) {
			return charSet.Equals("UTF-8", StringComparison.OrdinalIgnoreCase);
		}

		private static bool IsUtf16(string charSet) {
			return charSet.Equals("UTF-16", StringComparison.OrdinalIgnoreCase) ||
				   charSet.Equals("UTF-16BE", StringComparison.OrdinalIgnoreCase) ||
				   charSet.Equals("UTF-16LE", StringComparison.OrdinalIgnoreCase);
		}

		private static bool? IsUtfBigEndian(string charSet) {
			if (charSet.EndsWith("BE", StringComparison.OrdinalIgnoreCase)) return true;
			if (charSet.EndsWith("LE", StringComparison.OrdinalIgnoreCase)) return false;
			return null;
		}

		public static bool ArraysAreEqual<T>(T[] a, T[] b) where T : IComparable {
			if (a.Length != b.Length) return false;
			for (int i = 0; i < a.Length; i++) {
				if (a[i].CompareTo(b[i]) != 0) return false;
			}
			return true;
		}

		public static string GetAbsoluteUrl(Uri baseUri, string relativeUrl) {
			// AbsoluteUri can throw undocumented Exception (e.g. for "mailto:+")
			try {
				return Uri.TryCreate(baseUri, relativeUrl, out Uri uri) ? uri.AbsoluteUri : null;
			}
			catch { return null; }
		}

		public static string[] GetUrlPathComponents(Uri uri) {
			string path = uri.AbsolutePath;
			if (!path.StartsWith("/", StringComparison.Ordinal)) return new string[0];
			return path.Substring(1).Split('/');
		}

		public static string StripFragmentFromUrl(string url) {
			int pos = url.IndexOf('#');
			return pos != -1 ? url.Substring(0, pos) : url;
		}

		public static string CleanPageUrl(string url) {
			url = url.Trim();
			url = StripFragmentFromUrl(url);
			if (url.Length == 0) return null;
			if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
				!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			{
				url = "http://" + url;
			}
			if (url.IndexOf('/', url.IndexOf("//", StringComparison.Ordinal) + 2) == -1) return null;
			try {
				return Uri.TryCreate(url, UriKind.Absolute, out Uri uri) ? uri.AbsoluteUri : null;
			}
			catch { return null; }
		}

		public static string GetRelativeDirectoryPath(string dir, string baseDir) {
			if (dir.Length != 0 && Path.IsPathRooted(dir)) {
				Uri baseDirUri = new Uri(Path.Combine(baseDir, "dummy.txt"));
				Uri targetDirUri = new Uri(Path.Combine(dir, "dummy.txt"));
				try {
					dir = Uri.UnescapeDataString(baseDirUri.MakeRelativeUri(targetDirUri).ToString());
				}
				catch (UriFormatException) {
					// Workaround for Mono when determining the relative URI of directories
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
				// Workaround for Mono
				return true;
			}
		}

		public static void EnsureThreadPoolMaxThreads(int minWorkerThreads, int minCompletionPortThreads) {
			ThreadPool.GetMaxThreads(out int workerThreads, out int completionPortThreads);
			if (workerThreads < minWorkerThreads || completionPortThreads < minCompletionPortThreads) {
				ThreadPool.SetMaxThreads(Math.Max(workerThreads, minWorkerThreads), Math.Max(completionPortThreads, minCompletionPortThreads));
			}
		}

		public static byte[] TryBase64Decode(string s) {
			try {
				return Convert.FromBase64String(s);
			}
			catch { return null; }
		}

		public static byte[] CalculateMD5(byte[] bytes) =>
			CalculateHash<MD5CryptoServiceProvider>(bytes);

		public static byte[] CalculateSha1(byte[] bytes) =>
			CalculateHash<SHA1CryptoServiceProvider>(bytes);

		public static byte[] CalculateHash<T>(byte[] bytes) where T : HashAlgorithm, new() {
			using var hashAlgo = new T();
			return hashAlgo.ComputeHash(bytes);
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

		public static void AddOtherReplaces(HtmlParser htmlParser, string pageUrl, List<ReplaceInfo> replaceList) {
			HashSet<int> existingOffsets = new HashSet<int>();

			foreach (ReplaceInfo replace in replaceList) {
				existingOffsets.Add(replace.Offset);
			}

			if (Environment.NewLine != "\n") {
				int offset = 0;
				while ((offset = htmlParser.PreprocessedHtml.IndexOf('\n', offset)) != -1) {
					replaceList.Add(new ReplaceInfo {
						Offset = offset,
						Length = 1,
						Type = ReplaceType.Other,
						Value = Environment.NewLine
					});
					offset += 1;
				}
			}

			foreach (HtmlTag tag in htmlParser.FindStartTags("base")) {
				replaceList.Add(
					new ReplaceInfo {
						Offset = tag.Offset,
						Length = tag.Length,
						Type = ReplaceType.Other,
						Value = ""
					});
			}

			foreach (HtmlTag tag in htmlParser.FindStartTags("a", "img", "script", "link")) {
				bool isATag = tag.NameEquals("a");
				bool isImgTag = tag.NameEquals("img");
				bool isScriptTag = tag.NameEquals("script");
				bool isLinkTag = tag.NameEquals("link");
				bool usesHRefAttr = isATag || isLinkTag;
				bool usesSrcAttr = isImgTag || isScriptTag;
				if (usesHRefAttr || usesSrcAttr) {
					HtmlAttribute attribute = tag.GetAttribute(usesHRefAttr ? "href" : usesSrcAttr ? "src" : null);
					if (attribute != null && !existingOffsets.Contains(attribute.Offset)) {
						// Make attribute's URL absolute
						string newUrl = GetAbsoluteUrl(new Uri(pageUrl), HttpUtility.HtmlDecode(attribute.Value));
						// For links to anchors on the current page, use just the fragment
						if (isATag && newUrl != null && newUrl.Length > pageUrl.Length &&
							newUrl.StartsWith(pageUrl, StringComparison.Ordinal) && newUrl[pageUrl.Length] == '#')
						{
							newUrl = newUrl.Substring(pageUrl.Length);
						}
						if (newUrl != null) {
							replaceList.Add(
								new ReplaceInfo {
									Offset = attribute.Offset,
									Length = attribute.Length,
									Type = ReplaceType.Other,
									Value = attribute.Name + "=\"" + HttpUtility.HtmlAttributeEncode(newUrl) + "\""
								});
						}
					}
				}
			}
		}

		public static string UrlFileName(string url) {
			return url.SubstringAfterLast("/", StringComparison.Ordinal);
		}

		public static string CleanFileName(string src) {
			if (src == null) return null;
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
