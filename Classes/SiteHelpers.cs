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
			int pos = _url.IndexOf("://", StringComparison.Ordinal);
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

			foreach (HTMLTag postStartTag in Enumerable.Where(_htmlParser.FindStartTags("div"),
				t => HTMLParser.ClassAttributeValueHas(t, "post")))
			{
				HTMLTag postEndTag = _htmlParser.FindCorrespondingEndTag(postStartTag);
				if (postEndTag == null) continue;

				HTMLTag fileTextSpanStartTag = Enumerable.FirstOrDefault(Enumerable.Where(_htmlParser.FindStartTags(
					postStartTag, postEndTag, "span"), t => HTMLParser.ClassAttributeValueHas(t, "fileText")));
				if (fileTextSpanStartTag == null) continue;
				HTMLTag fileTextSpanEndTag = _htmlParser.FindCorrespondingEndTag(fileTextSpanStartTag);
				if (fileTextSpanEndTag == null) continue;

				HTMLTag fileThumbLinkStartTag = Enumerable.FirstOrDefault(Enumerable.Where(_htmlParser.FindStartTags(
					postStartTag, postEndTag, "a"), t => HTMLParser.ClassAttributeValueHas(t, "fileThumb")));
				if (fileThumbLinkStartTag == null) continue;
				HTMLTag fileThumbLinkEndTag = _htmlParser.FindCorrespondingEndTag(fileThumbLinkStartTag);
				if (fileThumbLinkEndTag == null) continue;

				HTMLTag fileTextLinkStartTag = _htmlParser.FindStartTag(fileTextSpanStartTag, fileTextSpanEndTag, "a");
				if (fileTextLinkStartTag == null) continue;

				HTMLTag fileThumbImageTag = _htmlParser.FindStartTag(fileThumbLinkStartTag, fileThumbLinkEndTag, "img");
				if (fileThumbImageTag == null) continue;

				string imageURL = fileTextLinkStartTag.GetAttributeValue("href");
				if (imageURL == null) continue;

				string thumbURL = fileThumbImageTag.GetAttributeValue("src");
				if (thumbURL == null) continue;

				bool isSpoiler = HTMLParser.ClassAttributeValueHas(fileThumbLinkStartTag, "imgspoiler");

				string originalFileName;
				if (isSpoiler) {
					originalFileName = fileTextSpanStartTag.GetAttributeValue("title");
				}
				else {
					HTMLTag fileNameSpanStartTag = _htmlParser.FindStartTag(fileTextSpanStartTag, fileTextSpanEndTag, "span");
					if (fileNameSpanStartTag == null) continue;
					originalFileName = fileNameSpanStartTag.GetAttributeValue("title");
				}
				if (originalFileName == null) continue;

				string imageMD5 = fileThumbImageTag.GetAttributeValue("data-md5");
				if (imageMD5 == null) continue;

				ImageInfo image = new ImageInfo {
					URL = General.GetAbsoluteURL(_url, HttpUtility.HtmlDecode(imageURL)),
					Referer = _url,
					OriginalFileName = General.CleanFileName(HttpUtility.HtmlDecode(originalFileName)),
					HashType = HashType.MD5,
					Hash = General.TryBase64Decode(imageMD5)
				};
				if (image.URL.Length == 0 || image.FileName.Length == 0 || image.Hash == null) continue;

				ThumbnailInfo thumb = new ThumbnailInfo {
					URL = General.GetAbsoluteURL(_url, HttpUtility.HtmlDecode(thumbURL)),
					Referer = _url
				};
				if (thumb.URL == null || thumb.FileName.Length == 0) continue;

				{
					HTMLAttribute attribute = fileTextLinkStartTag.GetAttribute("href");
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
				}

				{
					HTMLAttribute attribute = fileThumbLinkStartTag.GetAttribute("href");
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
				}

				{
					HTMLAttribute attribute = fileThumbImageTag.GetAttribute("src");
					if (attribute == null) continue;
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
