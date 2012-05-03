using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Web;

namespace ChanThreadWatch {
	public class SiteHelper {
		protected string _url = String.Empty;
		protected string _html = String.Empty;

		public static SiteHelper GetInstance(string host) {
			Type type = null;
			try {
				string ns = (typeof(SiteHelper)).Namespace;
				string[] hostSplit = host.ToLower(CultureInfo.InvariantCulture).Split('.');
				for (int i = 0; i < hostSplit.Length - 1; i++) {
					type = Assembly.GetExecutingAssembly().GetType(ns +	".SiteHelper_" +
						String.Join("_", hostSplit, i, hostSplit.Length - i));
					if (type != null) break;
				}
			}
			catch { }
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
			return (urlSplit.Length >= 3) ? urlSplit[1] : String.Empty;
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

		public virtual bool IsBoardHighTurnover() {
			return false;
		}

		protected virtual string ImageURLKeyword {
			get { return "/src/"; }
		}

		public virtual List<ImageInfo> GetImages(List<ReplaceInfo> replaceList, List<ThumbnailInfo> thumbnailList) {
			HashSet<string> imageFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			HashSet<string> thumbnailFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			List<ImageInfo> imageList = new List<ImageInfo>();
			ElementInfo elem;
			int offset = 0;
			AttributeInfo attr;
			string url;
			int pos;

			while ((elem = General.FindElement(_html, offset, "a")) != null) {
				offset = elem.Offset + 1;
				attr = elem.GetAttribute("href");
				if (attr == null || String.IsNullOrEmpty(attr.Value)) continue;
				url = General.GetAbsoluteURL(_url, HttpUtility.HtmlDecode(attr.Value));
				if (url == null || url.IndexOf(ImageURLKeyword, StringComparison.OrdinalIgnoreCase) == -1) continue;

				int linkEnd = General.FindElementClose(_html, elem.Offset + 1, "a");
				if (linkEnd == -1) break;

				ImageInfo image = new ImageInfo();
				ThumbnailInfo thumb = null;

				image.URL = url;
				if (image.URL == null || image.FileName.Length == 0) continue;
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
				if (replaceList != null) {
					replaceList.Add(
						new ReplaceInfo {
							Offset = attr.Offset,
							Length = attr.Length,
							Type = ReplaceType.ImageLinkHref,
							Tag = image.FileName
						});
				}

				elem = General.FindElement(_html, elem.Offset + 1, linkEnd, "img");
				if (elem != null) {
					attr = elem.GetAttribute("src");
					if (attr != null && !String.IsNullOrEmpty(attr.Value)) {
						url = General.GetAbsoluteURL(_url, HttpUtility.HtmlDecode(attr.Value));
						if (url != null) {
							thumb = new ThumbnailInfo();
							thumb.URL = url;
							thumb.Referer = _url;
							if (replaceList != null) {
								replaceList.Add(
									new ReplaceInfo {
										Offset = attr.Offset,
										Length = attr.Length,
										Type = ReplaceType.ImageSrc,
										Tag = thumb.FileName
									});
							}
						}
					}
				}

				if (!imageFileNames.Contains(image.FileName)) {
					imageList.Add(image);
					imageFileNames.Add(image.FileName);
				}
				if (thumb != null && !thumbnailFileNames.Contains(thumb.FileName)) {
					thumbnailList.Add(thumb);
					thumbnailFileNames.Add(thumb.FileName);
				}
			}

			return imageList;
		}

		public virtual string GetNextPageURL() {
			return null;
		}
	}

	public class SiteHelper_4chan_org : SiteHelper {
		public override List<ImageInfo> GetImages(List<ReplaceInfo> replaceList, List<ThumbnailInfo> thumbnailList) {
			List<ImageInfo> imageList = new List<ImageInfo>();
			ElementInfo elem;
			AttributeInfo attr;
			int offset = 0;
			string value;

			while ((elem = General.FindElement(_html, offset, "span", "div")) != null) {
				offset = elem.Offset + 1;
				value = elem.GetAttributeValue("class");
				if (value == null) continue;
				bool isNewHTML = elem.Name.Equals("div", StringComparison.OrdinalIgnoreCase) &&
					value.Equals("fileInfo", StringComparison.OrdinalIgnoreCase);
				if (!isNewHTML) {
					bool isOldHTML = elem.Name.Equals("span", StringComparison.OrdinalIgnoreCase) &&
						value.Equals("filesize", StringComparison.OrdinalIgnoreCase);
					if (!isOldHTML) continue;
				}

				int postEnd = General.FindElementClose(_html, elem.Offset + 1, "blockquote");
				if (postEnd == -1) break;
				offset = postEnd + 1;

				ImageInfo image = new ImageInfo();
				ThumbnailInfo thumb = new ThumbnailInfo();

				elem = General.FindElement(_html, elem.Offset + 1, postEnd, "a");
				if (elem == null) continue;
				attr = elem.GetAttribute("href");
				if (attr == null || String.IsNullOrEmpty(attr.Value)) continue;
				image.URL = General.GetAbsoluteURL(_url, HttpUtility.HtmlDecode(attr.Value));
				if (image.URL == null || image.FileName.Length == 0) continue;
				image.Referer = _url;
				if (replaceList != null) {
					replaceList.Add(
						new ReplaceInfo {
							Offset = attr.Offset,
							Length = attr.Length,
							Type = ReplaceType.ImageLinkHref,
							Tag = image.FileName
						});
				}

				elem = General.FindElement(_html, elem.Offset + 1, postEnd, "span");
				if (elem == null) continue;
				value = elem.GetAttributeValue("title");
				if (String.IsNullOrEmpty(value)) continue;
				image.OriginalFileName = General.CleanFileName(HttpUtility.HtmlDecode(value));

				elem = General.FindElement(_html, elem.Offset + 1, postEnd, "a");
				if (elem == null) continue;
				attr = elem.GetAttribute("href");
				if (attr == null || String.IsNullOrEmpty(attr.Value)) continue;
				if (replaceList != null) {
					replaceList.Add(
						new ReplaceInfo {
							Offset = attr.Offset,
							Length = attr.Length,
							Type = ReplaceType.ImageLinkHref,
							Tag = image.FileName
						});
				}

				elem = General.FindElement(_html, elem.Offset + 1, postEnd, "img");
				if (elem == null) continue;
				attr = elem.GetAttribute("src");
				if (attr == null || String.IsNullOrEmpty(attr.Value)) continue;
				thumb.URL = General.GetAbsoluteURL(_url, HttpUtility.HtmlDecode(attr.Value));
				if (thumb.URL == null) continue;
				thumb.Referer = _url;
				if (replaceList != null) {
					replaceList.Add(
						new ReplaceInfo {
							Offset = attr.Offset,
							Length = attr.Length,
							Type = ReplaceType.ImageSrc,
							Tag = thumb.FileName
						});
				}
				value = elem.GetAttributeValue(isNewHTML ? "data-md5" : "md5");
				if (String.IsNullOrEmpty(value)) continue;
				try {
					image.Hash = Convert.FromBase64String(value);
				}
				catch { continue; }
				image.HashType = HashType.MD5;

				imageList.Add(image);
				thumbnailList.Add(thumb);
			}

			return imageList;
		}

		public override bool IsBoardHighTurnover() {
			return String.Equals(GetBoardName(), "b", StringComparison.OrdinalIgnoreCase);
		}
	}

	public class SiteHelper_krautchan_net : SiteHelper {
		public override string GetThreadName() {
			string threadName = base.GetThreadName();
			if (threadName.StartsWith("thread-", StringComparison.OrdinalIgnoreCase)) {
				threadName = threadName.Substring(7);
			}
			return threadName;
		}

		protected override string ImageURLKeyword {
			get { return "/files/"; }
		}
	}
}
