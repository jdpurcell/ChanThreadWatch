// --------------------------------------------------------------------------------
// Copyright (c) J.D. Purcell
//
// Licensed under the MIT License (see LICENSE.txt)
// --------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace JDP {
	public class ReplaceInfo {
		public int Offset { get; set; }
		public int Length { get; set; }
		public string Value { get; set; }
		public ReplaceType Type { get; set; }
		public string Tag { get; set; }
	}

	public class PageInfo {
		public string Url { get; set; }
		public DateTime? CacheTime { get; set; }
		public bool IsFresh { get; set; }
		public string Path { get; set; }
		public Encoding Encoding { get; set; }
		public List<ReplaceInfo> ReplaceList { get; set; }
	}

	public class ImageInfo {
		public string Url { get; set; }
		public string Referer { get; set; }
		public string UnsanitizedOriginalFileName { get; set; }
		public bool ForceOriginalFileName { get; set; }
		public HashType HashType { get; set; }
		public byte[] Hash { get; set; }

		public string UnsanitizedFileName => General.UrlFileName(Url);

		public string FileName => General.CleanFileName(UnsanitizedFileName);

		public string OriginalFileName => General.CleanFileName(UnsanitizedOriginalFileName);
	}

	public class DownloadInfo {
		public string FileName { get; set; }
		public bool Skipped { get; set; }
	}

	public class ThumbnailInfo {
		public string Url { get; set; }
		public string Referer { get; set; }

		public string FileName {
			get {
				return General.CleanFileName(General.UrlFileName(Url));
			}
		}
	}

	public class FilePostprocessingTask {
		public IFilePostprocessor SiteHelper { get; set; }
		public string DownloadDirectory { get; set; }
	}

	public class Http404Exception : Exception {
	}

	public class Http304Exception : Exception {
	}

	public static class TickCount {
		private static readonly object _sync = new object();
		private static int _lastTickCount;
		private static long _correction;

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

		private static readonly Dictionary<string, ConnectionManager> _connectionManagers = new Dictionary<string, ConnectionManager>(StringComparer.OrdinalIgnoreCase);

		private readonly FifoSemaphore _semaphore = new FifoSemaphore(_maxConnectionsPerHost, _maxConnectionsPerHost);
		private readonly Stack<string> _groupNames = new Stack<string>();

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

		public string ObtainConnectionGroupName() {
			_semaphore.WaitOne();
			return GetConnectionGroupName();
		}

		public void ReleaseConnectionGroupName(string name) {
			lock (_groupNames) {
				_groupNames.Push(name);
			}
			_semaphore.Release();
		}

		public string SwapForFreshConnection(string name, string url) {
			ServicePoint servicePoint = ServicePointManager.FindServicePoint(new Uri(url));
			try {
				servicePoint.CloseConnectionGroup(name);
			}
			catch (NotImplementedException) {
				// Workaround for Mono
			}
			return GetConnectionGroupName();
		}

		private string GetConnectionGroupName() {
			lock (_groupNames) {
				return _groupNames.Count != 0 ? _groupNames.Pop() : Guid.NewGuid().ToString();
			}
		}
	}

	public class FifoSemaphore {
		private readonly object _mainSync = new object();
		private readonly Queue<QueueSync> _queueSyncs = new Queue<QueueSync>();
		private int _currentCount;
		private int _maximumCount;

		public FifoSemaphore(int initialCount, int maximumCount) {
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
			WaitOne(Timeout.Infinite);
		}

		public bool WaitOne(int timeout) {
			QueueSync queueSync;
			lock (_mainSync) {
				if (_currentCount > 0) {
					_currentCount--;
					return true;
				}
				else {
					queueSync = new QueueSync();
					_queueSyncs.Enqueue(queueSync);
				}
			}
			lock (queueSync) {
				if (queueSync.IsSignaled || Monitor.Wait(queueSync, timeout)) {
					return true;
				}
				else {
					queueSync.IsAbandoned = true;
					return false;
				}
			}
		}

		public void Release() {
			lock (_mainSync) {
				if (_currentCount >= _maximumCount) {
					throw new SemaphoreFullException();
				}
			CheckQueue:
				if (_queueSyncs.Count == 0) {
					_currentCount++;
				}
				else {
					QueueSync queueSync = _queueSyncs.Dequeue();
					lock (queueSync) {
						if (queueSync.IsAbandoned) {
							goto CheckQueue;
						}
						// Backup signal in case we acquired the lock before the waiter
						queueSync.IsSignaled = true;
						Monitor.Pulse(queueSync);
					}
				}
			}
		}

		private class QueueSync {
			public bool IsSignaled { get; set; }
			public bool IsAbandoned { get; set; }
		}
	}

	public class WorkScheduler {
		private const int _maxThreadIdleTime = 15000;

		private readonly object _sync = new object();
		private readonly LinkedList<WorkItem> _workItems = new LinkedList<WorkItem>();
		private readonly ManualResetEvent _scheduleChanged = new ManualResetEvent(false);
		private Thread _schedulerThread;

		public WorkItem AddItem(long runAtTicks, Action action) {
			return AddItem(runAtTicks, action, "");
		}

		public WorkItem AddItem(long runAtTicks, Action action, string group) {
			WorkItem item = new WorkItem(this, runAtTicks, action, group);
			AddItem(item);
			return item;
		}

		private void AddItem(WorkItem item) {
			lock (_sync) {
				LinkedListNode<WorkItem> nextNode = null;
				foreach (LinkedListNode<WorkItem> node in EnumerateNodes()) {
					if (node.Value.RunAtTicks > item.RunAtTicks) {
						nextNode = node;
						break;
					}
				}
				if (nextNode == null) {
					_workItems.AddLast(item);
				}
				else {
					_workItems.AddBefore(nextNode, item);
				}
				_scheduleChanged.Set();
				if (_schedulerThread == null) {
					_schedulerThread = new Thread(SchedulerThread);
					_schedulerThread.IsBackground = true;
					_schedulerThread.Start();
				}
			}
		}

		public bool RemoveItem(WorkItem item) {
			lock (_sync) {
				if (_workItems.Remove(item)) {
					_scheduleChanged.Set();
					return true;
				}
				else {
					return false;
				}
			}
		}

		private void ReAddItem(WorkItem item) {
			lock (_sync) {
				if (RemoveItem(item)) {
					AddItem(item);
				}
			}
		}

		private void SchedulerThread() {
			while (true) {
				int? firstWaitTime = null;

				lock (_sync) {
					_scheduleChanged.Reset();
					if (_workItems.Count != 0) {
						firstWaitTime = (int)(_workItems.First.Value.RunAtTicks - TickCount.Now);
					}
				}

				if (!(firstWaitTime <= 0)) {
					if (_scheduleChanged.WaitOne(firstWaitTime ?? _maxThreadIdleTime, false)) {
						continue;
					}
					if (firstWaitTime == null) {
						lock (_sync) {
							if (_workItems.Count != 0) {
								continue;
							}
							else {
								_schedulerThread = null;
								return;
							}
						}
					}
				}

				lock (_sync) {
					while (_workItems.Count != 0 && _workItems.First.Value.RunAtTicks <= TickCount.Now) {
						_workItems.First.Value.StartRunning();
						_workItems.RemoveFirst();
					}
				}
			}
		}

		private IEnumerable<LinkedListNode<WorkItem>> EnumerateNodes() {
			LinkedListNode<WorkItem> node = _workItems.First;
			while (node != null) {
				yield return node;
				node = node.Next;
			}
		}

		public class WorkItem {
			private readonly WorkScheduler _scheduler;
			private readonly Action _action;
			private readonly string _group;
			private long _runAtTicks;
			private bool _hasStarted;

			public WorkItem(WorkScheduler scheduler, long runAtTicks, Action action, string group) {
				_scheduler = scheduler;
				_runAtTicks = runAtTicks;
				_action = action;
				_group = group;
			}

			public long RunAtTicks {
				get { lock (_scheduler._sync) { return _runAtTicks; } }
				set {
					lock (_scheduler._sync) {
						if (_hasStarted) return;
						_runAtTicks = value;
						_scheduler.ReAddItem(this);
					}
				}
			}

			public void StartRunning() {
				lock (_scheduler._sync) {
					if (_hasStarted) {
						throw new Exception("Work item has already started.");
					}
					_hasStarted = true;
					ThreadPoolManager.QueueWorkItem(_group, _action);
				}
			}
		}
	}

	public class ThreadPoolManager {
		private const int _minThreadCount = 4;
		private const int _threadCreationDelay = 500;
		private const int _maxThreadIdleTime = 15000;

		private static readonly Dictionary<string, ThreadPoolManager> _threadPoolManagers = new Dictionary<string, ThreadPoolManager>(StringComparer.OrdinalIgnoreCase);

		private readonly object _sync = new object();
		private readonly FifoSemaphore _semaphore = new FifoSemaphore(0, Int32.MaxValue);
		private readonly Stack<ThreadPoolThread> _idleThreads = new Stack<ThreadPoolThread>();
		private readonly ThreadPoolThread _schedulerThread = new ThreadPoolThread(null);

		public ThreadPoolManager() {
			lock (_sync) {
				for (int i = 0; i < _minThreadCount; i++) {
					_idleThreads.Push(new ThreadPoolThread(this));
					_semaphore.Release();
				}
			}
		}

		public static void QueueWorkItem(string group, Action action) {
			ThreadPoolManager manager;
			lock (_threadPoolManagers) {
				if (!_threadPoolManagers.TryGetValue(group, out manager)) {
					manager = new ThreadPoolManager();
					_threadPoolManagers[group] = manager;
				}
			}
			manager.QueueWorkItem(action);
		}

		public void QueueWorkItem(Action action) {
			_schedulerThread.QueueWorkItem(() => {
				ThreadPoolThread thread;
				if (_semaphore.WaitOne(_threadCreationDelay)) {
					lock (_sync) {
						thread = _idleThreads.Pop();
					}
				}
				else {
					thread = new ThreadPoolThread(this);
				}
				thread.QueueWorkItem(action);
				thread.QueueWorkItem(() => {
					lock (_sync) {
						_idleThreads.Push(thread);
						_semaphore.Release();
					}
				});
			});
		}

		private void OnThreadPoolThreadExit(ThreadPoolThread exitedThread) {
			lock (_sync) {
				if (_idleThreads.Count <= _minThreadCount) return;
				Stack<ThreadPoolThread> threads = new Stack<ThreadPoolThread>();
				while (_idleThreads.Count != 0) {
					ThreadPoolThread thread = _idleThreads.Pop();
					if (thread == exitedThread) {
						if (!_semaphore.WaitOne(0)) {
							throw new Exception("Semaphore count is invalid.");
						}
						break;
					}
					threads.Push(thread);
				}
				while (threads.Count != 0) {
					_idleThreads.Push(threads.Pop());
				}
			}
		}

		private class ThreadPoolThread {
			private readonly object _sync = new object();
			private readonly ThreadPoolManager _manager;
			private readonly Queue<Action> _workItems = new Queue<Action>();
			private Thread _thread;
			private ManualResetEvent _newWorkItem;

			internal ThreadPoolThread(ThreadPoolManager manager) {
				_manager = manager;
			}

			internal void QueueWorkItem(Action action) {
				lock (_sync) {
					if (_thread == null) {
						_newWorkItem = new ManualResetEvent(false);
						_thread = new Thread(WorkThread);
						_thread.IsBackground = true;
						_thread.Start();
					}
					_workItems.Enqueue(action);
					_newWorkItem.Set();
				}
			}

			private void WorkThread() {
				while (_newWorkItem.WaitOne(_maxThreadIdleTime, false) || !ReleaseThread()) {
					Action workItem = null;
					lock (_sync) {
						if (_workItems.Count != 0) {
							workItem = _workItems.Dequeue();
						}
						else {
							_newWorkItem.Reset();
						}
					}
					if (workItem != null) {
						Thread.MemoryBarrier();
						workItem();
						Thread.MemoryBarrier();
					}
				}
				if (_manager != null) {
					_manager.OnThreadPoolThreadExit(this);
				}
			}

			private bool ReleaseThread() {
				lock (_sync) {
					if (_workItems.Count == 0) {
						_newWorkItem.Close();
						_newWorkItem = null;
						_thread = null;
						return true;
					}
					else {
						return false;
					}
				}
			}
		}
	}

	public class HashGeneratorStream : Stream {
		private HashAlgorithm _hashAlgo;
		private byte[] _dataHash;

		public HashGeneratorStream(HashType hashType) {
			switch (hashType) {
				case HashType.MD5:
					_hashAlgo = new MD5CryptoServiceProvider();
					break;
				default:
					throw new Exception("Unsupported hash type.");
			}
		}

		public override void Close() {
			if (_hashAlgo != null) {
				try { _hashAlgo.Dispose(); } catch { }
				_hashAlgo = null;
			}
			base.Close();
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
				_hashAlgo.Dispose();
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
		public DownloadType DownloadType { get; }
		public int CompleteCount { get; }
		public int TotalCount { get; }

		public DownloadStatusEventArgs(DownloadType downloadType, int completeCount, int totalCount) {
			DownloadType = downloadType;
			CompleteCount = completeCount;
			TotalCount = totalCount;
		}
	}

	public class StopStatusEventArgs : EventArgs {
		public StopReason StopReason { get; }

		public StopStatusEventArgs(StopReason stopReason) {
			StopReason = stopReason;
		}
	}

	public class DownloadStartEventArgs : EventArgs {
		public long DownloadID { get; }
		public string Url { get; }
		public int TryNumber { get; }
		public long? TotalSize { get; }

		public DownloadStartEventArgs(long downloadID, string url, int tryNumber, long? totalSize) {
			DownloadID = downloadID;
			Url = url;
			TryNumber = tryNumber;
			TotalSize = totalSize;
		}
	}

	public class DownloadProgressEventArgs : EventArgs {
		public long DownloadID { get; }
		public long DownloadedSize { get; }

		public DownloadProgressEventArgs(long downloadID, long downloadedSize) {
			DownloadID = downloadID;
			DownloadedSize = downloadedSize;
		}
	}

	public class DownloadEndEventArgs : EventArgs {
		public long DownloadID { get; }
		public long DownloadedSize { get; }
		public bool IsSuccessful { get; }

		public DownloadEndEventArgs(long downloadID, long downloadedSize, bool isSuccessful) {
			DownloadID = downloadID;
			DownloadedSize = downloadedSize;
			IsSuccessful = isSuccessful;
		}
	}

	public delegate void ProgressReporter(double progress);

	public delegate void EventHandler<TSender, TArgs>(TSender sender, TArgs e) where TArgs : EventArgs;

	public delegate void DownloadFileEndCallback(DownloadResult result);

	public delegate void DownloadPageEndCallback(DownloadResult result, string content, DateTime? lastModifiedTime, Encoding encoding);

	public enum ThreadDoubleClickAction {
		OpenFolder = 1,
		OpenUrl = 2,
		EditDescription = 3
	}

	public enum DownloadFolderNamingMethod {
		GlobalThreadID = 1,
		Description = 2
	}

	public enum HashType {
		None = 0,
		MD5 = 1
	}

	public enum ReplaceType {
		Other = 0,
		ImageLinkHref = 1,
		ImageSrc = 2
	}

	public enum DownloadType {
		Page = 1,
		Image = 2,
		Thumbnail = 3
	}

	public enum DownloadResult {
		Completed = 1,
		Skipped = 2,
		RetryLater = 3
	}

	public enum StopReason {
		Other = 0,
		UserRequest = 1,
		Exiting = 2,
		PageNotFound = 3,
		DownloadComplete = 4,
		IOError = 5
	}

	public enum BomType {
		None = 0,
		Utf8 = 1,
		Utf16LE = 2,
		Utf16BE = 3
	}
}
