using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;

namespace JDP {
	public class SiteHelper {
		private static readonly Dictionary<string, Type> _siteHelpersByHost;

		protected string Url { get; private set; }

		protected Uri Uri { get; private set; }

		protected string[] UrlPathComponents { get; private set; }

		protected HtmlParser Parser { get; private set; }

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

		public static SiteHelper CreateByHost(string host) {
			List<string> hostSplit = host.Split('.').ToList();
			while (hostSplit.Count > 0) {
				if (_siteHelpersByHost.TryGetValue(String.Join(".", hostSplit), out Type type)) {
					return (SiteHelper)Activator.CreateInstance(type);
				}
				hostSplit.RemoveAt(0);
			}
			return new SiteHelper();
		}

		public static SiteHelper CreateByUrl(string url) {
			SiteHelper siteHelper = CreateByHost(new Uri(url).Host);
			siteHelper.SetUrl(url);
			return siteHelper;
		}

		public void SetUrl(string url) {
			Url = url;
			Uri = new Uri(url);
			UrlPathComponents = General.GetUrlPathComponents(Uri);
		}

		public void SetHtmlParser(HtmlParser htmlParser) {
			Parser = htmlParser;
		}

		public virtual string GetSiteName() {
			string[] hostSplit = Uri.Host.Split('.');
			return hostSplit.Length >= 2 ? hostSplit[hostSplit.Length - 2] : "";
		}

		public virtual string GetBoardName() {
			return UrlPathComponents.Length >= 2 ? UrlPathComponents[0] : "";
		}

		public virtual string GetThreadName() {
			if (UrlPathComponents.Length >= 2) {
				string page = UrlPathComponents[UrlPathComponents.Length - 1];
				int pos = page.LastIndexOf('.');
				if (pos != -1) page = page.Substring(0, pos);
				return page;
			}
			return "";
		}

		public string GetGlobalThreadID() {
			return $"{GetSiteName()}_{GetBoardName()}_{GetThreadName()}";
		}

		public virtual bool IsBoardHighTurnover() {
			return false;
		}

		protected virtual string ImageUrlKeyword {
			get { return "/src/"; }
		}

		public virtual List<ImageInfo> GetImages(List<ReplaceInfo> replaceList, List<ThumbnailInfo> thumbnailList) {
			HashSet<string> imageFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			HashSet<string> thumbnailFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			List<ImageInfo> imageList = new List<ImageInfo>();
			HtmlAttribute attribute;
			string url;
			int pos;

			foreach (HtmlTag linkTag in Parser.FindStartTags("a")) {
				attribute = linkTag.GetAttribute("href");
				if (attribute == null) continue;
				url = General.GetAbsoluteUrl(Uri, HttpUtility.HtmlDecode(attribute.Value));
				if (url == null || url.IndexOf(ImageUrlKeyword, StringComparison.OrdinalIgnoreCase) == -1) continue;

				HtmlTag linkEndTag = Parser.FindCorrespondingEndTag(linkTag);
				if (linkEndTag == null) continue;

				ImageInfo image = new ImageInfo();
				ThumbnailInfo thumb = null;

				image.Url = url;
				if (image.Url == null || image.FileName.Length == 0) continue;
				pos = Math.Max(
					image.Url.LastIndexOf("http://", StringComparison.OrdinalIgnoreCase),
					image.Url.LastIndexOf("https://", StringComparison.OrdinalIgnoreCase));
				if (pos == -1) {
					image.Referer = Url;
				}
				else {
					image.Referer = image.Url;
					image.Url = image.Url.Substring(pos);
				}
				replaceList?.Add(
					new ReplaceInfo {
						Offset = attribute.Offset,
						Length = attribute.Length,
						Type = ReplaceType.ImageLinkHref,
						Tag = image.FileName
					});

				HtmlTag imageTag = Parser.FindStartTag(linkTag, linkEndTag, "img");
				if (imageTag != null) {
					attribute = imageTag.GetAttribute("src");
					if (attribute != null) {
						url = General.GetAbsoluteUrl(Uri, HttpUtility.HtmlDecode(attribute.Value));
						if (url != null) {
							thumb = new ThumbnailInfo();
							thumb.Url = url;
							thumb.Referer = Url;
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

		public virtual string GetNextPageUrl() {
			return null;
		}
	}
}
