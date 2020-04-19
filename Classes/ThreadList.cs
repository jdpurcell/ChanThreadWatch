using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace JDP {
	public static class ThreadList {
		private const int _currentFileVersion = 1;

		private static List<ThreadWatcher> _watchers;

		public static IEnumerable<ThreadWatcher> Items {
			get {
				EnsureLoaded();
				foreach (ThreadWatcher watcher in _watchers) {
					yield return watcher;
				}
			}
		}

		private static void EnsureLoaded() {
			if (_watchers == null) {
				throw new Exception("Threads have not been loaded.");
			}
		}

		public static void Load(Action<ThreadWatcher> onThreadLoad) {
			if (_watchers != null) {
				throw new Exception("Threads have already been loaded.");
			}

			_watchers = new List<ThreadWatcher>();

			string path = Path.Combine(Settings.GetSettingsDirectory(), Settings.ThreadsFileName);
			if (!File.Exists(path)) {
				return;
			}

			ThreadListData data = JsonConvert.DeserializeObject<ThreadListData>(File.ReadAllText(path));
			if (data.FileVersion > _currentFileVersion) {
				throw new Exception("Threads file was created with a newer version of this program.");
			}

			foreach (ThreadWatcherConfig config in data.Threads) {
				ThreadWatcher watcher = ThreadWatcher.Create(config);

				onThreadLoad(watcher);

				if (!watcher.IsStopping) {
					watcher.Start();
				}

				_watchers.Add(watcher);
			}
		}

		public static void Save() {
			EnsureLoaded();

			ThreadListData data = new ThreadListData {
				FileVersion = _currentFileVersion,
				Threads = _watchers
					.Select(w => new ThreadWatcherConfig {
						PageUrl = w.PageUrl,
						GlobalThreadID = w.GlobalThreadID,
						AddedOn = w.AddedOn,
						PageAuth = w.PageAuth,
						ImageAuth = w.ImageAuth,
						OneTimeDownload = w.OneTimeDownload,
						CheckIntervalSeconds = w.CheckIntervalSeconds,
						RelativeDownloadDirectory = General.GetRelativeDirectoryPath(w.ThreadDownloadDirectory, w.BaseDownloadDirectory),
						PageBaseFileName = w.PageBaseFileName,
						Description = w.Description,
						LastImageOn = w.LastImageOn,
						StopReason = w.IsStopping && w.StopReason != StopReason.Exiting ? w.StopReason : (StopReason?)null
					}).ToArray()
			};

			string path = Path.Combine(Settings.GetSettingsDirectory(), Settings.ThreadsFileName);
			string contents = JsonConvert.SerializeObject(data, Formatting.Indented);
			File.WriteAllText(path, contents);
		}

		public static void Add(ThreadWatcher watcher) {
			EnsureLoaded();
			_watchers.Add(watcher);
		}

		public static void Remove(ThreadWatcher watcher) {
			EnsureLoaded();
			_watchers.Remove(watcher);
		}
	}

	public class ThreadListData {
		public int FileVersion { get; set; }
		public ThreadWatcherConfig[] Threads { get; set; }
	}

	public class ThreadWatcherConfig {
		public string PageUrl { get; set; }
		public string GlobalThreadID { get; set; }
		public DateTime AddedOn { get; set; }
		public string PageAuth { get; set; }
		public string ImageAuth { get; set; }
		public bool OneTimeDownload { get; set; }
		public int CheckIntervalSeconds { get; set; }
		public string RelativeDownloadDirectory { get; set; }
		public string PageBaseFileName { get; set; }
		public string Description { get; set; }
		public DateTime? LastImageOn { get; set; }
		public StopReason? StopReason { get; set; }
	}
}
