using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace JDP {
	public class SiteHelper_Playlist : SiteHelper, IFilePostprocessor {
		private const string _indexFileName = "_index.txt";

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

		public override GetFilesResult GetFiles(List<ReplaceInfo> replaceList) {
			GetFilesResult result = new GetFilesResult();

			result.Images.AddRange(
				from line in Parser.PreprocessedHtml.Split('\n').Select(l => l.Trim())
				where line.Length != 0 &&
					  !line.StartsWith("#", StringComparison.Ordinal)
				let url = General.GetAbsoluteUrl(Uri, line)
				select new ImageInfo {
					Url = url,
					UnsanitizedFileNameCustom = General.CalculateSha1(Encoding.UTF8.GetBytes(line)).ToHexString(false) + ".ts",
					UnsanitizedOriginalFileName = General.UrlFileName(url)
				}
			);

			if (result.Images.Count != 0) {
				string indexPath = Path.Combine(Watcher.ThreadDownloadDirectory, _indexFileName);
				_seenFileNames ??= ReadIndexFile(indexPath).Select(f => f.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
				var newFiles = new List<ImageInfo>();
				foreach (ImageInfo image in result.Images) {
					if (!_seenFileNames.Add(image.FileName)) continue;
					newFiles.Add(image);
				}
				if (newFiles.Count != 0) {
					File.AppendAllLines(indexPath, newFiles.Select(f => $"{f.FileName}\t{f.OriginalFileName}"));
				}
			}

			return result;
		}

		public void PostprocessFiles(ThreadWatcher watcher, ProgressReporter onProgress) {
			string indexPath = Path.Combine(watcher.ThreadDownloadDirectory, _indexFileName);
			IndexEntry[] files = ReadIndexFile(indexPath);
			if (files.Length == 0) return;
			string imageDir = watcher.ImageDownloadDirectory;
			bool useOriginalName = watcher.UseOriginalFileNames;
			int fileNameLengthLimit = ThreadWatcher.GetFileNameLengthLimit(imageDir);
			HashSet<string> processedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			using (FileStream dst = File.Create(Path.Combine(imageDir, "stream.ts"))) {
				for (int iFile = 0; iFile < files.Length; iFile++) {
					onProgress((double)iFile / files.Length);
					IndexEntry file = files[iFile];
					string fileName = ThreadWatcher.GetUniqueFileName(ImageInfo.GetEffectiveFileName(
						file.FileName, file.OriginalFileName, useOriginalName, fileNameLengthLimit), processedFileNames);
					using (FileStream src = File.OpenRead(Path.Combine(imageDir, fileName))) {
						src.CopyTo(dst);
					}
				}
			}
			foreach (string fileName in processedFileNames) {
				string path = Path.Combine(imageDir, fileName);
				try { File.Delete(path); } catch { }
			}
		}

		private static IndexEntry[] ReadIndexFile(string path) {
			if (!File.Exists(path)) {
				return Array.Empty<IndexEntry>();
			}
			return
				(from line in File.ReadAllLines(path)
				 let split = line.Split('\t')
				 select new IndexEntry {
					 FileName = split[0],
					 OriginalFileName = split[1]
				 }).ToArray();
		}

		private class IndexEntry {
			public string FileName { get; set; }
			public string OriginalFileName { get; set; }
		}
	}
}
