using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace ChanThreadWatch {
	public class SiteHelper {
		protected string _url = String.Empty;
		protected string _html = String.Empty;

		public static SiteHelper GetInstance(string url) {
			string ns = (typeof(SiteHelper)).Namespace;
			string[] hostSplit = (new Uri(url)).Host.ToLower(CultureInfo.InvariantCulture).Split('.');
			Type type = null;
			for (int i = 0; i < hostSplit.Length - 1; i++) {
				type = Assembly.GetExecutingAssembly().GetType(ns +	".SiteHelper_" +
					String.Join("_", hostSplit, i, hostSplit.Length - i));
				if (type != null) break;
			}
			if (type == null) type = typeof(SiteHelper);
			return (SiteHelper)Activator.CreateInstance(type);
		}

		public void SetURL(string url) {
			_url = url;
		}

		public void SetHTML(string html) {
			_html = html;
		}

		protected string[] SplitURL() {
			int pos = _url.IndexOf("://");
			if (pos == -1) return new string[0];
			return _url.Substring(pos + 3).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
		}

		public virtual string GetSiteName() {
			string[] hostSplit = (new Uri(_url)).Host.Split('.');
			return (hostSplit.Length >= 2) ? hostSplit[hostSplit.Length - 2] : String.Empty;
		}

		public virtual string GetBoardName() {
			string[] urlSplit = SplitURL();
			return (urlSplit.Length > 2) ? urlSplit[1] : String.Empty;
		}
			
		public virtual string GetThreadName() {
			string[] urlSplit = SplitURL();
			if (urlSplit.Length >= 3) {
				string page = urlSplit[urlSplit.Length - 1];
				int pos = page.IndexOf('?');
				if (pos != -1) page = page.Substring(0, pos);
				pos = page.LastIndexOf('.');
				if (pos != -1) page = page.Substring(0, pos);
				return page;
			}
			return String.Empty;
		}

		public virtual List<ImageInfo> GetImages() {
			var filenames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			List<ImageInfo> images = new List<ImageInfo>();
			ElementInfo elem;
			int offset = 0;
			string value;
			int pos;

			while ((elem = General.FindElement(_html, "a", offset)) != null) {
				offset = elem.Offset + 1;
				value = General.GetAttributeValue(elem, "href");
				if (String.IsNullOrEmpty(value) || value.IndexOf("/src/", StringComparison.OrdinalIgnoreCase) == -1) {
					continue;
				}

				ImageInfo image = new ImageInfo();

				image.URL = General.ProperURL(_url, value);
				if (image.URL == null) continue;
				pos = Math.Max(
					image.URL.LastIndexOf("http://", StringComparison.OrdinalIgnoreCase),
					image.URL.LastIndexOf("https://", StringComparison.OrdinalIgnoreCase));
				if (pos == -1) {
					image.Referer = _url;
				}
				else {
					image.Referer = image.URL;
					image.URL = image.URL.Substring(pos);
				}
				if (filenames.ContainsKey(image.FileName)) continue;

				images.Add(image);
				filenames.Add(image.FileName, 0);
			}

			return images;
		}

		public virtual string GetNextPageURL() {
			return null;
		}
	}

	public class SiteHelper_4chan_org : SiteHelper {
		public override List<ImageInfo> GetImages() {
			List<ImageInfo> images = new List<ImageInfo>();
			ElementInfo elem;
			int offset = 0;
			string value;

			while ((elem = General.FindElement(_html, "span", offset)) != null) {
				offset = elem.Offset + 1;
				value = General.GetAttributeValue(elem, "class");
				if (value == null || !String.Equals(value, "filesize", StringComparison.OrdinalIgnoreCase)) {
					continue;
				}

				int postEnd = General.FindElementClose(_html, "blockquote", elem.Offset + 1);
				if (postEnd == -1) {
					break;
				}
				offset = postEnd + 1;

				ImageInfo image = new ImageInfo();

				elem = General.FindElement(_html, "a", elem.Offset + 1, postEnd);
				if (elem == null) continue;
				value = General.GetAttributeValue(elem, "href");
				if (String.IsNullOrEmpty(value)) continue;
				image.URL = General.ProperURL(_url, value);
				if (image.URL == null) continue;
				image.Referer = _url;

				elem = General.FindElement(_html, "span", elem.Offset + 1, postEnd);
				if (elem == null) continue;
				value = General.GetAttributeValue(elem, "title");
				if (String.IsNullOrEmpty(value)) continue;
				image.OriginalFileName = Path.GetFileNameWithoutExtension(value);

				elem = General.FindElement(_html, "img", elem.Offset + 1, postEnd);
				if (elem == null) continue;
				value = General.GetAttributeValue(elem, "md5");
				if (String.IsNullOrEmpty(value)) continue;
				try {
					image.Hash = Convert.FromBase64String(value);
				}
				catch { continue; }
				image.HashType = HashType.MD5;

				images.Add(image);
			}

			return images;
		}
	}

}
