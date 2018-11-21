﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;

namespace JDP {
	public class SiteHelper {
		private static readonly Dictionary<string, Type> _siteHelpersByHost;

		protected string URL { get; private set; }

		protected Uri URI { get; private set; }

		protected string[] URLPathComponents { get; private set; }

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

		public static SiteHelper CreateByURL(string url) {
			SiteHelper siteHelper = CreateByHost(new Uri(url).Host);
			siteHelper.SetURL(url);
			return siteHelper;
		}

		public void SetURL(string url) {
			URL = url;
			URI = new Uri(url);
			URLPathComponents = General.GetURLPathComponents(URI);
		}

		public void SetHTMLParser(HTMLParser htmlParser) {
			Parser = htmlParser;
		}

		public virtual string GetSiteName() {
			string[] hostSplit = URI.Host.Split('.');
			return hostSplit.Length >= 2 ? hostSplit[hostSplit.Length - 2] : "";
		}

		public virtual string GetBoardName() {
			return URLPathComponents.Length >= 2 ? URLPathComponents[0] : "";
		}

		public virtual string GetThreadName() {
			if (URLPathComponents.Length >= 2) {
				string page = URLPathComponents[URLPathComponents.Length - 1];
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
				url = General.GetAbsoluteURL(URI, HttpUtility.HtmlDecode(attribute.Value));
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
						url = General.GetAbsoluteURL(URI, HttpUtility.HtmlDecode(attribute.Value));
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
			"4chan.org",
			"4channel.org"
		};

		public override string GetThreadName() {
			if (URLPathComponents.Length >= 3 && URLPathComponents[1].Equals("thread", StringComparison.Ordinal)) {
				return URLPathComponents[2];
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
					URL = General.GetAbsoluteURL(URI, HttpUtility.HtmlDecode(imageURL)),
					Referer = URL,
					UnsanitizedOriginalFileName = HttpUtility.HtmlDecode(originalFileName),
					HashType = HashType.MD5,
					Hash = General.TryBase64Decode(imageMD5)
				};
				if (image.URL.Length == 0 || image.FileName.Length == 0 || image.Hash == null) continue;

				ThumbnailInfo thumb = new ThumbnailInfo {
					URL = General.GetAbsoluteURL(URI, HttpUtility.HtmlDecode(thumbURL)),
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

	public class SiteHelper_TwitchVOD : SiteHelper, IFilePostprocessor {
		public static string[] Hosts { get; } = {
			"twitch.tv",
			"hls.ttvnw.net",
			"akamaized.net",
		};

		public override string GetSiteName() {
			return "Twitch";
		}

		public override string GetBoardName() {
			return URLPathComponents.Length >= 5 ? "Upload" : "VOD";
		}

		public override string GetThreadName() {
			if (URLPathComponents.Length >= 5) {
				// For uploads, we're returning the video ID.
				return URLPathComponents[1];
			}
			else if (URLPathComponents.Length >= 1) {
				string[] s = URLPathComponents[0].Split('_');
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

			// We aren't really returning the "original" filenames; we just need to assign
			// filenames that ensure correct chronological order when sorted.
			return files.Select((f, i) => new ImageInfo {
				URL = General.GetAbsoluteURL(URI, f.FileName),
				UnsanitizedOriginalFileName = $"{i:D6}.ts",
				ForceOriginalFileName = true
			}).ToList();
		}

		public void PostprocessFiles(string downloadDirectory, ProgressReporter onProgress) {
			List<string> files =
				(from path in Directory.GetFiles(downloadDirectory, "*.ts")
				 let name = Path.GetFileNameWithoutExtension(path)
				 where name.All(Char.IsDigit)
				 let sequence = Int32.Parse(name)
				 orderby sequence
				 select path).ToList();
			if (files.Count == 0) return;
			using (FileStream dst = File.Create(Path.Combine(downloadDirectory, "stream.ts"))) {
				for (int iFile = 0; iFile < files.Count; iFile++) {
					onProgress((double)iFile / files.Count);
					using (FileStream src = File.OpenRead(files[iFile])) {
						src.CopyTo(dst);
					}
				}
			}
			foreach (string path in files) {
				try { File.Delete(path); } catch { }
			}
		}
	}

	public interface IFilePostprocessor {
		void PostprocessFiles(string downloadDirectory, ProgressReporter onProgress);
	}
}
