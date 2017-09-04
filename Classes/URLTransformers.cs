using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JDP {
	public abstract class URLTransformer {
		private static List<URLTransformer> _urlTransformers;

		static URLTransformer() {
			_urlTransformers =
				(from t in Assembly.GetExecutingAssembly().GetTypes()
				 where t.IsSubclassOf(typeof(URLTransformer))
				 select (URLTransformer)Activator.CreateInstance(t)).ToList();
		}

		public static string Transform(string url, string auth) {
			return _urlTransformers.Select(n => n.TransformIfRecognized(url, auth)).FirstOrDefault(n => n != null) ?? url;
		}

		public abstract string TransformIfRecognized(string url, string auth);
	}

	public class URLTransformer_TwitchVOD : URLTransformer {
		public override string TransformIfRecognized(string url, string auth) {
			long videoID = TryParseVideoIDFromURL(url) ?? 0;
			if (videoID == 0) {
				return null;
			}
			JsonVodAccessToken accessToken = JObject.Parse(General.DownloadPageToString($"{"https"}://api.twitch.tv/api/vods/{videoID}/access_token", withRequest: AddTwitchAPIHeaders)).ToObject<JsonVodAccessToken>();
			string[] masterPlaylistLines = General.NormalizeNewLines(General.DownloadPageToString($"{"https"}://usher.ttvnw.net/vod/{videoID}?allow_source=true&allow_audio_only=true&allow_spectre=true&player=twitchweb&nauth={Uri.EscapeUriString(accessToken.Token)}&nauthsig={Uri.EscapeUriString(accessToken.Sig)}")).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
			return GetPreferredPlaylistFromMasterPlaylist(masterPlaylistLines);
		}

		private static string GetPreferredPlaylistFromMasterPlaylist(string[] lines) {
			bool IsMetaDataForStreamName(string line, string name) {
				if (!line.StartsWith("#EXT-X-MEDIA:", StringComparison.OrdinalIgnoreCase)) return false;
				string contents = "," + line.SubstringAfterFirst(":", StringComparison.Ordinal) + ",";
				return contents.IndexOf($",NAME=\"{name}\",", StringComparison.OrdinalIgnoreCase) != -1;
			}
			bool IsComment(string line) =>
				line.StartsWith("#", StringComparison.Ordinal);

			string playlist;

			// Try to find the "Source" playlist
			playlist = lines.SkipWhile(l => !IsMetaDataForStreamName(l, "Source")).FirstOrDefault(l => !IsComment(l));
			if (playlist != null) return playlist;

			// Otherwise, just get the first playlist
			playlist = lines.FirstOrDefault(l => !IsComment(l));
			if (playlist != null) return playlist;

			throw new Exception("Unable to find a suitable playlist.");
		}

		private static long? TryParseVideoIDFromURL(string s) {
			string[] hosts = { "twitch.tv", "www.twitch.tv" };
			if (!s.StartsWith("http", StringComparison.OrdinalIgnoreCase)) {
				s = "https://" + s;
			}
			if (!Uri.TryCreate(s, UriKind.Absolute, out Uri uri)) return null;
			if (!hosts.Any(h => uri.Host.Equals(h, StringComparison.OrdinalIgnoreCase))) return null;
			if (!uri.AbsolutePath.StartsWith("/videos/", StringComparison.Ordinal)) return null;
			return Int64.TryParse(uri.AbsolutePath.Substring(8), out long id) ? id : (long?)null;
		}

		private static void AddTwitchAPIHeaders(HttpWebRequest request) {
			request.Accept = "application/vnd.twitchtv.v5+json";
			request.Headers.Add("Client-ID", "jzkbprff40iqj646a697cyrvl0zt2m6");
		}

		private class JsonVodAccessToken {
			[JsonProperty("token")]
			public string Token { get; set; }
			[JsonProperty("sig")]
			public string Sig { get; set; }
		}
	}
}
