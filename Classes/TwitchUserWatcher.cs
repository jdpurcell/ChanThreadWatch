using System;
using System.Net;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JDP {
	public class TwitchUserWatcher {
		private readonly Timer _timer;
		private readonly int _userID;
		private readonly TimeSpan _interval;
		private long _lastVideoID;

		public TwitchUserWatcher(string userName, TimeSpan interval) {
			_timer = new Timer(TimerCallback);
			_userID = UserNameToID(userName);
			_interval = interval;
			_lastVideoID = GetCurrentVideoID(_userID);
		}

		public event EventHandler<TwitchUserWatcher, TwitchNewVODEventArgs> NewVOD;

		private void OnNewVOD(TwitchNewVODEventArgs e) {
			try { NewVOD?.Invoke(this, e); } catch { }
		}

		private void TimerCallback(object state) {
			try {
				long currentVideoID;
				try {
					currentVideoID = GetCurrentVideoID(_userID);
				}
				catch {
					return;
				}

				if (currentVideoID <= _lastVideoID) return;

				OnNewVOD(new TwitchNewVODEventArgs("https://www.twitch.tv/videos/" + currentVideoID));
				_lastVideoID = currentVideoID;
			}
			finally {
				Start();
			}
		}

		public void Start() {
			_timer.Change(_interval, Timeout.InfiniteTimeSpan);
		}

		private static int UserNameToID(string userName) {
			return JObject.Parse(General.DownloadPageToString($"{"https"}://api.twitch.tv/kraken/users?login={userName}", withRequest: AddTwitchAPIHeaders)).ToObject<JsonUsers>().Users[0].ID;
		}

		private static long GetCurrentVideoID(int userID) {
			return Int64.Parse(JObject.Parse(General.DownloadPageToString($"{"https"}://api.twitch.tv/kraken/channels/{userID}/videos?limit=1", withRequest: AddTwitchAPIHeaders)).ToObject<JsonVideos>().Videos[0].ID.Substring(1));
		}

		private static void AddTwitchAPIHeaders(HttpWebRequest request) {
			request.Accept = "application/vnd.twitchtv.v5+json";
			request.Headers.Add("Client-ID", "jzkbprff40iqj646a697cyrvl0zt2m6");
		}

		private class JsonUsers {
			[JsonProperty("users")]
			public JsonUserDetail[] Users { get; set; }
		}

		private class JsonUserDetail {
			[JsonProperty("_id")]
			public int ID { get; set; }
		}

		private class JsonVideos {
			[JsonProperty("videos")]
			public JsonVideoDetail[] Videos { get; set; }
		}

		private class JsonVideoDetail {
			[JsonProperty("_id")]
			public string ID { get; set; }
		}
	}

	public class TwitchNewVODEventArgs : EventArgs {
		public string URL { get; }

		public TwitchNewVODEventArgs(string url) {
			URL = url;
		}
	}
}
