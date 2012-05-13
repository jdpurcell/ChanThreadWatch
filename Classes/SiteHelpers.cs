using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Web;

namespace JDP {
	public class SiteHelper {
		protected string _url = String.Empty;
		protected HTMLParser _htmlParser;

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

		public void SetHTMLParser(HTMLParser htmlParser) {
			_htmlParser = htmlParser;
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
			HTMLAttribute attribute;
			string url;
			int pos;

			foreach (HTMLTag linkTag in _htmlParser.FindStartTags("a")) {
				attribute = linkTag.GetAttribute("href");
				if (attribute == null) continue;
				url = General.GetAbsoluteURL(_url, HttpUtility.HtmlDecode(attribute.Value));
				if (url == null || url.IndexOf(ImageURLKeyword, StringComparison.OrdinalIgnoreCase) == -1) continue;

				HTMLTag linkEndTag = _htmlParser.FindCorrespondingEndTag(linkTag);
				if (linkEndTag == null) continue;

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
							Offset = attribute.Offset,
							Length = attribute.Length,
							Type = ReplaceType.ImageLinkHref,
							Tag = image.FileName
						});
				}

				HTMLTag imageTag = _htmlParser.FindStartTag(linkTag, linkEndTag, "img");
				if (imageTag != null) {
					attribute = imageTag.GetAttribute("src");
					if (attribute != null) {
						url = General.GetAbsoluteURL(_url, HttpUtility.HtmlDecode(attribute.Value));
						if (url != null) {
							thumb = new ThumbnailInfo();
							thumb.URL = url;
							thumb.Referer = _url;
							if (replaceList != null) {
								replaceList.Add(
									new ReplaceInfo {
										Offset = attribute.Offset,
										Length = attribute.Length,
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
			HTMLAttribute attribute;
			string value;

			foreach (HTMLTag fileTextTag in _htmlParser.FindStartTags("span")) {
				value = fileTextTag.GetAttributeValue("class");
				if (value == null) continue;
				bool isNewHTML = HTMLParser.ClassAttributeValueHas(value, "fileText");
				if (!isNewHTML) {
					bool isOldHTML = HTMLParser.ClassAttributeValueHas(value, "filesize");
					if (!isOldHTML) continue;
				}

				HTMLTag commentEndTag = _htmlParser.FindEndTag(fileTextTag, null, "blockquote");
				if (commentEndTag == null) continue;

				HTMLTag fileInfoEndTag = _htmlParser.FindCorrespondingEndTag(fileTextTag, commentEndTag);
				if (fileInfoEndTag == null) continue;

				ImageInfo image = new ImageInfo();
				ThumbnailInfo thumb = new ThumbnailInfo();

				HTMLTag textLinkTag = _htmlParser.FindStartTag(fileTextTag, fileInfoEndTag, "a");
				if (textLinkTag == null) continue;
				attribute = textLinkTag.GetAttribute("href");
				if (attribute == null) continue;
				image.URL = General.GetAbsoluteURL(_url, HttpUtility.HtmlDecode(attribute.Value));
				if (image.URL == null || image.FileName.Length == 0) continue;
				image.Referer = _url;
				if (replaceList != null) {
					replaceList.Add(
						new ReplaceInfo {
							Offset = attribute.Offset,
							Length = attribute.Length,
							Type = ReplaceType.ImageLinkHref,
							Tag = image.FileName
						});
				}

				HTMLTag fileNameTag = _htmlParser.FindStartTag(fileTextTag, fileInfoEndTag, "span");
				if (fileNameTag == null) continue;
				value = fileNameTag.GetAttributeValue("title");
				if (value == null) continue;
				image.OriginalFileName = General.CleanFileName(HttpUtility.HtmlDecode(value));

				HTMLTag imageLinkTag = _htmlParser.FindStartTag(fileInfoEndTag, commentEndTag, "a");
				if (imageLinkTag == null) continue;
				attribute = imageLinkTag.GetAttribute("href");
				if (attribute == null) continue;
				if (replaceList != null) {
					replaceList.Add(
						new ReplaceInfo {
							Offset = attribute.Offset,
							Length = attribute.Length,
							Type = ReplaceType.ImageLinkHref,
							Tag = image.FileName
						});
				}

				HTMLTag imageLinkEndTag = _htmlParser.FindCorrespondingEndTag(imageLinkTag, commentEndTag);
				if (imageLinkEndTag == null) continue;

				HTMLTag imageThumbnailTag = _htmlParser.FindStartTag(imageLinkTag, imageLinkEndTag, "img");
				if (imageThumbnailTag == null) continue;
				attribute = imageThumbnailTag.GetAttribute("src");
				if (attribute == null) continue;
				thumb.URL = General.GetAbsoluteURL(_url, HttpUtility.HtmlDecode(attribute.Value));
				if (thumb.URL == null) continue;
				thumb.Referer = _url;
				if (replaceList != null) {
					replaceList.Add(
						new ReplaceInfo {
							Offset = attribute.Offset,
							Length = attribute.Length,
							Type = ReplaceType.ImageSrc,
							Tag = thumb.FileName
						});
				}
				value = imageThumbnailTag.GetAttributeValue(isNewHTML ? "data-md5" : "md5");
				if (value == null) continue;
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
