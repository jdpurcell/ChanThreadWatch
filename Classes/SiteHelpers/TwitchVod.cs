using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace JDP {
	public class SiteHelper_TwitchVod : SiteHelper, IFilePostprocessor {
		public static string[] Hosts { get; } = {
			"twitch.tv",
			"hls.ttvnw.net",
			"akamaized.net"
		};

		public override string GetSiteName() {
			return "Twitch";
		}

		public override string GetBoardName() {
			return UrlPathComponents.Length >= 5 ? "Upload" : "VOD";
		}

		public override string GetThreadName() {
			if (UrlPathComponents.Length >= 5) {
				// For uploads, we're returning the video ID.
				return UrlPathComponents[1];
			}
			else if (UrlPathComponents.Length >= 1) {
				string[] s = UrlPathComponents[0].Split('_');
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
				from line in Parser.PreprocessedHtml.Split('\n').Select(l => l.Trim())
				where line.Length != 0 &&
					  !line.StartsWith("#", StringComparison.Ordinal)
				select new {
					FileName = line
				};

			// We aren't really returning the "original" filenames; we just need to assign
			// filenames that ensure correct chronological order when sorted.
			return files.Select((f, i) => new ImageInfo {
				Url = General.GetAbsoluteUrl(Uri, f.FileName),
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
