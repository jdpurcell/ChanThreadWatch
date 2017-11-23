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
			Uri uri = new Uri(url);
			return _urlTransformers.Select(n => n.TransformIfRecognized(uri, auth)).FirstOrDefault(n => n != null) ?? url;
		}

		public abstract string TransformIfRecognized(Uri uri, string auth);
	}

	public class URLTransformer_4chan : URLTransformer {
		public override string TransformIfRecognized(Uri uri, string auth) {
			if (!uri.Host.Equals("boards.4chan.org", StringComparison.OrdinalIgnoreCase)) return null;
			string[] pathComponents = General.GetURLPathComponents(uri);
			if (pathComponents.Length < 3) return null;
			return General.GetAbsoluteURL(uri, "/" + String.Join("/", pathComponents.Take(3)));
		}
	}

	public class URLTransformer_TwitchVOD : URLTransformer {
		public override string TransformIfRecognized(Uri uri, string auth) {
			long videoID = TryParseVideoIDFromURL(uri) ?? 0;
			if (videoID == 0) {
				return null;
			}
			JsonVodAccessToken accessToken = JObject.Parse(General.DownloadPageToString($"{"https"}://api.twitch.tv/api/vods/{videoID}/access_token", withRequest: AddTwitchAPIHeaders)).ToObject<JsonVodAccessToken>();
			string[] masterPlaylistLines = General.NormalizeNewLines(General.DownloadPageToString($"{"https"}://usher.ttvnw.net/vod/{videoID}?allow_source=true&allow_audio_only=true&allow_spectre=true&player=twitchweb&nauth={Uri.EscapeUriString(accessToken.Token)}&nauthsig={Uri.EscapeUriString(accessToken.Sig)}")).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
			Uri playlistURI = new Uri(GetPreferredPlaylistFromMasterPlaylist(masterPlaylistLines));
			// Use of one of the akamaized.net hosts - they are better for live steams because
			// the playlists on twitch.tv and ttvnw.net have some kind of caching or delay.
			// Also if the VOD gets muted, the unmuted segments are still available from
			// akamaized.net (for 24 hours I believe) but not the other hosts. Once muted, the
			// playlist is named like index-muted-XXXXXXXXXX.m3u8, whereas the original
			// playlist is named index-dvr.m3u8.
			return $"https://vod{new Random().Next(1, 200):D3}-ttvnw.akamaized.net{playlistURI.PathAndQuery}";
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

		private static long? TryParseVideoIDFromURL(Uri uri) {
			string[] hosts = { "twitch.tv", "www.twitch.tv" };
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
