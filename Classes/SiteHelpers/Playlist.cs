using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace JDP {
	public class SiteHelper_Playlist : SiteHelper, IFilePostprocessor {
		private const string _playlistFilename = "_playlist.m3u8";

		private static readonly string[] _extensions = {
			".m3u",
			".m3u8"
		};

		private HashSet<string> _seenFileNames;

		public static bool CanHandle(Uri uri) =>
			IsMatchByExtension(uri, _extensions);

		public override string GetSiteName() {
			return "Playlist";
		}

		public override string GetBoardName() {
			return null;
		}

		public override string GetThreadName() {
			return General.CalculateSha1(Encoding.UTF8.GetBytes(Uri.AbsoluteUri)).ToHexString(false);
		}

		public override int MinCheckIntervalSeconds =>
			2;

		public override List<ImageInfo> GetImages(List<ReplaceInfo> replaceList, List<ThumbnailInfo> thumbnailList) {
			List<ImageInfo> images =
				(from line in Parser.PreprocessedHtml.Split('\n').Select(l => l.Trim())
				 where line.Length != 0 &&
					   !line.StartsWith("#", StringComparison.Ordinal)
				 let url = General.GetAbsoluteUrl(Uri, line)
				 select new ImageInfo {
					 Url = url,
					 UnsanitizedFileNameCustom = General.CalculateSha1(Encoding.UTF8.GetBytes(General.UrlFileName(url))).ToHexString(false) + ".ts"
				 }).ToList();

			if (images.Count != 0) {
				string playlistPath = Path.Combine(ThreadDir, _playlistFilename);
				_seenFileNames ??= File.Exists(playlistPath) ? File.ReadAllLines(playlistPath).ToHashSet() : new HashSet<string>();
				List<string> newFileNames = images.Select(n => n.FileName).Where(n => !_seenFileNames.Contains(n)).ToList();
				if (newFileNames.Count != 0) {
					File.AppendAllLines(playlistPath, newFileNames);
					_seenFileNames.UnionWith(newFileNames);
				}
			}

			return images;
		}

		public void PostprocessFiles(string downloadDirectory, ProgressReporter onProgress) {
			string playlistPath = Path.Combine(downloadDirectory, _playlistFilename);
			if (!File.Exists(playlistPath)) return;
			List<string> files = File.ReadAllLines(playlistPath).Select(l => Path.Combine(downloadDirectory, l)).ToList();
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
