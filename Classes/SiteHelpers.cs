using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;

namespace JDP {
	public class SiteHelper {
		private static readonly Dictionary<string, Type> _siteHelpersByHost;

		protected string URL { get; private set; }

		protected HTMLParser Parser { get; private set; }

		static SiteHelper() {
			_siteHelpersByHost =
				(from t in Assembly.GetExecutingAssembly().GetTypes()
				 where t.IsSubclassOf(typeof(SiteHelper))
				 let hosts = (string[])t.InvokeMember("Hosts", BindingFlags.GetProperty, null, null, null)
				 from host in hosts
				 select new {
					 Host = host,
					 Type = t
				 }).ToDictionary(n => n.Host, n => n.Type, StringComparer.OrdinalIgnoreCase);
		}

		public static SiteHelper GetInstance(string host) {
			List<string> hostSplit = host.Split('.').ToList();
			while (hostSplit.Count > 0) {
				if (_siteHelpersByHost.TryGetValue(String.Join(".", hostSplit), out Type type)) {
					return (SiteHelper)Activator.CreateInstance(type);
				}
				hostSplit.RemoveAt(0);
			}
			return new SiteHelper();
		}

		public void SetURL(string url) {
			URL = url;
		}

		public void SetHTMLParser(HTMLParser htmlParser) {
			Parser = htmlParser;
		}

		protected string[] SplitURL() {
			int pos = URL.IndexOf("://", StringComparison.Ordinal);
			if (pos == -1) return new string[0];
			return URL.Substring(pos + 3).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
		}

		public virtual string GetSiteName() {
			string[] hostSplit = (new Uri(URL)).Host.Split('.');
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

			foreach (HTMLTag linkTag in Parser.FindStartTags("a")) {
				attribute = linkTag.GetAttribute("href");
				if (attribute == null) continue;
				url = General.GetAbsoluteURL(URL, HttpUtility.HtmlDecode(attribute.Value));
				if (url == null || url.IndexOf(ImageURLKeyword, StringComparison.OrdinalIgnoreCase) == -1) continue;

				HTMLTag linkEndTag = Parser.FindCorrespondingEndTag(linkTag);
				if (linkEndTag == null) continue;

				ImageInfo image = new ImageInfo();
				ThumbnailInfo thumb = null;

				image.URL = url;
				if (image.URL == null || image.FileName.Length == 0) continue;
				pos = Math.Max(
					image.URL.LastIndexOf("http://", StringComparison.OrdinalIgnoreCase),
					image.URL.LastIndexOf("https://", StringComparison.OrdinalIgnoreCase));
				if (pos == -1) {
					image.Referer = URL;
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

				HTMLTag imageTag = Parser.FindStartTag(linkTag, linkEndTag, "img");
				if (imageTag != null) {
					attribute = imageTag.GetAttribute("src");
					if (attribute != null) {
						url = General.GetAbsoluteURL(URL, HttpUtility.HtmlDecode(attribute.Value));
						if (url != null) {
							thumb = new ThumbnailInfo();
							thumb.URL = url;
							thumb.Referer = URL;
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

	public class SiteHelper_4chan : SiteHelper {
		public static string[] Hosts { get; } = {
			"4chan.org"
		};

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

			foreach (HTMLTagRange postTagRange in Parser.FindStartTags("div").Where(t => HTMLParser.ClassAttributeValueHas(t, "post"))
				.Select(t => Parser.CreateTagRange(t)).Where(r => r != null))
			{
				HTMLTagRange fileTextDivTagRange = Parser.CreateTagRange(Parser.FindStartTags(postTagRange, "div")
					.Where(t => HTMLParser.ClassAttributeValueHas(t, "fileText")).FirstOrDefault());
				if (fileTextDivTagRange == null) continue;

				HTMLTagRange fileThumbLinkTagRange = Parser.CreateTagRange(Parser.FindStartTags(postTagRange, "a")
					.Where(t => HTMLParser.ClassAttributeValueHas(t, "fileThumb")).FirstOrDefault());
				if (fileThumbLinkTagRange == null) continue;

				HTMLTag fileTextLinkStartTag = Parser.FindStartTag(fileTextDivTagRange, "a");
				if (fileTextLinkStartTag == null) continue;

				HTMLTag fileThumbImageTag = Parser.FindStartTag(fileThumbLinkTagRange, "img");
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
						HTMLTagRange fileTextLinkTagRange = Parser.CreateTagRange(fileTextLinkStartTag);
						if (fileTextLinkTagRange == null) continue;
						originalFileName = Parser.GetInnerHTML(fileTextLinkTagRange);
					}
				}
				if (originalFileName == null) continue;

				string imageMD5 = fileThumbImageTag.GetAttributeValue("data-md5");
				if (imageMD5 == null) continue;

				ImageInfo image = new ImageInfo {
					URL = General.GetAbsoluteURL(URL, HttpUtility.HtmlDecode(imageURL)),
					Referer = URL,
					OriginalFileName = General.CleanFileName(HttpUtility.HtmlDecode(originalFileName)),
					HashType = HashType.MD5,
					Hash = General.TryBase64Decode(imageMD5)
				};
				if (image.URL.Length == 0 || image.FileName.Length == 0 || image.Hash == null) continue;

				ThumbnailInfo thumb = new ThumbnailInfo {
					URL = General.GetAbsoluteURL(URL, HttpUtility.HtmlDecode(thumbURL)),
					Referer = URL
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

	public class SiteHelper_Krautchan : SiteHelper {
		public static string[] Hosts { get; } = {
			"krautchan.net"
		};

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

	public class SiteHelper_TwitchVOD : SiteHelper {
		public static string[] Hosts { get; } = {
			"twitch.tv",
			"hls.ttvnw.net",
			"akamaized.net",
		};

		public override string GetBoardName() {
			return "TwitchVOD";
		}

		public override string GetThreadName() {
			string[] urlSplit = SplitURL();
			if (urlSplit.Length >= 2) {
				string[] s = urlSplit[1].Split('_');
				// Can have more than 4 items if the channel name contains an underscore. The item
				// we're returning is what Twitch refers to as "broadcast_id".
				if (s.Length >= 4) {
					return s[s.Length - 2];
				}
			}
			return "";
		}

		public override List<ImageInfo> GetImages(List<ReplaceInfo> replaceList, List<ThumbnailInfo> thumbnailList) {
			var files =
				from line in Parser.PreprocessedHTML.Split('\n').Select(l => l.Trim())
				where line.Length != 0 &&
					  !line.StartsWith("#", StringComparison.Ordinal)
				select new {
					FileName = line
				};

			return files.Select((f, i) => new ImageInfo {
				URL = General.GetAbsoluteURL(URL, f.FileName),
				RequiredFileName = i.ToString("D6") + ".ts"
			}).ToList();
		}
	}
}
