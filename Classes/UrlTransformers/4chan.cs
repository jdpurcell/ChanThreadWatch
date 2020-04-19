using System;
using System.Linq;

namespace JDP{
	public class UrlTransformer_4chan : UrlTransformer {
		public override UrlTransformResult TransformIfRecognized(Uri uri, string auth) {
			if (!uri.Host.Equals("boards.4chan.org", StringComparison.OrdinalIgnoreCase)) return null;
			string[] pathComponents = General.GetUrlPathComponents(uri);
			if (pathComponents.Length < 3) return null;
			return new UrlTransformResult(General.GetAbsoluteUrl(uri, "/" + String.Join("/", pathComponents.Take(3))));
		}
	}
}
