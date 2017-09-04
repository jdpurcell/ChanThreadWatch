﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Web;

namespace JDP {
	public class SiteHelper {
		protected string _url = "";
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
			return (hostSplit.Length >= 2) ? hostSplit[hostSplit.Length - 2] : "";
		}

		public virtual string GetBoardName() {
			string[] urlSplit = SplitURL();
			return (urlSplit.Length >= 3) ? urlSplit[1] : "";
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
			return "";
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
				replaceList?.Add(
					new ReplaceInfo {
						Offset = attribute.Offset,
						Length = attribute.Length,
						Type = ReplaceType.ImageLinkHref,
						Tag = image.FileName
					});

				HTMLTag imageTag = _htmlParser.FindStartTag(linkTag, linkEndTag, "img");
				if (imageTag != null) {
					attribute = imageTag.GetAttribute("src");
					if (attribute != null) {
						url = General.GetAbsoluteURL(_url, HttpUtility.HtmlDecode(attribute.Value));
						if (url != null) {
							thumb = new ThumbnailInfo();
							thumb.URL = url;
							thumb.Referer = _url;
							replaceList?.Add(
								new ReplaceInfo {
									Offset = attribute.Offset,
									Length = attribute.Length,
									Type = ReplaceType.ImageSrc,
									Tag = thumb.FileName
								});
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
		public override string GetThreadName() {
			string[] urlSplit = SplitURL();
			if (urlSplit.Length >= 4 && urlSplit[2].Equals("thread", StringComparison.Ordinal)) {
				string page = urlSplit[3];
				int pos = page.IndexOf('?');
				if (pos != -1) page = page.Substring(0, pos);
				return page;
			}
			return base.GetThreadName();
		}

		public override List<ImageInfo> GetImages(List<ReplaceInfo> replaceList, List<ThumbnailInfo> thumbnailList) {
			List<ImageInfo> imageList = new List<ImageInfo>();
			bool seenSpoiler = false;

			foreach (HTMLTagRange postTagRange in _htmlParser.FindStartTags("div").Where(t => HTMLParser.ClassAttributeValueHas(t, "post"))
				.Select(t => _htmlParser.CreateTagRange(t)).Where(r => r != null))
			{
				HTMLTagRange fileTextDivTagRange = _htmlParser.CreateTagRange(_htmlParser.FindStartTags(postTagRange, "div")
					.Where(t => HTMLParser.ClassAttributeValueHas(t, "fileText")).FirstOrDefault());
				if (fileTextDivTagRange == null) continue;

				HTMLTagRange fileThumbLinkTagRange = _htmlParser.CreateTagRange(_htmlParser.FindStartTags(postTagRange, "a")
					.Where(t => HTMLParser.ClassAttributeValueHas(t, "fileThumb")).FirstOrDefault());
				if (fileThumbLinkTagRange == null) continue;

				HTMLTag fileTextLinkStartTag = _htmlParser.FindStartTag(fileTextDivTagRange, "a");
				if (fileTextLinkStartTag == null) continue;

				HTMLTag fileThumbImageTag = _htmlParser.FindStartTag(fileThumbLinkTagRange, "img");
				if (fileThumbImageTag == null) continue;

				string imageURL = fileTextLinkStartTag.GetAttributeValue("href");
				if (imageURL == null) continue;

				string thumbURL = fileThumbImageTag.GetAttributeValue("src");
				if (thumbURL == null) continue;

				bool isSpoiler = HTMLParser.ClassAttributeValueHas(fileThumbLinkTagRange.StartTag, "imgspoiler");

				string originalFileName;
				if (isSpoiler) {
					originalFileName = fileTextDivTagRange.StartTag.GetAttributeValue("title");
				}
				else {
					// If the filename is shortened, the original filename is in the title attribute
					originalFileName = fileTextLinkStartTag.GetAttributeValue("title");
					// Otherwise, the link's innerHTML contains the original filename
					if (originalFileName == null) {
						HTMLTagRange fileTextLinkTagRange = _htmlParser.CreateTagRange(fileTextLinkStartTag);
						if (fileTextLinkTagRange == null) continue;
						originalFileName = _htmlParser.GetInnerHTML(fileTextLinkTagRange);
					}
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

				if (replaceList != null) {
					HTMLAttribute attribute;

					attribute = fileTextLinkStartTag.GetAttribute("href");
					if (attribute != null) {
						replaceList.Add(
							new ReplaceInfo {
								Offset = attribute.Offset,
								Length = attribute.Length,
								Type = ReplaceType.ImageLinkHref,
								Tag = image.FileName
							});
					}

					attribute = fileThumbLinkTagRange.StartTag.GetAttribute("href");
					if (attribute != null) {
						replaceList.Add(
							new ReplaceInfo {
								Offset = attribute.Offset,
								Length = attribute.Length,
								Type = ReplaceType.ImageLinkHref,
								Tag = image.FileName
							});
					}

					attribute = fileThumbImageTag.GetAttribute("src");
					if (attribute != null) {
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

				if (!isSpoiler || !seenSpoiler) {
					thumbnailList.Add(thumb);
					if (isSpoiler) seenSpoiler = true;
				}
			}

			return imageList;
		}

		public override bool IsBoardHighTurnover() {
			return String.Equals(GetBoardName(), "b", StringComparison.Ordinal);
		}
	}

	public class SiteHelper_krautchan_net : SiteHelper {
		public override string GetThreadName() {
			string threadName = base.GetThreadName();
			if (threadName.StartsWith("thread-", StringComparison.Ordinal)) {
				threadName = threadName.Substring(7);
			}
			return threadName;
		}

		protected override string ImageURLKeyword {
			get { return "/files/"; }
		}
	}

	public class SiteHelper_twitch_tv : SiteHelper {
		public override string GetBoardName() {
			return "Twitch";
		}

		public override string GetThreadName() {
			return "Default";
		}

		public override List<ImageInfo> GetImages(List<ReplaceInfo> replaceList, List<ThumbnailInfo> thumbnailList) {
			var files =
				from line in _htmlParser.PreprocessedHTML.Split('\n').Select(l => l.Trim())
				where line.Length != 0 &&
					  !line.StartsWith("#", StringComparison.Ordinal)
				select new {
					FileName = line
				};

			return files.Select((f, i) => new ImageInfo {
				URL = General.GetAbsoluteURL(_url, f.FileName),
				OriginalFileName = i.ToString("D6") + ".ts"
			}).ToList();
		}
	}

	public class SiteHelper_akamaized_net : SiteHelper_twitch_tv { }

	public class SiteHelper_ttvnw_net : SiteHelper_twitch_tv { }
}
