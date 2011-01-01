﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web;

namespace ChanThreadWatch {
	public class ThreadWatcher {
		private const int _maxDownloadTries = 3;

		private static WorkScheduler _workScheduler = new WorkScheduler();

		private WorkScheduler.WorkItem _nextCheckWorkItem;
		private object _settingsSync = new object();
		private ManualResetEvent _stopEvent = new ManualResetEvent(false);
		private StopReason _stopReason;
		private Dictionary<long, Action> _downloadAborters = new Dictionary<long, Action>();
		private bool _hasRun;
		private ManualResetEvent _checkFinishedEvent = new ManualResetEvent(true);
		private bool _isWaiting;
		private string _pageURL;
		private string _pageAuth;
		private string _imageAuth;
		private bool _oneTimeDownload;
		private int _checkIntervalSeconds;
		private string _mainDownloadDir = Settings.AbsoluteDownloadDir;
		private string _threadDownloadDir;
		private long _nextCheckTicks;
		private string _description = String.Empty;
		private bool _renameThreadDownloadDir;
		private object _tag;

		static ThreadWatcher() {
			// HttpWebRequest uses ThreadPool for asynchronous calls
			General.EnsureThreadPoolMaxThreads(500, 1000);

			// Shouldn't matter since the limit is supposed to be per connection group
			ServicePointManager.DefaultConnectionLimit = Int32.MaxValue;

			// Ignore invalid certificates
			ServicePointManager.ServerCertificateValidationCallback = (s, cert, chain, errors) => true;
		}

		public ThreadWatcher(string pageURL) {
			_pageURL = pageURL;
		}

		public string PageURL {
			get { return _pageURL; }
		}

		private string PageHost {
			get { return (new Uri(PageURL)).Host; }
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
					int newCheckIntervalSeconds = (_hasInitialized && value < _minCheckIntervalSeconds) ?
						_minCheckIntervalSeconds : value;
					int changeAmount = newCheckIntervalSeconds - _checkIntervalSeconds;
					_checkIntervalSeconds = newCheckIntervalSeconds;
					NextCheckTicks += changeAmount * 1000;
				}
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
			get { return Math.Max((int)(NextCheckTicks - TickCount.Now), 0); }
			set { NextCheckTicks = TickCount.Now + value; }
		}

		private long NextCheckTicks {
			get { lock (_settingsSync) { return _nextCheckTicks; } }
			set {
				lock (_settingsSync) {
					_nextCheckTicks = value;
					if (_nextCheckWorkItem != null) {
						_nextCheckWorkItem.RunAtTicks = _nextCheckTicks;
					}
				}
			}
		}

		public string Description {
			get { lock (_settingsSync) { return _description; } }
			set {
				lock (_settingsSync) {
					_description = value;
					if (_hasRun && Settings.RenameDownloadFolderWithDescription == true) {
						_renameThreadDownloadDir = true;
						TryRenameThreadDownloadDir();
					}
				}
			}
		}

		public object Tag {
			get { lock (_settingsSync) { return _tag; } }
			set { lock (_settingsSync) { _tag = value; } }
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
				_hasInitialized = false;
				_nextCheckWorkItem = _workScheduler.AddItem(TickCount.Now, Check, PageHost);
			}
		}

		public void Stop(StopReason reason) {
			lock (_settingsSync) {
				if (!IsStopping) {
					_stopEvent.Set();
					_stopReason = reason;
					_hasRun = true;
					if (_nextCheckWorkItem != null) {
						_workScheduler.RemoveItem(_nextCheckWorkItem);
						_nextCheckWorkItem = null;
					}
					List<Action> downloadAborters;
					lock (_downloadAborters) {
						downloadAborters = new List<Action>(_downloadAborters.Values);
					}
					foreach (Action abortDownload in downloadAborters) {
						abortDownload();
					}
					if (_checkFinishedEvent.WaitOne(0, false)) {
						_isWaiting = false;
						OnStopStatus(new StopStatusEventArgs(reason));
					}
				}
			}
		}

		public void WaitUntilStopped() {
			WaitUntilStopped(Timeout.Infinite);
		}

		public bool WaitUntilStopped(int timeout) {
			return _checkFinishedEvent.WaitOne(timeout, false);
		}

		public bool IsRunning {
			get {
				lock (_settingsSync) {
					return !_checkFinishedEvent.WaitOne(0, false) || _nextCheckWorkItem != null;
				}
			}
		}

		public bool IsWaiting {
			get { lock (_settingsSync) { return _isWaiting; } }
		}

		public bool IsStopping {
			get { lock (_settingsSync) { return _stopEvent.WaitOne(0, false); } }
		}

		public StopReason StopReason {
			get { lock (_settingsSync) { return _stopReason; } }
		}

		public event EventHandler<ThreadWatcher, DownloadStatusEventArgs> DownloadStatus;

		public event EventHandler<ThreadWatcher, EventArgs> WaitStatus;

		public event EventHandler<ThreadWatcher, StopStatusEventArgs> StopStatus;

		public event EventHandler<ThreadWatcher, EventArgs> ThreadDownloadDirectoryRename;

		public event EventHandler<ThreadWatcher, DownloadStartEventArgs> DownloadStart;

		public event EventHandler<ThreadWatcher, DownloadProgressEventArgs> DownloadProgress;

		public event EventHandler<ThreadWatcher, DownloadEndEventArgs> DownloadEnd;

		private void OnDownloadStatus(DownloadStatusEventArgs e) {
			var evt = DownloadStatus;
			if (evt != null) try { evt(this, e); } catch { }
		}

		private void OnWaitStatus(EventArgs e) {
			var evt = WaitStatus;
			if (evt != null) try { evt(this, e); } catch { }
		}

		private void OnStopStatus(StopStatusEventArgs e) {
			var evt = StopStatus;
			if (evt != null) try { evt(this, e); } catch { }
		}

		private void OnThreadDownloadDirectoryRename(EventArgs e) {
			var evt = ThreadDownloadDirectoryRename;
			if (evt != null) try { evt(this, e); } catch { }
		}

		private void OnDownloadStart(DownloadStartEventArgs e) {
			var evt = DownloadStart;
			if (evt != null) try { evt(this, e); } catch { }
		}

		private void OnDownloadProgress(DownloadProgressEventArgs e) {
			var evt = DownloadProgress;
			if (evt != null) try { evt(this, e); } catch { }
		}

		private void OnDownloadEnd(DownloadEndEventArgs e) {
			var evt = DownloadEnd;
			if (evt != null) try { evt(this, e); } catch { }
		}

		private bool _hasInitialized;
		private List<PageInfo> _pageList;
		private HashSet<string> _imageDiskFileNames;
		private Dictionary<string, DownloadInfo> _completedImages;
		private Dictionary<string, DownloadInfo> _completedThumbs;
		private int _maxFileNameLength;
		private string _threadName;
		private int _minCheckIntervalSeconds;

		private void Check() {
			try {
				SiteHelper siteHelper = SiteHelper.GetInstance(PageHost);
				string threadDir;
				string imageDir;
				string thumbDir;

				lock (_settingsSync) {
					_nextCheckWorkItem = null;
					_checkFinishedEvent.Reset();
					_isWaiting = false;
				}

				if (!_hasInitialized) {
					siteHelper.SetURL(PageURL);

					_pageList = new List<PageInfo>();
					_imageDiskFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
					_completedImages = new Dictionary<string, DownloadInfo>(StringComparer.OrdinalIgnoreCase);
					_completedThumbs = new Dictionary<string, DownloadInfo>(StringComparer.OrdinalIgnoreCase);
					_maxFileNameLength = 0;
					_threadName = siteHelper.GetThreadName();
					_minCheckIntervalSeconds = siteHelper.IsBoardHighTurnover() ? 30 : 60;

					if (String.IsNullOrEmpty(ThreadDownloadDirectory)) {
						lock (_settingsSync) {
							_threadDownloadDir = Path.Combine(MainDownloadDirectory, General.CleanFileName(String.Format(
								"{0}_{1}_{2}", siteHelper.GetSiteName(), siteHelper.GetBoardName(), _threadName)));
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
					if (String.IsNullOrEmpty(Description)) {
						lock (_settingsSync) {
							_description = General.GetLastDirectory(_threadDownloadDir);
						}
					}

					_pageList.Add(new PageInfo {
						URL = PageURL
					});

					_hasInitialized = true;
				}

				threadDir = ThreadDownloadDirectory;
				imageDir = ThreadDownloadDirectory;
				thumbDir = Path.Combine(ThreadDownloadDirectory, "thumbs");

				Queue<ImageInfo> pendingImages = new Queue<ImageInfo>();
				Queue<ThumbnailInfo> pendingThumbs = new Queue<ThumbnailInfo>();

				foreach (PageInfo pageInfo in _pageList) {
					// Reset the fresh flag on all of the pages before downloading starts so that
					// they're valid even if stopping before all the pages have been downloaded
					pageInfo.IsFresh = false;
				}

				int pageIndex = 0;
				OnDownloadStatus(new DownloadStatusEventArgs(DownloadType.Page, 0, _pageList.Count));
				while (pageIndex < _pageList.Count && !IsStopping) {
					string saveFileName = General.CleanFileName(_threadName) + ((pageIndex == 0) ? String.Empty : ("_" + (pageIndex + 1))) + ".html";
					string pageContent = null;

					PageInfo pageInfo = _pageList[pageIndex];
					pageInfo.Path = Path.Combine(threadDir, saveFileName);

					ManualResetEvent downloadEndEvent = new ManualResetEvent(false);
					DownloadPageEndCallback downloadEnd = (result, content, lastModifiedTime, encoding, replaceList) => {
						if (result == DownloadResult.Completed) {
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
						if (_completedImages.Count == 0) {
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
									while (_imageDiskFileNames.Contains(fileName));
									string path = Path.Combine(imageDir, fileName);
									if (File.Exists(path)) {
										_imageDiskFileNames.Add(fileName);
										_completedImages[image.FileName] = new DownloadInfo {
											FileName = fileName,
											Skipped = false
										};
										break;
									}
								}
							}
							foreach (ThumbnailInfo thumb in thumbs) {
								string path = Path.Combine(thumbDir, thumb.FileName);
								if (File.Exists(path)) {
									_completedThumbs[thumb.FileName] = new DownloadInfo {
										FileName = thumb.FileName,
										Skipped = false
									};
								}
							}
						}
						foreach (ImageInfo image in images) {
							if (!_completedImages.ContainsKey(image.FileName)) {
								pendingImages.Enqueue(image);
							}
						}
						foreach (ThumbnailInfo thumb in thumbs) {
							if (!_completedThumbs.ContainsKey(thumb.FileName)) {
								pendingThumbs.Enqueue(thumb);
							}
						}

						string nextPageURL = siteHelper.GetNextPageURL();
						if (!String.IsNullOrEmpty(nextPageURL)) {
							PageInfo nextPageInfo = new PageInfo {
								URL = nextPageURL
							};
							if (pageIndex == _pageList.Count - 1) {
								_pageList.Add(nextPageInfo);
							}
							else if (_pageList[pageIndex + 1].URL != nextPageURL) {
								_pageList[pageIndex + 1] = nextPageInfo;
							}
						}
						else if (pageIndex < _pageList.Count - 1) {
							_pageList.RemoveRange(pageIndex + 1, _pageList.Count - (pageIndex + 1));
						}
					}

					pageIndex++;
					OnDownloadStatus(new DownloadStatusEventArgs(DownloadType.Page, pageIndex, _pageList.Count));
				}

				MillisecondsUntilNextCheck = Math.Max(CheckIntervalSeconds, _minCheckIntervalSeconds) * 1000;

				if (pendingImages.Count != 0 && !IsStopping) {
					if (_maxFileNameLength == 0) {
						_maxFileNameLength = General.GetMaximumFileNameLength(imageDir);
					}

					List<ManualResetEvent> downloadEndEvents = new List<ManualResetEvent>();
					int completedImageCount = 0;
					foreach (KeyValuePair<string, DownloadInfo> item in _completedImages) {
						if (!item.Value.Skipped) completedImageCount++;
					}
					int totalImageCount = completedImageCount + pendingImages.Count;
					OnDownloadStatus(new DownloadStatusEventArgs(DownloadType.Image, completedImageCount, totalImageCount));
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
							savePath = Path.Combine(imageDir, saveFileNameNoExtension + ((iSuffix == 1) ?
								String.Empty : ("_" + iSuffix)) + saveExtension);
							saveFileName = Path.GetFileName(savePath);
							fileNameTaken = _imageDiskFileNames.Contains(saveFileName);
							iSuffix++;
						}
						while (fileNameTaken);

						if (saveFileName.Length > _maxFileNameLength && !pathTooLong) {
							pathTooLong = true;
							goto MakeImagePath;
						}
						_imageDiskFileNames.Add(saveFileName);

						HashType hashType = (Settings.VerifyImageHashes != false) ? image.HashType : HashType.None;
						ManualResetEvent downloadEndEvent = new ManualResetEvent(false);
						DownloadFileEndCallback onDownloadEnd = (result) => {
							if (result == DownloadResult.Completed || result == DownloadResult.Skipped) {
								lock (_completedImages) {
									_completedImages[image.FileName] = new DownloadInfo {
										FileName = saveFileName,
										Skipped = (result == DownloadResult.Skipped)
									};
									if (result != DownloadResult.Skipped) {
										completedImageCount++;
									}
									else {
										totalImageCount--;
									}
									OnDownloadStatus(new DownloadStatusEventArgs(DownloadType.Image, completedImageCount, totalImageCount));
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

				if (Settings.SaveThumbnails != false) {
					if (pendingThumbs.Count != 0 && !IsStopping) {
						if (!Directory.Exists(thumbDir)) {
							try {
								Directory.CreateDirectory(thumbDir);
							}
							catch {
								Stop(StopReason.IOError);
							}
						}

						List<ManualResetEvent> downloadEndEvents = new List<ManualResetEvent>();
						int completedThumbCount = 0;
						foreach (KeyValuePair<string, DownloadInfo> item in _completedThumbs) {
							if (!item.Value.Skipped) completedThumbCount++;
						}
						int totalThumbCount = completedThumbCount + pendingThumbs.Count;
						OnDownloadStatus(new DownloadStatusEventArgs(DownloadType.Thumbnail, completedThumbCount, totalThumbCount));
						while (pendingThumbs.Count != 0 && !IsStopping) {
							ThumbnailInfo thumb = pendingThumbs.Dequeue();
							string savePath = Path.Combine(thumbDir, thumb.FileName);

							ManualResetEvent downloadEndEvent = new ManualResetEvent(false);
							DownloadFileEndCallback onDownloadEnd = (result) => {
								if (result == DownloadResult.Completed || result == DownloadResult.Skipped) {
									lock (_completedThumbs) {
										_completedThumbs[thumb.FileName] = new DownloadInfo {
											FileName = thumb.FileName,
											Skipped = (result == DownloadResult.Skipped)
										};
										if (result != DownloadResult.Skipped) {
											completedThumbCount++;
										}
										else {
											totalThumbCount--;
										}
										OnDownloadStatus(new DownloadStatusEventArgs(DownloadType.Thumbnail, completedThumbCount, totalThumbCount));
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
						foreach (PageInfo pageInfo in _pageList) {
							if (!pageInfo.IsFresh) continue;
							string pageContent = General.HTMLBytesToString(File.ReadAllBytes(pageInfo.Path), pageInfo.Encoding);
							for (int i = 0; i < pageInfo.ReplaceList.Count; i++) {
								ReplaceInfo replace = pageInfo.ReplaceList[i];
								DownloadInfo downloadInfo = null;
								Func<string, string> getRelativeDownloadPath = (fileDownloadDir) => {
									return General.GetRelativeFilePath(Path.Combine(fileDownloadDir, downloadInfo.FileName),
										threadDir).Replace(Path.DirectorySeparatorChar, '/');
								};
								if (replace.Type == ReplaceType.ImageLinkHref && _completedImages.TryGetValue(replace.Tag, out downloadInfo)) {
									replace.Value = "href=\"" + HttpUtility.HtmlAttributeEncode(getRelativeDownloadPath(imageDir)) + "\"";
								}
								if (replace.Type == ReplaceType.ImageSrc && _completedThumbs.TryGetValue(replace.Tag, out downloadInfo)) {
									replace.Value = "src=\"" + HttpUtility.HtmlAttributeEncode(getRelativeDownloadPath(thumbDir)) + "\"";
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
			}
			catch {
				Stop(StopReason.Other);
			}

			lock (_settingsSync) {
				_checkFinishedEvent.Set();
				if (_renameThreadDownloadDir) {
					TryRenameThreadDownloadDir();
				}
				if (!IsStopping) {
					_nextCheckWorkItem = _workScheduler.AddItem(NextCheckTicks, Check, PageHost);
					_isWaiting = MillisecondsUntilNextCheck > 0;
				}
			}
			if (IsStopping) {
				OnStopStatus(new StopStatusEventArgs(StopReason));
			}
			else if (IsWaiting) {
				OnWaitStatus(EventArgs.Empty);
			}
		}

		private void TryRenameThreadDownloadDir() {
			lock (_settingsSync) {
				if (!_checkFinishedEvent.WaitOne(0, false) || String.IsNullOrEmpty(_threadDownloadDir) ||
					(IsStopping && (StopReason == StopReason.IOError || StopReason == StopReason.Exiting)))
				{
					return;
				}
				try {
					string destDir = Path.Combine(General.RemoveLastDirectory(_threadDownloadDir), General.CleanFileName(_description));
					if (!destDir.Equals(_threadDownloadDir, StringComparison.OrdinalIgnoreCase)) {
						Directory.Move(_threadDownloadDir, destDir);
						_threadDownloadDir = destDir;
						OnThreadDownloadDirectoryRename(EventArgs.Empty);
					}
					_renameThreadDownloadDir = false;
				}
				catch { }
			}
		}

		private void DownloadPageAsync(string path, string url, string auth, DateTime? cacheLastModifiedTime, DownloadPageEndCallback onDownloadEnd) {
			ConnectionManager connectionManager = ConnectionManager.GetInstance(url);
			string connectionGroupName = connectionManager.ObtainConnectionGroupName();

			string backupPath = path + ".bak";
			int tryNumber = 0;
			long? prevDownloadedFileSize = null;

			Action tryDownload = null;
			tryDownload = () => {
				string httpCharSet = null;
				DateTime? lastModifiedTime = null;
				Encoding encoding = null;
				List<ReplaceInfo> replaceList = null;
				string content = null;

				Action<DownloadResult> endTryDownload = (result) => {
					connectionManager.ReleaseConnectionGroupName(connectionGroupName);
					onDownloadEnd(result, content, lastModifiedTime, encoding, replaceList);
				};

				tryNumber++;
				if (IsStopping || tryNumber > _maxDownloadTries) {
					endTryDownload(DownloadResult.RetryLater);
					return;
				}

				long downloadID = (long)(General.BytesTo64BitXor(Guid.NewGuid().ToByteArray()) & 0x7FFFFFFFFFFFFFFFUL);
				FileStream fileStream = null;
				long? totalFileSize = null;
				long downloadedFileSize = 0;
				bool createdFile = false;
				MemoryStream memoryStream = null;
				bool removedDownloadAborter = false;
				Action<bool> cleanup = (successful) => {
					if (fileStream != null) try { fileStream.Close(); } catch { }
					if (memoryStream != null) try { memoryStream.Close(); } catch { }
					if (!successful && createdFile) {
						try { File.Delete(path); } catch { }
						if (File.Exists(backupPath)) {
							try { File.Move(backupPath, path); } catch { }
						}
					}
					lock (_downloadAborters) {
						_downloadAborters.Remove(downloadID);
						removedDownloadAborter = true;
					}
				};

				Action abortDownload = General.DownloadAsync(url, auth, null, connectionGroupName, cacheLastModifiedTime,
					(response) => {
						if (File.Exists(path)) {
							if (File.Exists(backupPath)) {
								try { File.Delete(backupPath); } catch { }
							}
							try { File.Move(path, backupPath); } catch { }
						}
						fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
						if (response.ContentLength != -1) {
							totalFileSize = response.ContentLength;
							fileStream.SetLength(totalFileSize.Value);
						}
						createdFile = true;
						memoryStream = new MemoryStream();
						httpCharSet = General.GetCharSetFromContentType(response.ContentType);
						lastModifiedTime = General.GetResponseLastModifiedTime(response);
						OnDownloadStart(new DownloadStartEventArgs(downloadID, url, tryNumber, totalFileSize));
					},
					(data, dataLength) => {
						fileStream.Write(data, 0, dataLength);
						memoryStream.Write(data, 0, dataLength);
						downloadedFileSize += dataLength;
						OnDownloadProgress(new DownloadProgressEventArgs(downloadID, downloadedFileSize));
					},
					() => {
						byte[] pageBytes = memoryStream.ToArray();
						if (totalFileSize != null && downloadedFileSize != totalFileSize) {
							fileStream.SetLength(downloadedFileSize);
						}
						bool incompleteDownload = totalFileSize != null && downloadedFileSize != totalFileSize &&
							(prevDownloadedFileSize == null || downloadedFileSize != prevDownloadedFileSize);
						if (incompleteDownload) {
							// Corrupt download, retry
							prevDownloadedFileSize = downloadedFileSize;
							throw new Exception("Download is corrupt.");
						}
						cleanup(true);
						OnDownloadEnd(new DownloadEndEventArgs(downloadID, downloadedFileSize, true));
						encoding = General.DetectHTMLEncoding(pageBytes, httpCharSet);
						replaceList = (Settings.SaveThumbnails != false) ? new List<ReplaceInfo>() : null;
						content = General.HTMLBytesToString(pageBytes, encoding, replaceList);
						endTryDownload(DownloadResult.Completed);
					},
					(ex) => {
						cleanup(false);
						OnDownloadEnd(new DownloadEndEventArgs(downloadID, downloadedFileSize, false));
						if (ex is HTTP304Exception) {
							// Page not modified, skip
							endTryDownload(DownloadResult.Skipped);
						}
						else if (ex is HTTP404Exception) {
							// Page not found, stop
							Stop(StopReason.PageNotFound);
							endTryDownload(DownloadResult.Skipped);
						}
						else if (ex is DirectoryNotFoundException || ex is PathTooLongException || ex is UnauthorizedAccessException) {
							// Fatal IO error, stop
							Stop(StopReason.IOError);
							endTryDownload(DownloadResult.Skipped);
						}
						else {
							// Other error, retry
							connectionGroupName = connectionManager.SwapForFreshConnection(connectionGroupName, url);
							tryDownload();
						}
					});

				lock (_downloadAborters) {
					if (!removedDownloadAborter) {
						_downloadAborters[downloadID] = abortDownload;
					}
				}
			};

			tryDownload();
		}

		private void DownloadFileAsync(string path, string url, string auth, string referer, HashType hashType, byte[] correctHash, DownloadFileEndCallback onDownloadEnd) {
			ConnectionManager connectionManager = ConnectionManager.GetInstance(url);
			string connectionGroupName = connectionManager.ObtainConnectionGroupName();

			int tryNumber = 0;
			byte[] prevHash = null;
			long? prevDownloadedFileSize = null;

			Action tryDownload = null;
			tryDownload = () => {
				Action<DownloadResult> endTryDownload = (result) => {
					connectionManager.ReleaseConnectionGroupName(connectionGroupName);
					onDownloadEnd(result);
				};

				tryNumber++;
				if (IsStopping || tryNumber > _maxDownloadTries) {
					endTryDownload(DownloadResult.RetryLater);
					return;
				}

				long downloadID = (long)(General.BytesTo64BitXor(Guid.NewGuid().ToByteArray()) & 0x7FFFFFFFFFFFFFFFUL);
				FileStream fileStream = null;
				long? totalFileSize = null;
				long downloadedFileSize = 0;
				bool createdFile = false;
				HashGeneratorStream hashStream = null;
				bool removedDownloadAborter = false;
				Action<bool> cleanup = (successful) => {
					if (fileStream != null) try { fileStream.Close(); } catch { }
					if (hashStream != null) try { hashStream.Close(); } catch { }
					if (!successful && createdFile) {
						try { File.Delete(path); } catch { }
					}
					lock (_downloadAborters) {
						_downloadAborters.Remove(downloadID);
						removedDownloadAborter = true;
					}
				};

				Action abortDownload = General.DownloadAsync(url, auth, referer, connectionGroupName, null,
					(response) => {
						fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
						if (response.ContentLength != -1) {
							totalFileSize = response.ContentLength;
							fileStream.SetLength(totalFileSize.Value);
						}
						createdFile = true;
						if (hashType != HashType.None) {
							hashStream = new HashGeneratorStream(hashType);
						}
						OnDownloadStart(new DownloadStartEventArgs(downloadID, url, tryNumber, totalFileSize));
					},
					(data, dataLength) => {
						fileStream.Write(data, 0, dataLength);
						if (hashStream != null) hashStream.Write(data, 0, dataLength);
						downloadedFileSize += dataLength;
						OnDownloadProgress(new DownloadProgressEventArgs(downloadID, downloadedFileSize));
					},
					() => {
						byte[] hash = (hashType != HashType.None) ? hashStream.GetDataHash() : null;
						if (totalFileSize != null && downloadedFileSize != totalFileSize) {
							fileStream.SetLength(downloadedFileSize);
						}
						bool incorrectHash = hashType != HashType.None && !General.ArraysAreEqual(hash, correctHash) &&
							(prevHash == null || !General.ArraysAreEqual(hash, prevHash));
						bool incompleteDownload = totalFileSize != null && downloadedFileSize != totalFileSize &&
							(prevDownloadedFileSize == null || downloadedFileSize != prevDownloadedFileSize);
						if (incorrectHash || incompleteDownload) {
							// Corrupt download, retry
							prevHash = hash;
							prevDownloadedFileSize = downloadedFileSize;
							throw new Exception("Download is corrupt.");
						}
						cleanup(true);
						OnDownloadEnd(new DownloadEndEventArgs(downloadID, downloadedFileSize, true));
						endTryDownload(DownloadResult.Completed);
					},
					(ex) => {
						cleanup(false);
						OnDownloadEnd(new DownloadEndEventArgs(downloadID, downloadedFileSize, false));
						if (ex is HTTP404Exception || ex is PathTooLongException) {
							// Fatal problem with this file, skip
							endTryDownload(DownloadResult.Skipped);
						}
						else if (ex is DirectoryNotFoundException || ex is UnauthorizedAccessException) {
							// Fatal IO error, stop
							Stop(StopReason.IOError);
							endTryDownload(DownloadResult.Skipped);
						}
						else {
							// Other error, retry
							connectionGroupName = connectionManager.SwapForFreshConnection(connectionGroupName, url);
							tryDownload();
						}
					});

				lock (_downloadAborters) {
					if (!removedDownloadAborter) {
						_downloadAborters[downloadID] = abortDownload;
					}
				}
			};

			tryDownload();
		}
	}
}