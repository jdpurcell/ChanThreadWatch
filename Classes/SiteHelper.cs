using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;

namespace JDP {
	public class SiteHelper {
		private static readonly List<(Type Type, Func<Uri, bool> CanHandle)> _siteHelpers;

		protected string Url { get; private set; }

		protected Uri Uri { get; private set; }

		protected string[] UrlPathComponents { get; private set; }

		protected ThreadWatcher Watcher { get; private set; }

		protected HtmlParser Parser { get; private set; }

		static SiteHelper() {
			_siteHelpers =
				(from t in Assembly.GetExecutingAssembly().GetTypes()
				 where t.IsSubclassOf(typeof(SiteHelper))
				 select (
					 Type: t,
					 CanHandle: (Func<Uri, bool>)t.GetMethod("CanHandle", BindingFlags.Static | BindingFlags.Public).CreateDelegate(typeof(Func<Uri, bool>))
				 )).ToList();
		}

		public static SiteHelper CreateByUrl(string url) {
			Uri uri = new Uri(url);
			Type helperType = _siteHelpers.Where(h => h.CanHandle(uri)).Select(h => h.Type).FirstOrDefault();
			SiteHelper helper = helperType != null ? (SiteHelper)Activator.CreateInstance(helperType) : new SiteHelper();
			helper.SetUrl(url);
			return helper;
		}

		public static bool IsMatchByHost(Uri uri, string[] hosts) {
			string uriHost = uri.Host;
			return hosts.Any(h => uriHost.Equals(h, StringComparison.OrdinalIgnoreCase) || uriHost.EndsWith("." + h, StringComparison.OrdinalIgnoreCase));
		}

		public static bool IsMatchByExtension(Uri uri, string[] extensions) {
			string uriPath = uri.AbsolutePath;
			return extensions.Any(e => uriPath.EndsWith(e, StringComparison.OrdinalIgnoreCase));
		}

		public void SetUrl(string url) {
			Url = url;
			Uri = new Uri(url);
			UrlPathComponents = General.GetUrlPathComponents(Uri);
		}

		public void SetParameters(ThreadWatcher watcher, HtmlParser htmlParser) {
			Watcher = watcher;
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
			string siteName = GetSiteName();
			string boardName = GetBoardName();
			string threadName = GetThreadName();
			return !String.IsNullOrEmpty(boardName) ? $"{siteName}_{boardName}_{threadName}" : $"{siteName}_{threadName}";
		}

		public virtual string GetDefaultDescription() {
			return null;
		}

		public virtual int MinCheckIntervalSeconds =>
			30;

		protected virtual string ImageUrlKeyword =>
			"/src/";

		public virtual GetFilesResult GetFiles(List<ReplaceInfo> replaceList) {
			GetFilesResult result = new GetFilesResult();
			HashSet<string> imageFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			HashSet<string> thumbnailFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
					result.Images.Add(image);
					imageFileNames.Add(image.FileName);
				}
				if (thumb != null && !thumbnailFileNames.Contains(thumb.FileName)) {
					result.Thumbnails.Add(thumb);
					thumbnailFileNames.Add(thumb.FileName);
				}
			}

			return result;
		}

		public virtual string GetNextPageUrl() {
			return null;
		}
	}

	public interface IFilePostprocessor {
		void PostprocessFiles(ThreadWatcher watcher, ProgressReporter onProgress);
	}
}
