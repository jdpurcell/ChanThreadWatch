using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Web;

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

		public virtual List<ImageInfo> GetImages(List<ReplaceInfo> replaceList, List<ThumbnailInfo> thumbnailList) {
			var fileNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			List<ImageInfo> images = new List<ImageInfo>();
			ElementInfo elem;
			int offset = 0;
			AttributeInfo attr;
			string url;
			int pos;

			while ((elem = General.FindElement(_html, "a", offset)) != null) {
				offset = elem.Offset + 1;
				attr = General.GetAttribute(elem, "href");
				if (attr == null || String.IsNullOrEmpty(attr.Value)) continue;
				url = General.ProperURL(_url, HttpUtility.HtmlDecode(attr.Value));
				if (url == null || url.IndexOf("/src/", StringComparison.OrdinalIgnoreCase) == -1) continue;

				int linkEnd = General.FindElementClose(_html, "a", elem.Offset + 1);
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

				elem = General.FindElement(_html, "img", elem.Offset + 1, linkEnd);
				if (elem != null) {
					attr = General.GetAttribute(elem, "src");
					if (attr != null && !String.IsNullOrEmpty(attr.Value)) {
						url = General.ProperURL(_url, HttpUtility.HtmlDecode(attr.Value));
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
										Tag = thumb.FileNameWithExt
									});
							}
						}
					}
				}

				if (!fileNames.ContainsKey(image.FileName)) {
					images.Add(image);
					fileNames.Add(image.FileName, 0);
				}
				if (thumb != null) {
					thumbnailList.Add(thumb);
				}
			}

			return images;
		}

		public virtual string GetNextPageURL() {
			return null;
		}
	}

	public class SiteHelper_4chan_org : SiteHelper {
		public override List<ImageInfo> GetImages(List<ReplaceInfo> replaceList, List<ThumbnailInfo> thumbnailList) {
			List<ImageInfo> images = new List<ImageInfo>();
			ElementInfo elem;
			AttributeInfo attr;
			int offset = 0;
			string value;

			while ((elem = General.FindElement(_html, "span", offset)) != null) {
				offset = elem.Offset + 1;
				value = General.GetAttributeValue(elem, "class");
				if (value == null || !String.Equals(value, "filesize", StringComparison.OrdinalIgnoreCase)) {
					continue;
				}

				int postEnd = General.FindElementClose(_html, "blockquote", elem.Offset + 1);
				if (postEnd == -1) break;
				offset = postEnd + 1;

				ImageInfo image = new ImageInfo();
				ThumbnailInfo thumb = new ThumbnailInfo();

				elem = General.FindElement(_html, "a", elem.Offset + 1, postEnd);
				if (elem == null) continue;
				attr = General.GetAttribute(elem, "href");
				if (attr == null || String.IsNullOrEmpty(attr.Value)) continue;
				image.URL = General.ProperURL(_url, HttpUtility.HtmlDecode(attr.Value));
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

				elem = General.FindElement(_html, "span", elem.Offset + 1, postEnd);
				if (elem == null) continue;
				value = General.GetAttributeValue(elem, "title");
				if (String.IsNullOrEmpty(value)) continue;
				image.OriginalFileName = Path.GetFileNameWithoutExtension(General.CleanFileName(HttpUtility.HtmlDecode(value)));

				elem = General.FindElement(_html, "a", elem.Offset + 1, postEnd);
				if (elem == null) continue;
				attr = General.GetAttribute(elem, "href");
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

				elem = General.FindElement(_html, "img", elem.Offset + 1, postEnd);
				if (elem == null) continue;
				attr = General.GetAttribute(elem, "src");
				if (attr == null || String.IsNullOrEmpty(attr.Value)) continue;
				thumb.URL = General.ProperURL(_url, HttpUtility.HtmlDecode(attr.Value));
				if (thumb.URL == null) continue;
				thumb.Referer = _url;
				if (replaceList != null) {
					replaceList.Add(
						new ReplaceInfo {
							Offset = attr.Offset,
							Length = attr.Length,
							Type = ReplaceType.ImageSrc,
							Tag = thumb.FileNameWithExt
						});
				}
				value = General.GetAttributeValue(elem, "md5");
				if (String.IsNullOrEmpty(value)) continue;
				try {
					image.Hash = Convert.FromBase64String(value);
				}
				catch { continue; }
				image.HashType = HashType.MD5;

				images.Add(image);
				thumbnailList.Add(thumb);
			}

			return images;
		}
	}
}
