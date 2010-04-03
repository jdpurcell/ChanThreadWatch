using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ChanThreadWatch {
	static class Settings {
		const string _appName = "Chan Thread Watch";
		const string _fileName = "settings.txt";

		static string _path;
		static Dictionary<string, string> _settings;

		static Settings() {
			_path = Path.Combine(GetSettingsDir(), _fileName);
		}

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

		public static string DownloadFolder {
			get { return Get("DownloadFolder"); }
			set { Set("DownloadFolder", value); }
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
			string appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			string settingsDir = Path.Combine(appDataDir, _appName);

			if (Directory.Exists(settingsDir) == false) {
				Directory.CreateDirectory(settingsDir);
			}

			return settingsDir;
		}

		private static string Get(string name) {
			string value;
			return _settings.TryGetValue(name, out value) ? value : null;
		}

		private static bool? GetBool(string name) {
			string value = Get(name);
			if (value == null) return null;
			return value == "1";
		}

		private static int? GetInt(string name) {
			string value = Get(name);
			int x;
			return Int32.TryParse(value, out x) ? x : (int?)null;
		}

		private static void Set(string name, string value) {
			if (value == null) {
				_settings.Remove(name);
			}
			else {
				_settings[name] = value;
			}
		}

		private static void SetBool(string name, bool? value) {
			Set(name, value.HasValue ? (value.Value ? "1" : "0") : null);
		}

		private static void SetInt(string name, int? value) {
			Set(name, value.HasValue ? value.Value.ToString() : null);
		}

		public static void Load() {
			_settings = new Dictionary<string, string>();

			if (!File.Exists(_path)) {
				return;
			}

			using (StreamReader sr = new StreamReader(_path, Encoding.UTF8)) {
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
			using (StreamWriter sw = new StreamWriter(_path, false, Encoding.UTF8)) {
				foreach (KeyValuePair<string, string> kvp in _settings) {
					sw.WriteLine(kvp.Key + "=" + kvp.Value);
				}
			}
		}
	}
}
