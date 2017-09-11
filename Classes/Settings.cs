using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace JDP {
	public static class Settings {
		private const string _appName = "CTW Classic";
		private const int _currentFileVersion = 1;

		private static readonly object _sync = new object();
		private static SettingsData _settings;

		public static string SettingsFileName => "settings.json";

		public static string ThreadsFileName => "threads.json";

		public static string ExeDirectory { get; } = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

		public static string AppDataDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), _appName);

		public static bool UseExeDirectoryForSettings { get; set; } = File.Exists(Path.Combine(ExeDirectory, SettingsFileName));

		public static string GetSettingsDirectory() {
			return GetSettingsDirectory(UseExeDirectoryForSettings);
		}

		public static string GetSettingsDirectory(bool useExeDirForSettings) {
			if (useExeDirForSettings) {
				return ExeDirectory;
			}
			else {
				string dir = AppDataDirectory;
				Directory.CreateDirectory(dir);
				return dir;
			}
		}

		private static void EnsureLoaded() {
			if (_settings == null) {
				throw new Exception("Settings have not been loaded.");
			}
		}

		public static void Load() {
			string path = Path.Combine(GetSettingsDirectory(), SettingsFileName);
			SettingsData settings;

			if (File.Exists(path)) {
				settings = JsonConvert.DeserializeObject<SettingsData>(File.ReadAllText(path));
				if (settings.FileVersion > _currentFileVersion) {
					throw new Exception("Settings file was created with a newer version of this program.");
				}
				settings.FileVersion = _currentFileVersion;
			}
			else {
				settings = new SettingsData { FileVersion = _currentFileVersion };
			}

			lock (_sync) {
				_settings = settings;
			}
		}

		public static void Save() {
			EnsureLoaded();
			string path = Path.Combine(GetSettingsDirectory(), SettingsFileName);
			string contents;
			lock (_sync) {
				contents = JsonConvert.SerializeObject(_settings, Formatting.Indented);
			}
			File.WriteAllText(path, contents);
		}

		private static T Get<T>(Func<SettingsData, T> func) {
			lock (_sync) {
				EnsureLoaded();
				return func(_settings);
			}
		}

		private static void Set(Action<SettingsData> action) {
			lock (_sync) {
				EnsureLoaded();
				action(_settings);
			}
		}

		public static bool UseCustomUserAgent {
			get => Get(s => s.UseCustomUserAgent);
			set => Set(s => s.UseCustomUserAgent = value);
		}

		public static string CustomUserAgent {
			get => Get(s => s.CustomUserAgent);
			set => Set(s => s.CustomUserAgent = value);
		}

		public static bool UsePageAuth {
			get => Get(s => s.UsePageAuth);
			set => Set(s => s.UsePageAuth = value);
		}

		public static string PageAuth {
			get => Get(s => s.PageAuth);
			set => Set(s => s.PageAuth = value);
		}

		public static bool UseImageAuth {
			get => Get(s => s.UseImageAuth);
			set => Set(s => s.UseImageAuth = value);
		}

		public static string ImageAuth {
			get => Get(s => s.ImageAuth);
			set => Set(s => s.ImageAuth = value);
		}

		public static bool OneTimeDownload {
			get => Get(s => s.OneTimeDownload);
			set => Set(s => s.OneTimeDownload = value);
		}

		public static int CheckEvery {
			get => Get(s => s.CheckEvery);
			set => Set(s => s.CheckEvery = value);
		}

		public static ThreadDoubleClickAction ThreadDoubleClickAction {
			get => Get(s => s.ThreadDoubleClickAction);
			set => Set(s => s.ThreadDoubleClickAction = value);
		}

		public static bool DownloadFolderIsRelative {
			get => Get(s => s.DownloadFolderIsRelative);
			set => Set(s => s.DownloadFolderIsRelative = value);
		}

		public static string DownloadFolder {
			get => Get(s => s.DownloadFolder);
			set => Set(s => s.DownloadFolder = value);
		}

		public static DownloadFolderNamingMethod DownloadFolderNamingMethod {
			get => Get(s => s.DownloadFolderNamingMethod);
			set => Set(s => s.DownloadFolderNamingMethod = value);
		}

		public static bool SaveThumbnails {
			get => Get(s => s.SaveThumbnails);
			set => Set(s => s.SaveThumbnails = value);
		}

		public static bool UseOriginalFileNames {
			get => Get(s => s.UseOriginalFileNames);
			set => Set(s => s.UseOriginalFileNames = value);
		}

		public static bool VerifyImageHashes {
			get => Get(s => s.VerifyImageHashes);
			set => Set(s => s.VerifyImageHashes = value);
		}

		public static string AbsoluteDownloadDirectory {
			get {
				string dir = DownloadFolder;
				if (!String.IsNullOrEmpty(dir) && DownloadFolderIsRelative) {
					dir = General.GetAbsoluteDirectoryPath(dir, ExeDirectory);
				}
				return dir;
			}
		}
	}

	public class SettingsData {
		public int FileVersion { get; set; }
		public bool UseCustomUserAgent { get; set; }
		public string CustomUserAgent { get; set; } = "";
		public bool UsePageAuth { get; set; }
		public string PageAuth { get; set; } = "";
		public bool UseImageAuth { get; set; }
		public string ImageAuth { get; set; } = "";
		public bool OneTimeDownload { get; set; }
		public int CheckEvery { get; set; } = 3;
		public ThreadDoubleClickAction ThreadDoubleClickAction { get; set; } = ThreadDoubleClickAction.OpenFolder;
		public bool DownloadFolderIsRelative { get; set; }
		public string DownloadFolder { get; set; } = "";
		public DownloadFolderNamingMethod DownloadFolderNamingMethod { get; set; } = DownloadFolderNamingMethod.GlobalThreadID;
		public bool SaveThumbnails { get; set; } = true;
		public bool UseOriginalFileNames { get; set; } = true;
		public bool VerifyImageHashes { get; set; } = true;
	}
}
