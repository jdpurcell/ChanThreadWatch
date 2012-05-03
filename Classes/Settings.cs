using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace ChanThreadWatch {
	public static class Settings {
		private const string _appName = "Chan Thread Watch";

		private static Dictionary<string, string> _settings;

		public static bool? UseCustomUserAgent {
			get { return GetBool("UseCustomUserAgent"); }
			set { SetBool("UseCustomUserAgent", value); }
		}

		public static string CustomUserAgent {
			get { return Get("CustomUserAgent"); }
			set { Set("CustomUserAgent", value); }
		}

		public static bool? UsePageAuth {
			get { return GetBool("UsePageAuth"); }
			set { SetBool("UsePageAuth", value); }
		}

		public static string PageAuth {
			get { return Get("PageAuth"); }
			set { Set("PageAuth", value); }
		}

		public static bool? UseImageAuth {
			get { return GetBool("UseImageAuth"); }
			set { SetBool("UseImageAuth", value); }
		}

		public static string ImageAuth {
			get { return Get("ImageAuth"); }
			set { Set("ImageAuth", value); }
		}

		public static bool? OneTimeDownload {
			get { return GetBool("OneTimeDownload"); }
			set { SetBool("OneTimeDownload", value); }
		}

		public static int? CheckEvery {
			get { return GetInt("CheckEvery"); }
			set { SetInt("CheckEvery", value); }
		}

		public static bool? DownloadFolderIsRelative {
			get { return GetBool("DownloadFolderIsRelative"); }
			set { SetBool("DownloadFolderIsRelative", value); }
		}

		public static string DownloadFolder {
			get { return Get("DownloadFolder"); }
			set { Set("DownloadFolder", value); }
		}

		public static bool? RenameDownloadFolderWithDescription {
			get { return GetBool("RenameDownloadFolderWithDescription"); }
			set { SetBool("RenameDownloadFolderWithDescription", value); }
		}

		public static bool? SaveThumbnails {
			get { return GetBool("SaveThumbnails"); }
			set { SetBool("SaveThumbnails", value); }
		}

		public static bool? UseOriginalFileNames {
			get { return GetBool("UseOriginalFileNames"); }
			set { SetBool("UseOriginalFileNames", value); }
		}

		public static bool? VerifyImageHashes {
			get { return GetBool("VerifyImageHashes"); }
			set { SetBool("VerifyImageHashes", value); }
		}

		public static bool? CheckForUpdates {
			get { return GetBool("CheckForUpdates"); }
			set { SetBool("CheckForUpdates", value); }
		}

		public static DateTime? LastUpdateCheck {
			get { return GetDate("LastUpdateCheck"); }
			set { SetDate("LastUpdateCheck", value); }
		}

		public static string LatestUpdateVersion {
			get { return Get("LatestUpdateVersion"); }
			set { Set("LatestUpdateVersion", value); }
		}

		public static bool? UseExeDirForSettings { get; set; }

		public static string ExeDir {
			get {
				return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			}
		}

		public static string AppDataDir {
			get {
				return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), _appName);
			}
		}

		public static string SettingsFileName {
			get { return "settings.txt"; }
		}

		public static string ThreadsFileName {
			get { return "threads.txt"; }
		}

		public static ThreadDoubleClickAction? OnThreadDoubleClick {
			get {
				int x = GetInt("OnThreadDoubleClick") ?? -1;
				return Enum.IsDefined(typeof(ThreadDoubleClickAction), x) ?
					(ThreadDoubleClickAction?)x : null;
			}
			set {
				SetInt("OnThreadDoubleClick", value.HasValue ? (int?)value.Value : null);
			}
		}

		public static string GetSettingsDir() {
			if (UseExeDirForSettings == null) {
				UseExeDirForSettings = File.Exists(Path.Combine(ExeDir, SettingsFileName));
			}
			return GetSettingsDir(UseExeDirForSettings.Value);
		}

		public static string GetSettingsDir(bool useExeDirForSettings) {
			if (useExeDirForSettings) {
				return ExeDir;
			}
			else {
				string dir = AppDataDir;
				if (!Directory.Exists(dir)) {
					Directory.CreateDirectory(dir);
				}
				return dir;
			}
		}

		public static string AbsoluteDownloadDir {
			get {
				string dir = DownloadFolder;
				if (!String.IsNullOrEmpty(dir) && (DownloadFolderIsRelative == true)) {
					dir = General.GetAbsoluteDirectoryPath(dir, ExeDir);
				}
				return dir;
			}
		}

		private static string Get(string name) {
			lock (_settings) {
				string value;
				return _settings.TryGetValue(name, out value) ? value : null;
			}
		}

		private static bool? GetBool(string name) {
			string value = Get(name);
			if (value == null) return null;
			return value == "1";
		}

		private static int? GetInt(string name) {
			string value = Get(name);
			if (value == null) return null;
			int x;
			return Int32.TryParse(value, out x) ? x : (int?)null;
		}

		private static DateTime? GetDate(string name) {
			string value = Get(name);
			if (value == null) return null;
			DateTime x;
			return DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture,
				DateTimeStyles.None, out x) ? x : (DateTime?)null;
		}

		private static void Set(string name, string value) {
			lock (_settings) {
				if (value == null) {
					_settings.Remove(name);
				}
				else {
					_settings[name] = value;
				}
			}
		}

		private static void SetBool(string name, bool? value) {
			Set(name, value.HasValue ? (value.Value ? "1" : "0") : null);
		}

		private static void SetInt(string name, int? value) {
			Set(name, value.HasValue ? value.Value.ToString() : null);
		}

		private static void SetDate(string name, DateTime? value) {
			Set(name, value.HasValue ? value.Value.ToString("yyyyMMdd") : null);
		}

		public static void Load() {
			string path = Path.Combine(GetSettingsDir(), SettingsFileName);

			_settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			if (!File.Exists(path)) {
				return;
			}

			using (StreamReader sr = File.OpenText(path)) {
				string line, name, val;
				int pos;

				while ((line = sr.ReadLine()) != null) {
					pos = line.IndexOf('=');
					if (pos != -1) {
						name = line.Substring(0, pos);
						val = line.Substring(pos + 1);

						if (!_settings.ContainsKey(name)) {
							_settings.Add(name, val);
						}
					}
				}
			}
		}

		public static void Save() {
			string path = Path.Combine(GetSettingsDir(), SettingsFileName);
			using (StreamWriter sw = File.CreateText(path)) {
				lock (_settings) {
					foreach (KeyValuePair<string, string> kvp in _settings) {
						sw.WriteLine(kvp.Key + "=" + kvp.Value);
					}
				}
			}
		}
	}
}
