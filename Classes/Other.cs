using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ChanThreadWatch {
	public class WatchInfo {
		public int ListIndex;
		public bool Stop;
		public Thread WatchThread;
		public string PageURL;
		public string PageAuth;
		public string ImageAuth;
		public int WaitSeconds;
		public bool OneTime;
		public string SaveDir;
		public string SaveBaseDir;
		public long NextCheck;
	}

	public class ElementInfo {
		public int Offset;
		public int Length;
		public string Name;
		public List<AttributeInfo> Attributes;
		public string InnerHTML;
	}

	public class AttributeInfo {
		public string Name;
		public string Value;
		public int Offset;
		public int Length;
	}

	public class ReplaceInfo {
		public int Offset;
		public int Length;
		public string Value;
		public ReplaceType Type;
		public string Tag;
	}

	public class PageInfo {
		public string URL;
		public DateTime? CacheTime;
		public bool IsFresh;
		public string Path;
		public Encoding Encoding;
		public List<ReplaceInfo> ReplaceList;
	}

	public class ImageInfo {
		public string URL { get; set; }
		public string Referer { get; set; }
		public string OriginalFileName { get; set; }
		public HashType HashType { get; set; }
		public byte[] Hash { get; set; }

		public string FileName {
			get {
				return Path.GetFileNameWithoutExtension(General.CleanFilename(General.URLFilename(URL)));
			}
		}

		public string Extension {
			get {
				return Path.GetExtension(General.CleanFilename(General.URLFilename(URL)));
			}
		}
	}

	public class ThumbnailInfo {
		public string URL { get; set; }
		public string Referer { get; set; }

		public string FileNameWithExt {
			get {
				return General.CleanFilename(General.URLFilename(URL));
			}
		}
	}

	public class HTTP404Exception : Exception {
		public HTTP404Exception() {
		}
	}

	public class HTTP304Exception : Exception {
		public HTTP304Exception() {
		}
	}

	public class SoftLock : IDisposable {
		private object _obj;

		public SoftLock(object obj) {
			_obj = obj;
			while (!Monitor.TryEnter(_obj, 10)) Application.DoEvents();
		}

		public static SoftLock Obtain(object obj) {
			return new SoftLock(obj);
		}

		public void Dispose() {
			Dispose(true);
		}

		private void Dispose(bool disposing) {
			if (_obj != null) {
				Monitor.Exit(_obj);
				_obj = null;
			}
		}
	}

	public static class TickCount {
		static object _synchObj = new object();
		static int _lastTickCount = Environment.TickCount;
		static long _correction;

		public static long Now {
			get {
				lock (_synchObj) {
					int tickCount = Environment.TickCount;
					if ((tickCount < 0) && (_lastTickCount >= 0)) {
						_correction += 0x100000000L;
					}
					_lastTickCount = tickCount;
					return tickCount + _correction;
				}
			}
		}
	}

	public class HashGeneratorStream : Stream {
		HashAlgorithm _hashAlgo;
		byte[] _dataHash;

		public HashGeneratorStream(HashType hashType) {
			switch (hashType) {
				case HashType.MD5:
					_hashAlgo = new MD5CryptoServiceProvider();
					break;
				default:
					throw new Exception("Unsupported hash type.");
			}
		}

		public override bool CanRead {
			get { return false; }
		}

		public override bool CanSeek {
			get { return false; }
		}

		public override bool CanWrite {
			get { return true; }
		}

		public override void Write(byte[] buffer, int offset, int count) {
			if (_hashAlgo == null) {
				throw new Exception("Cannot write after hash has been finalized.");
			}
			_hashAlgo.TransformBlock(buffer, offset, count, null, 0);
		}

		public override void Flush() {
		}

		public byte[] GetDataHash() {
			if (_hashAlgo != null) {
				_hashAlgo.TransformFinalBlock(new byte[0], 0, 0);
				_dataHash = _hashAlgo.Hash;
				_hashAlgo = null;
			}
			return _dataHash;
		}

		public override long Length {
			get { throw new NotSupportedException(); }
		}

		public override long Position {
			get { throw new NotSupportedException(); }
			set { throw new NotSupportedException(); }
		}

		public override int Read(byte[] buffer, int offset, int count) {
			throw new NotSupportedException();
		}

		public override long Seek(long offset, SeekOrigin origin) {
			throw new NotSupportedException();
		}

		public override void SetLength(long value) {
			throw new NotSupportedException();
		}
	}

	public delegate string WatchInfoSelector(WatchInfo watchInfo);

	public enum ThreadDoubleClickAction {
		OpenFolder = 1,
		OpenURL = 2
	}

	public enum HashType {
		None = 0,
		MD5 = 1
	}

	public enum ReplaceType {
		Other = 0,
		NewLine = 1,
		ImageLinkHref = 2,
		ImageSrc = 3,
		MetaContentType = 4
	}
}
