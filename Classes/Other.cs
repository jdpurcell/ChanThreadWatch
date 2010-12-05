using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace ChanThreadWatch {
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
				return General.CleanFileName(General.URLFileName(URL));
			}
		}
	}

	public class ThumbnailInfo {
		public string URL { get; set; }
		public string Referer { get; set; }

		public string FileName {
			get {
				return General.CleanFileName(General.URLFileName(URL));
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

	public static class TickCount {
		static object _sync = new object();
		static int _lastTickCount = Environment.TickCount;
		static long _correction;

		public static long Now {
			get {
				lock (_sync) {
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

	public class ConnectionManager {
		private const int _maxConnectionsPerHost = 4;

		private static Dictionary<string, ConnectionManager> _connectionManagers = new Dictionary<string, ConnectionManager>(StringComparer.OrdinalIgnoreCase);

		private Stack<Guid> _groupNames;
		private FIFOSemaphore _semaphore;

		public ConnectionManager() {
			_semaphore = new FIFOSemaphore(_maxConnectionsPerHost, _maxConnectionsPerHost);
			_groupNames = new Stack<Guid>();
		}

		public static ConnectionManager GetInstance(string url) {
			string host = (new Uri(url)).Host;
			ConnectionManager manager;
			lock (_connectionManagers) {
				if (!_connectionManagers.TryGetValue(host, out manager)) {
					manager = new ConnectionManager();
					_connectionManagers[host] = manager;
				}
			}
			return manager;
		}

		public string ObtainConnection() {
			return ObtainConnection(false);
		}

		private string ObtainConnection(bool alreadyInSemaphore) {
			if (!alreadyInSemaphore) {
				_semaphore.WaitOne();
			}
			lock (_groupNames) {
				if (_groupNames.Count != 0) {
					return _groupNames.Pop().ToString();
				}
				else {
					return Guid.NewGuid().ToString();
				}
			}
		}

		public void ReleaseConnection(string name) {
			lock (_groupNames) {
				_groupNames.Push(new Guid(name));
			}
			_semaphore.Release();
		}

		public string SwapForFreshConnection(string name, string url) {
			ServicePoint servicePoint = ServicePointManager.FindServicePoint(new Uri(url));
			servicePoint.CloseConnectionGroup(name);
			return ObtainConnection(true);
		}
	}

	public class FIFOSemaphore {
		private int _currentCount;
		private int _maximumCount;
		private object _mainSync = new object();
		private Queue<object> _queueSyncs = new Queue<object>();

		public FIFOSemaphore(int initialCount, int maximumCount) {
			if (initialCount > maximumCount) {
				throw new ArgumentException();
			}
			if (initialCount < 0 || maximumCount < 1) {
				throw new ArgumentOutOfRangeException();
			}
			_currentCount = initialCount;
			_maximumCount = maximumCount;
		}

		public void WaitOne() {
			object queueSync = null;
			lock (_mainSync) {
				if (_currentCount > 0) {
					_currentCount--;
					return;
				}
				else {
					queueSync = new object();
					_queueSyncs.Enqueue(queueSync);
				}
			}
			lock (queueSync) {
				Monitor.Wait(queueSync);
			}
		}

		public void Release() {
			lock (_mainSync) {
				if (_currentCount >= _maximumCount) {
					throw new SemaphoreFullException();
				}
				if (_queueSyncs.Count == 0) {
					_currentCount++;
				}
				else {
					object queueSync = _queueSyncs.Dequeue();
					lock (queueSync) {
						Monitor.Pulse(queueSync);
					}
				}
			}
		}
	}

	public class HashSet<T> : IEnumerable<T> {
		Dictionary<T, int> _dict;

		public HashSet() {
			_dict = new Dictionary<T, int>();
		}

		public HashSet(IEqualityComparer<T> comparer) {
			_dict = new Dictionary<T, int>(comparer);
		}

		public int Count {
			get { return _dict.Count; }
		}

		public bool Add(T item) {
			if (!_dict.ContainsKey(item)) {
				_dict[item] = 0;
				return false;
			}
			else {
				return true;
			}
		}

		public bool Remove(T item) {
			return _dict.Remove(item);
		}

		public void Clear() {
			_dict.Clear();
		}

		public bool Contains(T item) {
			return _dict.ContainsKey(item);
		}

		public IEnumerator<T> GetEnumerator() {
			foreach (KeyValuePair<T, int> item in _dict) {
				yield return item.Key;
			}
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
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

	public class DownloadStatusEventArgs : EventArgs {
		public DownloadType DownloadType { get; private set; }
		public int CompleteCount { get; private set; }
		public int TotalCount { get; private set; }

		public DownloadStatusEventArgs(DownloadType downloadType, int completeCount, int totalCount) {
			DownloadType = downloadType;
			CompleteCount = completeCount;
			TotalCount = totalCount;
		}
	}

	public class StopStatusEventArgs : EventArgs {
		public StopReason StopReason { get; private set; }

		public StopStatusEventArgs(StopReason stopReason) {
			StopReason = stopReason;
		}
	}

	public delegate void EventHandler<TSender, TArgs>(TSender sender, TArgs e) where TArgs : EventArgs;

	public delegate void DownloadFileEndCallback(bool completed);

	public delegate void DownloadPageEndCallback(bool completed, string content, DateTime? lastModifiedTime, Encoding encoding, List<ReplaceInfo> replaceList);

	public delegate void Action();

	public delegate void Action<T1, T2>(T1 arg1, T2 arg2);

	public delegate void Action<T1, T2, T3>(T1 arg1, T2 arg2, T3 arg3);

	public delegate void Action<T1, T2, T3, T4>(T1 arg1, T2 arg2, T3 arg3, T4 arg4);

	public delegate TResult Func<TResult>();

	public delegate TResult Func<T, TResult>(T arg);

	public delegate TResult Func<T1, T2, TResult>(T1 arg1, T2 arg2);

	public delegate TResult Func<T1, T2, T3, TResult>(T1 arg1, T2 arg2, T3 arg3);

	public delegate TResult Func<T1, T2, T3, T4, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4);

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

	public enum DownloadType {
		Page = 1,
		Image = 2,
		Thumbnail = 3
	}

	public enum StopReason {
		Other = 0,
		UserRequest = 1,
		Exiting = 2,
		PageNotFound = 3,
		DownloadComplete = 4,
		IOError = 5
	}
}
