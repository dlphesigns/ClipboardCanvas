﻿using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Search;

namespace ClipboardCanvas.Helpers
{
    public sealed class ChangeRegisteredEventArgs : EventArgs
    {
        public readonly StorageLibraryChangeTracker filesystemChangeTracker;

        public readonly StorageLibraryChangeReader filesystemChangeReader;

        public ChangeRegisteredEventArgs(StorageLibraryChangeTracker filesystemChangeTracker, StorageLibraryChangeReader filesystemChangeReader)
        {
            this.filesystemChangeTracker = filesystemChangeTracker;
            this.filesystemChangeReader = filesystemChangeReader;
        }
    }

    public sealed class ChangeRegisteredEventArgs2 : EventArgs
    {
        public string? OldPath { get; }

        public string FullPath { get; }

        public WatcherChangeTypes ChangeType { get; }

        public ChangeRegisteredEventArgs2(string fullPath, WatcherChangeTypes changeType, string oldPath = null)
        {
            FullPath = fullPath;
            ChangeType = changeType;
            OldPath = oldPath;
        }
    }

    public sealed class FilesystemChangeWatcher2 : IDisposable
    {
        private readonly FileSystemWatcher _fileSystemWatcher;

        public event EventHandler<ChangeRegisteredEventArgs2> OnChangeRegisteredEvent;

        public FilesystemChangeWatcher2(string path)
        {
            _fileSystemWatcher = new FileSystemWatcher(path);
            _fileSystemWatcher.EnableRaisingEvents = true;

            _fileSystemWatcher.Created += FileSystemWatcher_Created;
            _fileSystemWatcher.Deleted += FileSystemWatcher_Deleted;
            _fileSystemWatcher.Renamed += FileSystemWatcher_Renamed;
            _fileSystemWatcher.Changed += FileSystemWatcher_Changed;
        }

        private void FileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            OnChangeRegisteredEvent?.Invoke(this, new(e.FullPath, e.ChangeType));
        }

        private void FileSystemWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            OnChangeRegisteredEvent?.Invoke(this, new(e.FullPath, e.ChangeType, e.OldFullPath));
        }

        private void FileSystemWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            OnChangeRegisteredEvent?.Invoke(this, new(e.FullPath, e.ChangeType));
        }

        private void FileSystemWatcher_Created(object sender, FileSystemEventArgs e)
        {
            OnChangeRegisteredEvent?.Invoke(this, new(e.FullPath, e.ChangeType));
        }

        public void Dispose()
        {
            _fileSystemWatcher.Created -= FileSystemWatcher_Created;
            _fileSystemWatcher.Deleted -= FileSystemWatcher_Deleted;
            _fileSystemWatcher.Dispose();
        }
    }

    public sealed class FilesystemChangeWatcher : IDisposable
    {
        private bool _isStarted;

        private readonly StorageFolder _watchedFolder;

        private StorageItemQueryResult _filesystemWatcherQuery;

        private StorageLibraryChangeTracker _filesystemChangeTracker;

        private StorageLibraryChangeReader _filesystemChangeReader;

        public event EventHandler<ChangeRegisteredEventArgs> OnChangeRegisteredEvent;

        private FilesystemChangeWatcher(StorageFolder watchedFolder)
        {
            this._watchedFolder = watchedFolder;
        }

        public static async Task<FilesystemChangeWatcher> CreateNew(StorageFolder folderToWatch)
        {
            var filesystemChangeWatcher = new FilesystemChangeWatcher(folderToWatch);
            await filesystemChangeWatcher.StartWatching();

            return filesystemChangeWatcher;
        }

        private async Task<bool> StartWatching()
        {
            try
            {
                _isStarted = true;

                // 1. Prepare query - listen for changes
                QueryOptions queryOptions = new QueryOptions(CommonFolderQuery.DefaultQuery);
                queryOptions.FolderDepth = FolderDepth.Shallow;
                queryOptions.IndexerOption = IndexerOption.UseIndexerWhenAvailable;

                _filesystemWatcherQuery = _watchedFolder.CreateItemQueryWithOptions(queryOptions);

                // Indicate to the system the app is ready to change track
                await _filesystemWatcherQuery.GetItemsAsync(0, 1);

                _filesystemWatcherQuery.ContentsChanged += FilesystemWatcherQuery_ContentsChanged;

                // 2. Get change tracker
                _filesystemChangeTracker = _watchedFolder.TryGetChangeTracker();
                _filesystemChangeTracker.Enable();

                _filesystemChangeReader = _filesystemChangeTracker.GetChangeReader();

                return true;
            }
            catch
            {
                _isStarted = false;
            }

            return false;
        }

        private async void FilesystemWatcherQuery_ContentsChanged(IStorageQueryResultBase sender, object args)
        {
            OnChangeRegisteredEvent?.Invoke(this, new ChangeRegisteredEventArgs(_filesystemChangeTracker, _filesystemChangeReader));
        }

        #region IDisposable

        public void Dispose()
        {
            if (_isStarted)
            {
                _isStarted = false;
                _filesystemWatcherQuery.ContentsChanged -= FilesystemWatcherQuery_ContentsChanged;
            }
        }

        #endregion
    }
}
