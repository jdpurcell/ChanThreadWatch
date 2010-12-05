using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Web;

namespace ChanThreadWatch {
	public class ThreadWatcher {
		private const int _maxDownloadTries = 3;

		private Thread _watchThread;
		private object _settingsSync = new object();
		private ManualResetEvent _stopEvent = new ManualResetEvent(false);
		private StopReason _stopReason;
		private AutoResetEvent _nextCheckTicksChangedEvent = new AutoResetEvent(false);
		private bool _hasRun;
		private volatile bool _isWaiting;
		private string _pageURL;
		private string _pageAuth;
		private string _imageAuth;
		private bool _oneTimeDownload;
		private int _checkIntervalSeconds;
		private string _mainDownloadDir = Settings.AbsoluteDownloadDir;
		private string _threadDownloadDir;
		private long _nextCheckTicks;

		public ThreadWatcher(string pageURL) {
			_pageURL = pageURL;
		}

		public string PageURL {
			get { return _pageURL; }
		}

		public string PageAuth {
			get { return _pageAuth; }
			set { SetSetting(out _pageAuth, value, true, false); }
		}

		public string ImageAuth {
			get { return _imageAuth; }
			set { SetSetting(out _imageAuth, value, true, false); }
		}

		public bool OneTimeDownload {
			get { return _oneTimeDownload; }
			set { SetSetting(out _oneTimeDownload, value, true, false); }
		}

		public int CheckIntervalSeconds {
			get { lock (_settingsSync) { return _checkIntervalSeconds; } }
			set {
				lock (_settingsSync) {
					int changeAmount = value - _checkIntervalSeconds;
					_checkIntervalSeconds = value;
					_nextCheckTicks += changeAmount * 1000;
				}
				_nextCheckTicksChangedEvent.Set();
			}
		}

		public string MainDownloadDirectory {
			get { lock (_settingsSync) { return _mainDownloadDir; } }
		}

		public string ThreadDownloadDirectory {
			get { lock (_settingsSync) { return _threadDownloadDir; } }
			set { SetSetting(out _threadDownloadDir, value, false, false); }
		}

		public int MillisecondsUntilNextCheck {
			get {
				lock (_settingsSync) {
					return Math.Max((int)(_nextCheckTicks - Environment.TickCount), 0);
				}
			}
			set {
				lock (_settingsSync) {
					_nextCheckTicks = Environment.TickCount + value;
				}
				_nextCheckTicksChangedEvent.Set();
			}
		}

		private void SetSetting<T>(out T field, T value, bool canChangeAfterRunning, bool canChangeWhileRunning) {
			lock (_settingsSync) {
				if (!canChangeAfterRunning && _hasRun) {
					throw new Exception("This setting cannot be changed after the watcher has run.");
				}
				if (!canChangeWhileRunning && IsRunning) {
					throw new Exception("This setting cannot be changed while the watcher is running.");
				}
				field = value;
			}
		}

		public void Start() {
			lock (_settingsSync) {
				if (IsRunning) {
					throw new Exception("The watcher is already running.");
				}
				_stopEvent.Reset();
				_stopReason = StopReason.Other;
				_hasRun = true;
				_watchThread = new Thread(WatchThread);
				_watchThread.Start();
			}
		}

		public void Stop(StopReason reason) {
			lock (_settingsSync) {
				if (!IsStopping) {
					_stopEvent.Set();
					_stopReason = reason;
				}
			}
		}

		public void WaitUntilStopped() {
			WaitUntilStopped(Timeout.Infinite);
		}

		public bool WaitUntilStopped(int timeout) {
			if (_watchThread != null) {
				return _watchThread.Join(timeout);
			}
			return true;
		}

		public bool IsRunning {
			get { return _watchThread != null && _watchThread.IsAlive; }
		}

		public bool IsWaiting {
			get { return _isWaiting; }
			private set { _isWaiting = value; }
		}

		public bool IsStopping {
			get { lock (_settingsSync) { return _stopEvent.WaitOne(0, false); } }
		}

		public StopReason StopReason {
			get { lock (_settingsSync) { return _stopReason; } }
			set { SetSetting(out _stopReason, value, false, false); }
		}

		public event EventHandler<ThreadWatcher, DownloadStatusEventArgs> DownloadStatus;

		public event EventHandler<ThreadWatcher, EventArgs> WaitStatus;

		public event EventHandler<ThreadWatcher, StopStatusEventArgs> StopStatus;

		private void OnDownloadStatus(DownloadStatusEventArgs e) {
			var evt = DownloadStatus;
			if (evt != null) evt(this, e);
		}

		private void OnWaitStatus(EventArgs e) {
			var evt = WaitStatus;
			if (evt != null) evt(this, e);
		}

		private void OnStopStatus(StopStatusEventArgs e) {
			var evt = StopStatus;
			if (evt != null) evt(this, e);
		}

		private void WatchThread() {
			SiteHelper siteHelper;
			List<PageInfo> pageList = new List<PageInfo>();
			var completedImageDiskFileNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			var imageDiskFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var completedThumbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			string saveDir;
			string saveThumbsDir;
			string threadName;
			int maxFileNameLength = 0;

			try {
				siteHelper = SiteHelper.GetInstance(PageURL);
				siteHelper.SetURL(PageURL);
				threadName = siteHelper.GetThreadName();
				if (String.IsNullOrEmpty(ThreadDownloadDirectory)) {
					lock (_settingsSync) {
						_threadDownloadDir = Path.Combine(MainDownloadDirectory, General.CleanFileName(String.Format(
							"{0}_{1}_{2}", siteHelper.GetSiteName(), siteHelper.GetBoardName(), threadName)));
					}
					if (!Directory.Exists(ThreadDownloadDirectory)) {
						try {
							Directory.CreateDirectory(ThreadDownloadDirectory);
						}
						catch {
							Stop(StopReason.IOError);
						}
					}
				}
				saveDir = ThreadDownloadDirectory;
				saveThumbsDir = Path.Combine(ThreadDownloadDirectory, "thumbs");

				pageList.Add(new PageInfo { URL = PageURL });

				while (!IsStopping) {
					Queue<ImageInfo> pendingImages = new Queue<ImageInfo>();
					Queue<ThumbnailInfo> pendingThumbs = new Queue<ThumbnailInfo>();

					foreach (PageInfo pageInfo in pageList) {
						// Reset the fresh flag on all of the pages before downloading starts so that
						// they're valid even if stopping before all the pages have been downloaded
						pageInfo.IsFresh = false;
					}

					int pageIndex = 0;
					OnDownloadStatus(new DownloadStatusEventArgs(DownloadType.Page, 0, pageList.Count));
					do {
						string saveFileName = General.CleanFileName(threadName) + ((pageIndex == 0) ? String.Empty : ("_" + (pageIndex + 1))) + ".html";
						string pageContent = null;

						PageInfo pageInfo = pageList[pageIndex];
						pageInfo.Path = Path.Combine(saveDir, saveFileName);

						ManualResetEvent downloadEndEvent = new ManualResetEvent(false);
						DownloadPageEndCallback downloadEnd = (completed, content, lastModifiedTime, encoding, replaceList) => {
							if (completed) {
								pageInfo.IsFresh = true;
								pageContent = content;
								pageInfo.CacheTime = lastModifiedTime;
								pageInfo.Encoding = encoding;
								pageInfo.ReplaceList = replaceList;
							}
							downloadEndEvent.Set();
						};
						DownloadPageAsync(pageInfo.Path, pageInfo.URL, PageAuth, pageInfo.CacheTime, downloadEnd);
						downloadEndEvent.WaitOne();

						if (pageContent != null) {
							siteHelper.SetURL(pageInfo.URL);
							siteHelper.SetHTML(pageContent);

							List<ThumbnailInfo> thumbs = new List<ThumbnailInfo>();
							List<ImageInfo> images = siteHelper.GetImages(pageInfo.ReplaceList, thumbs);
							if (completedImageDiskFileNames.Count == 0) {
								foreach (ImageInfo image in images) {
									for (int iName = 0; iName < 2; iName++) {
										string baseFileName = (iName == 0) ? image.OriginalFileName : image.FileName;
										string baseFileNameNoExtension = Path.GetFileNameWithoutExtension(baseFileName);
										string baseExtension = Path.GetExtension(baseFileName);
										int iSuffix = 1;
										string fileName;
										do {
											fileName = baseFileNameNoExtension + ((iSuffix == 1) ? String.Empty :
												("_" + iSuffix)) + baseExtension;
											iSuffix++;
										}
										while (imageDiskFileNames.Contains(fileName));
										if (File.Exists(Path.Combine(saveDir, fileName))) {
											imageDiskFileNames.Add(fileName);
											completedImageDiskFileNames[image.FileName] = fileName;
											break;
										}
									}
								}
								foreach (ThumbnailInfo thumb in thumbs) {
									if (File.Exists(Path.Combine(saveThumbsDir, thumb.FileName))) {
										completedThumbs.Add(thumb.FileName);
									}
								}
							}
							foreach (ImageInfo image in images) {
								if (!completedImageDiskFileNames.ContainsKey(image.FileName)) {
									pendingImages.Enqueue(image);
								}
							}
							foreach (ThumbnailInfo thumb in thumbs) {
								if (!completedThumbs.Contains(thumb.FileName)) {
									pendingThumbs.Enqueue(thumb);
								}
							}

							string nextPageURL = siteHelper.GetNextPageURL();
							if (!String.IsNullOrEmpty(nextPageURL)) {
								PageInfo nextPageInfo = new PageInfo { URL = nextPageURL };
								if (pageIndex == pageList.Count - 1) {
									pageList.Add(nextPageInfo);
								}
								else if (pageList[pageIndex + 1].URL != nextPageURL) {
									pageList[pageIndex + 1] = nextPageInfo;
								}
							}
							else if (pageIndex < pageList.Count - 1) {
								pageList.RemoveRange(pageIndex + 1, pageList.Count - (pageIndex + 1));
							}
						}

						OnDownloadStatus(new DownloadStatusEventArgs(DownloadType.Page, pageIndex + 1, pageList.Count));
					}
					while (++pageIndex < pageList.Count && !IsStopping);

					MillisecondsUntilNextCheck = (CheckIntervalSeconds * 1000);

					if (pendingImages.Count != 0 && !IsStopping) {
						if (maxFileNameLength == 0) {
							maxFileNameLength = General.GetMaximumFileNameLength(saveDir);
						}

						List<ManualResetEvent> downloadEndEvents = new List<ManualResetEvent>();
						int totalImageCount = completedImageDiskFileNames.Count + pendingImages.Count;
						OnDownloadStatus(new DownloadStatusEventArgs(DownloadType.Image, completedImageDiskFileNames.Count, totalImageCount));
						while (pendingImages.Count != 0 && !IsStopping) {
							string saveFileNameNoExtension;
							string saveExtension;
							string savePath;
							ImageInfo image = pendingImages.Dequeue();
							bool pathTooLong = false;

						MakeImagePath:
							if ((Settings.UseOriginalFileNames == true) && !String.IsNullOrEmpty(image.OriginalFileName) && !pathTooLong) {
								saveFileNameNoExtension = Path.GetFileNameWithoutExtension(image.OriginalFileName);
								saveExtension = Path.GetExtension(image.OriginalFileName);
							}
							else {
								saveFileNameNoExtension = Path.GetFileNameWithoutExtension(image.FileName);
								saveExtension = Path.GetExtension(image.FileName);
							}

							int iSuffix = 1;
							bool fileNameTaken;
							string saveFileName;
							do {
								savePath = Path.Combine(saveDir, saveFileNameNoExtension + ((iSuffix == 1) ?
									String.Empty : ("_" + iSuffix)) + saveExtension);
								saveFileName = Path.GetFileName(savePath);
								fileNameTaken = imageDiskFileNames.Contains(saveFileName);
								iSuffix++;
							}
							while (fileNameTaken);

							if (saveFileName.Length > maxFileNameLength && !pathTooLong) {
								pathTooLong = true;
								goto MakeImagePath;
							}
							imageDiskFileNames.Add(saveFileName);

							HashType hashType = (Settings.VerifyImageHashes != false) ? image.HashType : HashType.None;
							ManualResetEvent downloadEndEvent = new ManualResetEvent(false);
							DownloadFileEndCallback onDownloadEnd = (completed) => {
								if (completed) {
									lock (completedImageDiskFileNames) {
										completedImageDiskFileNames[image.FileName] = saveFileName;
										OnDownloadStatus(new DownloadStatusEventArgs(DownloadType.Image, completedImageDiskFileNames.Count, totalImageCount));
									}
								}
								downloadEndEvent.Set();
							};
							downloadEndEvents.Add(downloadEndEvent);
							DownloadFileAsync(savePath, image.URL, ImageAuth, image.Referer, hashType, image.Hash, onDownloadEnd);
						}
						foreach (ManualResetEvent downloadEndEvent in downloadEndEvents) {
							downloadEndEvent.WaitOne();
						}
					}

					if (Settings.SaveThumbnails == true) {
						if (pendingThumbs.Count != 0 && !IsStopping) {
							if (!Directory.Exists(saveThumbsDir)) {
								try {
									Directory.CreateDirectory(saveThumbsDir);
								}
								catch {
									Stop(StopReason.IOError);
								}
							}

							List<ManualResetEvent> downloadEndEvents = new List<ManualResetEvent>();
							int totalThumbCount = completedThumbs.Count + pendingThumbs.Count;
							OnDownloadStatus(new DownloadStatusEventArgs(DownloadType.Thumbnail, completedThumbs.Count, totalThumbCount));
							while (pendingThumbs.Count != 0 && !IsStopping) {
								ThumbnailInfo thumb = pendingThumbs.Dequeue();
								string savePath = Path.Combine(saveThumbsDir, thumb.FileName);

								ManualResetEvent downloadEndEvent = new ManualResetEvent(false);
								DownloadFileEndCallback onDownloadEnd = (completed) => {
									if (completed) {
										lock (completedThumbs) {
											completedThumbs.Add(thumb.FileName);
											OnDownloadStatus(new DownloadStatusEventArgs(DownloadType.Thumbnail, completedThumbs.Count, totalThumbCount));
										}
									}
									downloadEndEvent.Set();
								};
								downloadEndEvents.Add(downloadEndEvent);
								DownloadFileAsync(savePath, thumb.URL, PageAuth, thumb.Referer, HashType.None, null, onDownloadEnd);
							}
							foreach (ManualResetEvent downloadEndEvent in downloadEndEvents) {
								downloadEndEvent.WaitOne();
							}
						}

						if (!IsStopping || StopReason != StopReason.IOError) {
							foreach (PageInfo pageInfo in pageList) {
								if (!pageInfo.IsFresh) continue;
								string pageContent = General.HTMLBytesToString(File.ReadAllBytes(pageInfo.Path), pageInfo.Encoding);
								for (int i = 0; i < pageInfo.ReplaceList.Count; i++) {
									ReplaceInfo replace = pageInfo.ReplaceList[i];
									string saveFileName;
									if ((replace.Type == ReplaceType.ImageLinkHref) && completedImageDiskFileNames.TryGetValue(replace.Tag, out saveFileName)) {
										replace.Value = "href=\"" + HttpUtility.HtmlAttributeEncode(saveFileName) + "\"";
									}
									if (replace.Type == ReplaceType.ImageSrc) {
										replace.Value = "src=\"thumbs/" + HttpUtility.HtmlAttributeEncode(replace.Tag) + "\"";
									}
								}
								General.AddOtherReplaces(pageContent, pageInfo.ReplaceList);
								using (StreamWriter sw = new StreamWriter(pageInfo.Path, false, pageInfo.Encoding)) {
									General.WriteReplacedString(pageContent, pageInfo.ReplaceList, sw);
								}
								if (General.FindElementClose(pageContent, "html", 0) != -1 && File.Exists(pageInfo.Path + ".bak")) {
									try { File.Delete(pageInfo.Path + ".bak"); }
									catch { }
								}
							}
						}
					}

					if (OneTimeDownload) {
						Stop(StopReason.DownloadComplete);
					}

					if (IsStopping) break;

					if (MillisecondsUntilNextCheck > 0) {
						IsWaiting = true;
						OnWaitStatus(EventArgs.Empty);
						_nextCheckTicksChangedEvent.WaitOne(0, false);
						WaitHandle[] waitHandles = new WaitHandle[] { _stopEvent, _nextCheckTicksChangedEvent };
						do {
							WaitHandle.WaitAny(waitHandles, MillisecondsUntilNextCheck, false);
						}
						while (!IsStopping && MillisecondsUntilNextCheck > 0);
						IsWaiting = false;
					}
				}

				OnStopStatus(new StopStatusEventArgs(StopReason));
			}
			catch {
				OnStopStatus(new StopStatusEventArgs(StopReason.Other));
			}
		}

		private void DownloadPageAsync(string path, string url, string auth, DateTime? cacheLastModifiedTime, DownloadPageEndCallback onDownloadEnd) {
			ConnectionManager connectionManager = ConnectionManager.GetInstance(url);
			string connectionName = connectionManager.ObtainConnection();

			string backupPath = path + ".bak";
			int tryNumber = 0;

			Action tryDownload = null;
			tryDownload = () => {
				string httpCharSet = null;
				DateTime? lastModifiedTime = null;
				Encoding encoding = null;
				List<ReplaceInfo> replaceList = null;
				string content = null;

				Action<bool> endTryDownload = (completed) => {
					connectionManager.ReleaseConnection(connectionName);
					onDownloadEnd(completed, content, lastModifiedTime, encoding, replaceList);
				};

				tryNumber++;
				if (IsStopping || tryNumber > _maxDownloadTries) {
					endTryDownload(false);
					return;
				}

				FileStream fileStream = null;
				bool createdFile = false;
				MemoryStream memoryStream = null;
				Action closeStreams = () => {
					if (fileStream != null) try { fileStream.Close(); } catch { }
					if (memoryStream != null) try { memoryStream.Close(); } catch { }
				};
				Action deleteFile = () => {
					try { File.Delete(path); } catch { }
					if (File.Exists(backupPath)) {
						try { File.Move(backupPath, path); } catch { }
					}
				};

				General.DownloadAsync(url, auth, null, connectionName, cacheLastModifiedTime,
					(response) => {
						if (File.Exists(path)) {
							if (File.Exists(backupPath)) {
								try { File.Delete(backupPath); } catch { }
							}
							try { File.Move(path, backupPath); } catch { }
						}
						fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
						createdFile = true;
						memoryStream = new MemoryStream();
						httpCharSet = General.GetCharSetFromContentType(response.ContentType);
						lastModifiedTime = General.GetResponseLastModifiedTime(response);
					},
					(data, dataLength) => {
						fileStream.Write(data, 0, dataLength);
						memoryStream.Write(data, 0, dataLength);
					},
					() => {
						byte[] pageBytes = memoryStream.ToArray();
						closeStreams();
						encoding = General.DetectHTMLEncoding(pageBytes, httpCharSet);
						replaceList = (Settings.SaveThumbnails == true) ? new List<ReplaceInfo>() : null;
						content = General.HTMLBytesToString(pageBytes, encoding, replaceList);
						endTryDownload(true);
					},
					(ex) => {
						closeStreams();
						if (createdFile) deleteFile();
						if (ex is HTTP304Exception) {
							// Page not modified, skip
							endTryDownload(false);
						}
						else if (ex is HTTP404Exception) {
							// Page not found, stop
							Stop(StopReason.PageNotFound);
							endTryDownload(false);
						}
						else if (ex is DirectoryNotFoundException || ex is PathTooLongException || ex is UnauthorizedAccessException) {
							// Fatal IO error, stop
							Stop(StopReason.IOError);
							endTryDownload(false);
						}
						else {
							// Other error, retry
							connectionName = connectionManager.SwapForFreshConnection(connectionName, url);
							tryDownload();
						}
					});
			};

			tryDownload();
		}

		private void DownloadFileAsync(string path, string url, string auth, string referer, HashType hashType, byte[] correctHash, DownloadFileEndCallback onDownloadEnd) {
			ConnectionManager connectionManager = ConnectionManager.GetInstance(url);
			string connectionName = connectionManager.ObtainConnection();

			int tryNumber = 0;
			byte[] prevHash = null;

			Action tryDownload = null;
			tryDownload = () => {
				Action<bool> endTryDownload = (completed) => {
					connectionManager.ReleaseConnection(connectionName);
					onDownloadEnd(completed);
				};

				tryNumber++;
				if (IsStopping || tryNumber > _maxDownloadTries) {
					endTryDownload(false);
					return;
				}

				FileStream fileStream = null;
				bool createdFile = false;
				HashGeneratorStream hashStream = null;
				Action closeStreams = () => {
					if (fileStream != null) try { fileStream.Close(); } catch { }
					if (hashStream != null) try { hashStream.Close(); } catch { }
				};
				Action deleteFile = () => {
					try { File.Delete(path); } catch { }
				};

				General.DownloadAsync(url, auth, referer, connectionName, null,
					(response) => {
						fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
						createdFile = true;
						if (hashType != HashType.None) {
							hashStream = new HashGeneratorStream(hashType);
						}
					},
					(data, dataLength) => {
						fileStream.Write(data, 0, dataLength);
						if (hashStream != null) hashStream.Write(data, 0, dataLength);
					},
					() => {
						byte[] hash = (hashType != HashType.None) ? hashStream.GetDataHash() : null;
						closeStreams();
						if (hashType != HashType.None && !General.ArraysAreEqual(hash, correctHash) &&
							(prevHash == null || !General.ArraysAreEqual(hash, prevHash)))
						{
							// Incorrect hash, retry
							prevHash = hash;
							deleteFile();
							tryDownload();
							return;
						}
						endTryDownload(true);
					},
					(ex) => {
						closeStreams();
						if (createdFile) deleteFile();
						if (ex is HTTP404Exception || ex is PathTooLongException) {
							// Fatal problem with this file, skip and mark as complete to prevent future retries
							endTryDownload(true);
						}
						else if (ex is DirectoryNotFoundException || ex is UnauthorizedAccessException) {
							// Fatal IO error, stop
							Stop(StopReason.IOError);
							endTryDownload(false);
						}
						else {
							// Other error, retry
							connectionName = connectionManager.SwapForFreshConnection(connectionName, url);
							tryDownload();
						}
					});
			};

			tryDownload();
		}
	}
}
