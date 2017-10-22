// --------------------------------------------------------------------------------
// Copyright (c) J.D. Purcell
//
// Licensed under the MIT License (see LICENSE.txt)
// --------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Web;

namespace JDP {
	public class ThreadWatcher {
		private const int _maxDownloadTries = 3;

		private static readonly WorkScheduler _workScheduler = new WorkScheduler();

		private readonly object _settingsSync = new object();
		private readonly Dictionary<long, Action> _downloadAborters = new Dictionary<long, Action>();
		private readonly ManualResetEvent _checkFinishedEvent = new ManualResetEvent(true);
		private readonly int _minCheckIntervalSeconds;
		private WorkScheduler.WorkItem _nextCheckWorkItem;
		private bool _isStopping;
		private StopReason _stopReason;
		private bool _hasRun;
		private bool _hasInitialized;
		private bool _isWaiting;
		private string _threadDownloadDirectory;
		private string _pageAuth;
		private string _imageAuth;
		private bool _oneTimeDownload;
		private int _checkIntervalSeconds;
		private long _nextCheckTicks;
		private string _description;
		private DateTime? _lastImageOn;
		private object _tag;

		private ThreadWatcher(ThreadWatcherConfig config) {
			SiteHelper siteHelper = SiteHelper.CreateByURL(config.PageURL);
			PageURL = config.PageURL;
			PageHost = new Uri(PageURL).Host;
			GlobalThreadID = config.GlobalThreadID;
			AddedOn = config.AddedOn;
			BaseDownloadDirectory = Settings.AbsoluteDownloadDirectory;
			PageBaseFileName = config.PageBaseFileName;
			_threadDownloadDirectory = General.GetAbsoluteDirectoryPath(config.RelativeDownloadDirectory, BaseDownloadDirectory);
			_pageAuth = config.PageAuth;
			_imageAuth = config.ImageAuth;
			_oneTimeDownload = config.OneTimeDownload;
			_minCheckIntervalSeconds = siteHelper.IsBoardHighTurnover() ? 30 : 60;
			_checkIntervalSeconds = Math.Max(config.CheckIntervalSeconds, _minCheckIntervalSeconds);
			_description = config.Description;
			_lastImageOn = config.LastImageOn;
			if (config.StopReason != null) {
				Stop(config.StopReason.Value);
			}
		}

		public static ThreadWatcher Create(string pageURL, string pageAuth, string imageAuth, bool oneTimeDownload, int checkIntervalSeconds) {
			SiteHelper siteHelper = SiteHelper.CreateByURL(pageURL);
			string globalThreadID = siteHelper.GetGlobalThreadID();
			return new ThreadWatcher(new ThreadWatcherConfig {
				PageURL = pageURL,
				GlobalThreadID = globalThreadID,
				AddedOn = DateTime.UtcNow,
				PageAuth = pageAuth,
				ImageAuth = imageAuth,
				OneTimeDownload = oneTimeDownload,
				CheckIntervalSeconds = checkIntervalSeconds,
				RelativeDownloadDirectory = General.CleanFileName(globalThreadID),
				PageBaseFileName = General.CleanFileName(siteHelper.GetThreadName()),
				Description = globalThreadID
			});
		}

		public static ThreadWatcher Create(ThreadWatcherConfig config) {
			return new ThreadWatcher(config);
		}

		public string PageURL { get; }

		public string PageHost { get; }

		public string GlobalThreadID { get; }

		public string BaseDownloadDirectory { get; }

		public string PageBaseFileName { get; }

		public DateTime AddedOn { get; }

		public string PageAuth {
			get { lock (_settingsSync) { return _pageAuth; } }
			set { SetSetting(out _pageAuth, value, true, false); }
		}

		public string ImageAuth {
			get { lock (_settingsSync) { return _imageAuth; } }
			set { SetSetting(out _imageAuth, value, true, false); }
		}

		public bool OneTimeDownload {
			get { lock (_settingsSync) { return _oneTimeDownload; } }
			set { SetSetting(out _oneTimeDownload, value, true, false); }
		}

		public int CheckIntervalSeconds {
			get { lock (_settingsSync) { return _checkIntervalSeconds; } }
			set {
				lock (_settingsSync) {
					value = Math.Max(value, _minCheckIntervalSeconds);
					int changeAmount = value - _checkIntervalSeconds;
					_checkIntervalSeconds = value;
					NextCheckTicks += changeAmount * 1000;
				}
			}
		}

		public string ThreadDownloadDirectory {
			get { lock (_settingsSync) { return _threadDownloadDirectory; } }
		}

		private bool IsThreadDownloadDirectoryPendingRename {
			get {
				lock (_settingsSync) {
					return !String.Equals(_threadDownloadDirectory, GetDesiredThreadDownloadDirectory(), StringComparison.Ordinal);
				}
			}
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
				}
				if (IsThreadDownloadDirectoryPendingRename) {
					TryRenameThreadDownloadDirectory(false);
				}
			}
		}

		public DateTime? LastImageOn {
			get { lock (_settingsSync) { return _lastImageOn; } }
			set { lock (_settingsSync) { _lastImageOn = value; } }
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
				_isStopping = false;
				_stopReason = StopReason.Other;
				_hasRun = true;
				_hasInitialized = false;
				_nextCheckWorkItem = _workScheduler.AddItem(TickCount.Now, Check, PageHost);
			}
		}

		public void Stop(StopReason reason) {
			bool stoppingNow = false;
			bool checkFinished = false;
			List<Action> downloadAborters = null;
			lock (_settingsSync) {
				if (!IsStopping) {
					stoppingNow = true;
					_isStopping = true;
					_stopReason = reason;
					_hasRun = true;
					if (_nextCheckWorkItem != null) {
						_workScheduler.RemoveItem(_nextCheckWorkItem);
						_nextCheckWorkItem = null;
					}
					checkFinished = _checkFinishedEvent.WaitOne(0, false);
					if (checkFinished) {
						_isWaiting = false;
					}
					else {
						lock (_downloadAborters) {
							downloadAborters = new List<Action>(_downloadAborters.Values);
						}
					}
				}
			}
			if (stoppingNow) {
				if (checkFinished) {
					OnStopStatus(EventArgs.Empty);
				}
				else {
					foreach (Action abortDownload in downloadAborters) {
						abortDownload();
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
			get { lock (_settingsSync) { return _isStopping; } }
		}

		public StopReason StopReason {
			get { lock (_settingsSync) { return _stopReason; } }
		}

		public event EventHandler<ThreadWatcher, DownloadStatusEventArgs> DownloadStatus;

		public event EventHandler<ThreadWatcher, EventArgs> WaitStatus;

		public event EventHandler<ThreadWatcher, EventArgs> StopStatus;

		public event EventHandler<ThreadWatcher, EventArgs> ThreadDownloadDirectoryRename;

		public event EventHandler<ThreadWatcher, EventArgs> FoundNewImage;

		public event EventHandler<ThreadWatcher, DownloadStartEventArgs> DownloadStart;

		public event EventHandler<ThreadWatcher, DownloadProgressEventArgs> DownloadProgress;

		public event EventHandler<ThreadWatcher, DownloadEndEventArgs> DownloadEnd;

		private void OnDownloadStatus(DownloadStatusEventArgs e) {
			try { DownloadStatus?.Invoke(this, e); } catch { }
		}

		private void OnWaitStatus(EventArgs e) {
			try { WaitStatus?.Invoke(this, e); } catch { }
		}

		private void OnStopStatus(EventArgs e) {
			try { StopStatus?.Invoke(this, e); } catch { }
		}

		private void OnThreadDownloadDirectoryRename(EventArgs e) {
			try { ThreadDownloadDirectoryRename?.Invoke(this, e); } catch { }
		}

		private void OnFoundNewImage(EventArgs e) {
			try { FoundNewImage?.Invoke(this, e); } catch { }
		}

		private void OnDownloadStart(DownloadStartEventArgs e) {
			try { DownloadStart?.Invoke(this, e); } catch { }
		}

		private void OnDownloadProgress(DownloadProgressEventArgs e) {
			try { DownloadProgress?.Invoke(this, e); } catch { }
		}

		private void OnDownloadEnd(DownloadEndEventArgs e) {
			try { DownloadEnd?.Invoke(this, e); } catch { }
		}

		private List<PageInfo> _pageList;
		private HashSet<string> _imageDiskFileNames;
		private Dictionary<string, DownloadInfo> _completedImages;
		private Dictionary<string, DownloadInfo> _completedThumbs;
		private bool _anyPendingRetries;
		private string _previousImageDir;
		private int _maxImageFileNameLength;

		private void Check() {
			try {
				try {
					lock (_settingsSync) {
						_nextCheckWorkItem = null;
						_checkFinishedEvent.Reset();
						_isWaiting = false;

						if (!_hasInitialized) {
							_pageList = new List<PageInfo> {
								new PageInfo {
									URL = PageURL
								}
							};
							_imageDiskFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
							_completedImages = new Dictionary<string, DownloadInfo>(StringComparer.OrdinalIgnoreCase);
							_completedThumbs = new Dictionary<string, DownloadInfo>(StringComparer.OrdinalIgnoreCase);

							Directory.CreateDirectory(_threadDownloadDirectory);

							_hasInitialized = true;
						}
					}
				}
				catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) {
					Stop(StopReason.IOError);
				}

				string threadDir = ThreadDownloadDirectory;
				string imageDir = ThreadDownloadDirectory;
				string thumbDir = Path.Combine(ThreadDownloadDirectory, "thumbs");

				Queue<ImageInfo> pendingImages = new Queue<ImageInfo>();
				Queue<ThumbnailInfo> pendingThumbs = new Queue<ThumbnailInfo>();

				foreach (PageInfo pageInfo in _pageList) {
					// Reset the fresh flag on all of the pages before downloading starts so that
					// they're valid even if stopping before all the pages have been downloaded
					pageInfo.IsFresh = false;
				}

				int pageIndex = 0;
				bool anyPageSkipped = false;
				OnDownloadStatus(new DownloadStatusEventArgs(DownloadType.Page, 0, _pageList.Count));
				while (pageIndex < _pageList.Count && !IsStopping) {
					string saveFileName = PageBaseFileName + (pageIndex == 0 ? "" : $"_{pageIndex + 1}") + ".html";
					HTMLParser pageParser = null;

					PageInfo pageInfo = _pageList[pageIndex];
					pageInfo.Path = Path.Combine(threadDir, saveFileName);

					ManualResetEvent downloadEndEvent = new ManualResetEvent(false);
					DownloadPageEndCallback downloadEnd = (result, content, lastModifiedTime, encoding) => {
						if (result == DownloadResult.Completed) {
							pageInfo.IsFresh = true;
							pageParser = new HTMLParser(content);
							pageInfo.CacheTime = lastModifiedTime;
							pageInfo.Encoding = encoding;
							pageInfo.ReplaceList = Settings.SaveThumbnails ? new List<ReplaceInfo>() : null;
						}
						downloadEndEvent.Set();
					};
					DownloadPageAsync(pageInfo.Path, pageInfo.URL, PageAuth, _anyPendingRetries ? null : pageInfo.CacheTime, downloadEnd);
					downloadEndEvent.WaitOne();
					downloadEndEvent.Close();

					if (pageParser == null) {
						anyPageSkipped = true;
					}
					else {
						SiteHelper siteHelper = SiteHelper.CreateByURL(pageInfo.URL);

						siteHelper.SetHTMLParser(pageParser);

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
										fileName = baseFileNameNoExtension + (iSuffix == 1 ? "" : $"_{iSuffix}") + baseExtension;
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
				if (!anyPageSkipped) {
					_anyPendingRetries = false;
				}

				MillisecondsUntilNextCheck = CheckIntervalSeconds * 1000;

				if (pendingImages.Count != 0 && !IsStopping) {
					LastImageOn = DateTime.UtcNow;
					OnFoundNewImage(EventArgs.Empty);

					if (imageDir != _previousImageDir) {
						_maxImageFileNameLength = General.GetMaximumFileNameLength(imageDir);
						_previousImageDir = imageDir;
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

						void ConfigureSaveFileName(string fileName) {
							saveFileNameNoExtension = Path.GetFileNameWithoutExtension(fileName);
							saveExtension = Path.GetExtension(fileName);
						}

					MakeImagePath:
						if (!String.IsNullOrEmpty(image.OriginalFileName) && (image.ForceOriginalFileName || (Settings.UseOriginalFileNames && !pathTooLong))) {
							ConfigureSaveFileName(image.OriginalFileName);
						}
						else {
							ConfigureSaveFileName(image.FileName);
						}

						int iSuffix = 1;
						bool fileNameTaken;
						string saveFileName;
						do {
							saveFileName = saveFileNameNoExtension + (iSuffix == 1 ? "" : $"_{iSuffix}") + saveExtension;
							savePath = Path.Combine(imageDir, saveFileName);
							fileNameTaken = _imageDiskFileNames.Contains(saveFileName);
							iSuffix++;
						}
						while (fileNameTaken);

						if (saveFileName.Length > _maxImageFileNameLength && !pathTooLong) {
							pathTooLong = true;
							goto MakeImagePath;
						}
						_imageDiskFileNames.Add(saveFileName);

						HashType hashType = Settings.VerifyImageHashes ? image.HashType : HashType.None;
						ManualResetEvent downloadEndEvent = new ManualResetEvent(false);
						DownloadFileEndCallback onDownloadEnd = (result) => {
							if (result == DownloadResult.Completed || result == DownloadResult.Skipped) {
								lock (_completedImages) {
									_completedImages[image.FileName] = new DownloadInfo {
										FileName = saveFileName,
										Skipped = (result == DownloadResult.Skipped)
									};
									if (result == DownloadResult.Completed) {
										completedImageCount++;
									}
									else if (result == DownloadResult.Skipped) {
										totalImageCount--;
									}
									OnDownloadStatus(new DownloadStatusEventArgs(DownloadType.Image, completedImageCount, totalImageCount));
								}
							}
							if (result == DownloadResult.Skipped || result == DownloadResult.RetryLater) {
								lock (_imageDiskFileNames) {
									_imageDiskFileNames.Remove(saveFileName);
									if (result == DownloadResult.RetryLater) {
										_anyPendingRetries = true;
									}
								}
							}
							downloadEndEvent.Set();
						};
						downloadEndEvents.Add(downloadEndEvent);
						DownloadFileAsync(savePath, image.URL, ImageAuth, image.Referer, hashType, image.Hash, onDownloadEnd);
					}
					foreach (ManualResetEvent downloadEndEvent in downloadEndEvents) {
						downloadEndEvent.WaitOne();
						downloadEndEvent.Close();
					}
				}

				if (Settings.SaveThumbnails) {
					if (pendingThumbs.Count != 0 && !IsStopping) {
						try {
							Directory.CreateDirectory(thumbDir);
						}
						catch {
							Stop(StopReason.IOError);
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
										if (result == DownloadResult.Completed) {
											completedThumbCount++;
										}
										else if (result == DownloadResult.Skipped) {
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
							downloadEndEvent.Close();
						}
					}

					if (!IsStopping || StopReason != StopReason.IOError) {
						foreach (PageInfo pageInfo in _pageList) {
							if (!pageInfo.IsFresh) continue;
							HTMLParser htmlParser = new HTMLParser(File.ReadAllText(pageInfo.Path, pageInfo.Encoding));
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
							General.AddOtherReplaces(htmlParser, pageInfo.URL, pageInfo.ReplaceList);
							using (StreamWriter sw = new StreamWriter(pageInfo.Path, false, pageInfo.Encoding)) {
								General.WriteReplacedString(htmlParser.PreprocessedHTML, pageInfo.ReplaceList, sw);
							}
							if (htmlParser.FindEndTag("html") != null && File.Exists(pageInfo.Path + ".bak")) {
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

			if (IsThreadDownloadDirectoryPendingRename) {
				TryRenameThreadDownloadDirectory(true);
			}
			lock (_settingsSync) {
				_checkFinishedEvent.Set();
				if (!IsStopping) {
					_nextCheckWorkItem = _workScheduler.AddItem(NextCheckTicks, Check, PageHost);
					_isWaiting = MillisecondsUntilNextCheck > 0;
				}
			}
			if (IsStopping) {
				OnStopStatus(EventArgs.Empty);
			}
			else if (IsWaiting) {
				OnWaitStatus(EventArgs.Empty);
			}
		}

		private string GetDesiredThreadDownloadDirectory() {
			string name;
			switch (Settings.DownloadFolderNamingMethod) {
				case DownloadFolderNamingMethod.Description:
					name = _description;
					break;
				default:
					name = GlobalThreadID;
					break;
			}
			return Path.Combine(BaseDownloadDirectory, General.CleanFileName(name));
		}

		private void TryRenameThreadDownloadDirectory(bool calledFromCheck) {
			bool renamedDir = false;
			lock (_settingsSync) {
				if ((!calledFromCheck && !_checkFinishedEvent.WaitOne(0, false)) ||
					(IsStopping && (StopReason == StopReason.IOError || StopReason == StopReason.Exiting)))
				{
					return;
				}
				try {
					string destDir = GetDesiredThreadDownloadDirectory();
					if (String.Equals(destDir, _threadDownloadDirectory, StringComparison.Ordinal)) return;
					if (String.Equals(destDir, _threadDownloadDirectory, StringComparison.OrdinalIgnoreCase)) {
						Directory.Move(_threadDownloadDirectory, destDir + " Temp");
						_threadDownloadDirectory = destDir + " Temp";
						renamedDir = true;
					}
					Directory.Move(_threadDownloadDirectory, destDir);
					_threadDownloadDirectory = destDir;
					renamedDir = true;
				}
				catch { }
			}
			if (renamedDir) {
				OnThreadDownloadDirectoryRename(EventArgs.Empty);
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
				string httpContentType = null;
				DateTime? lastModifiedTime = null;
				Encoding encoding = null;
				string content = null;

				Action<DownloadResult> endTryDownload = (result) => {
					connectionManager.ReleaseConnectionGroupName(connectionGroupName);
					onDownloadEnd(result, content, lastModifiedTime, encoding);
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
						httpContentType = response.ContentType;
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
						encoding = General.DetectHTMLEncoding(pageBytes, httpContentType);
						content = encoding.GetString(pageBytes);
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
						else if (ex is IOException || ex is UnauthorizedAccessException) {
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
						if (ex is DirectoryNotFoundException || ex is UnauthorizedAccessException) {
							// Fatal IO error, stop
							Stop(StopReason.IOError);
							endTryDownload(DownloadResult.Skipped);
						}
						else if (ex is HTTP404Exception) {
							// Fatal problem with this file, skip
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
