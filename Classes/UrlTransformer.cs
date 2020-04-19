using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace JDP {
	public abstract class UrlTransformer {
		private static List<UrlTransformer> _urlTransformers;

		static UrlTransformer() {
			_urlTransformers =
				(from t in Assembly.GetExecutingAssembly().GetTypes()
				 where t.IsSubclassOf(typeof(UrlTransformer))
				 select (UrlTransformer)Activator.CreateInstance(t)).ToList();
		}

		public abstract UrlTransformResult TransformIfRecognized(Uri uri, string auth);

		public static UrlTransformResult TransformIfRecognized(string url, string auth) {
			Uri uri = new Uri(url);
			return _urlTransformers.Select(n => n.TransformIfRecognized(uri, auth)).FirstOrDefault(n => n != null);
		}
	}

	public class UrlTransformResult {
		public string TransformedUrl { get; }
		public string DefaultDescription { get; set; }

		public UrlTransformResult(string transformedUrl) {
			TransformedUrl = transformedUrl;
		}
	}
}
