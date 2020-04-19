using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace JDP {
	public class SiteHelper_4chan : SiteHelper {
		public static string[] Hosts { get; } = {
			"4chan.org",
			"4channel.org"
		};

		public override string GetThreadName() {
			if (UrlPathComponents.Length >= 3 && UrlPathComponents[1].Equals("thread", StringComparison.Ordinal)) {
				return UrlPathComponents[2];
			}
			return base.GetThreadName();
		}

		public override List<ImageInfo> GetImages(List<ReplaceInfo> replaceList, List<ThumbnailInfo> thumbnailList) {
			List<ImageInfo> imageList = new List<ImageInfo>();
			bool seenSpoiler = false;

			foreach (HtmlTagRange postTagRange in Parser.FindStartTags("div").Where(t => HtmlParser.ClassAttributeValueHas(t, "post"))
				.Select(t => Parser.CreateTagRange(t)).Where(r => r != null))
			{
				HtmlTagRange fileTextDivTagRange = Parser.CreateTagRange(Parser.FindStartTags(postTagRange, "div")
					.Where(t => HtmlParser.ClassAttributeValueHas(t, "fileText")).FirstOrDefault());
				if (fileTextDivTagRange == null) continue;

				HtmlTagRange fileThumbLinkTagRange = Parser.CreateTagRange(Parser.FindStartTags(postTagRange, "a")
					.Where(t => HtmlParser.ClassAttributeValueHas(t, "fileThumb")).FirstOrDefault());
				if (fileThumbLinkTagRange == null) continue;

				HtmlTag fileTextLinkStartTag = Parser.FindStartTag(fileTextDivTagRange, "a");
				if (fileTextLinkStartTag == null) continue;

				HtmlTag fileThumbImageTag = Parser.FindStartTag(fileThumbLinkTagRange, "img");
				if (fileThumbImageTag == null) continue;

				string imageUrl = fileTextLinkStartTag.GetAttributeValue("href");
				if (imageUrl == null) continue;

				string thumbUrl = fileThumbImageTag.GetAttributeValue("src");
				if (thumbUrl == null) continue;

				bool isSpoiler = HtmlParser.ClassAttributeValueHas(fileThumbLinkTagRange.StartTag, "imgspoiler");

				string originalFileName;
				if (isSpoiler) {
					originalFileName = fileTextDivTagRange.StartTag.GetAttributeValue("title");
				}
				else {
					// If the filename is shortened, the original filename is in the title attribute
					originalFileName = fileTextLinkStartTag.GetAttributeValue("title");
					// Otherwise, the link's innerHTML contains the original filename
					if (originalFileName == null) {
						HtmlTagRange fileTextLinkTagRange = Parser.CreateTagRange(fileTextLinkStartTag);
						if (fileTextLinkTagRange == null) continue;
						originalFileName = Parser.GetInnerHtml(fileTextLinkTagRange);
					}
				}
				if (originalFileName == null) continue;

				string imageMD5 = fileThumbImageTag.GetAttributeValue("data-md5");
				if (imageMD5 == null) continue;

				ImageInfo image = new ImageInfo {
					Url = General.GetAbsoluteUrl(Uri, HttpUtility.HtmlDecode(imageUrl)),
					Referer = Url,
					UnsanitizedOriginalFileName = HttpUtility.HtmlDecode(originalFileName),
					HashType = HashType.MD5,
					Hash = General.TryBase64Decode(imageMD5)
				};
				if (image.Url.Length == 0 || image.FileName.Length == 0 || image.Hash == null) continue;

				ThumbnailInfo thumb = new ThumbnailInfo {
					Url = General.GetAbsoluteUrl(Uri, HttpUtility.HtmlDecode(thumbUrl)),
					Referer = Url
				};
				if (thumb.Url == null || thumb.FileName.Length == 0) continue;

				if (replaceList != null) {
					HtmlAttribute attribute;

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
}
