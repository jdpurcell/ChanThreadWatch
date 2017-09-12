﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace JDP {
	public static class Settings {
		private const string _appName = "Chan Thread Watch";

		private static readonly Dictionary<string, string> _settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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

		public static bool? UseExeDirectoryForSettings { get; set; }

		public static string ExeDirectory {
			get {
				return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			}
		}

		public static string AppDataDirectory {
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

		public static string GetSettingsDirectory() {
			if (UseExeDirectoryForSettings == null) {
				UseExeDirectoryForSettings = File.Exists(Path.Combine(ExeDirectory, SettingsFileName));
			}
			return GetSettingsDirectory(UseExeDirectoryForSettings.Value);
		}

		public static string GetSettingsDirectory(bool useExeDirForSettings) {
			if (useExeDirForSettings) {
				return ExeDirectory;
			}
			else {
				string dir = AppDataDirectory;
				if (!Directory.Exists(dir)) {
					Directory.CreateDirectory(dir);
				}
				return dir;
			}
		}

		public static string AbsoluteDownloadDirectory {
			get {
				string dir = DownloadFolder;
				if (!String.IsNullOrEmpty(dir) && (DownloadFolderIsRelative == true)) {
					dir = General.GetAbsoluteDirectoryPath(dir, ExeDirectory);
				}
				return dir;
			}
		}

		private static string Get(string name) {
			lock (_settings) {
				return _settings.TryGetValue(name, out string value) ? value : null;
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
			return Int32.TryParse(value, out int x) ? x : (int?)null;
		}

		private static DateTime? GetDate(string name) {
			string value = Get(name);
			if (value == null) return null;
			return DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture,
				DateTimeStyles.None, out DateTime x) ? x : (DateTime?)null;
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
			string path = Path.Combine(GetSettingsDirectory(), SettingsFileName);

			_settings.Clear();

			if (!File.Exists(path)) {
				return;
			}

			using (StreamReader sr = File.OpenText(path)) {
				string line;

				while ((line = sr.ReadLine()) != null) {
					int pos = line.IndexOf('=');

					if (pos != -1) {
						string name = line.Substring(0, pos);
						string val = line.Substring(pos + 1);

						if (!_settings.ContainsKey(name)) {
							_settings.Add(name, val);
						}
					}
				}
			}
		}

		public static void Save() {
			string path = Path.Combine(GetSettingsDirectory(), SettingsFileName);
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
