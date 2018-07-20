// -------------------------------------------------------------------------------------
// Alex Wiese
// Copyright (c) 2014
// 
// Assembly:	LiveLogViewer4
// Filename:	TimedFileMonitor.cs
// Created:	29/10/2014 8:53 AM
// Author:	Alex Wiese
// 
// -------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Text;
using System.Timers;

namespace TLog
{
    /// <summary>
    /// 프로퍼티 그리드용
    /// </summary>
    public class TimedFileMonitor : FileMonitor, IDisposable
    {
        private readonly Timer _timer;
        private readonly FileSystemWatcher _watcher;
        
        public TimedFileMonitor(string filePath, Encoding encoding = null)
            : base(filePath, encoding)
        {
            this._timer = new Timer(2000);
            this._timer.Elapsed += TimerCallback;
            this._timer.Start();

            _watcher = CreateFileWatcher(filePath);
        }

        private FileSystemWatcher CreateFileWatcher(string filePath)
        {
            var watcher = new FileSystemWatcher(Path.GetDirectoryName(filePath), Path.GetFileName(filePath));

            watcher.Changed += WatcherOnChanged;
            watcher.Created += WatcherOnCreated;
            watcher.Deleted += WatcherOnDeleted;
            watcher.Renamed += WatcherOnRenamed;
            watcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size;
            watcher.EnableRaisingEvents = true;

            return watcher;
        }

        private void WatcherOnRenamed(object sender, RenamedEventArgs renamedEventArgs)
        {
            this._filePath = renamedEventArgs.FullPath;
            _watcher.Filter = renamedEventArgs.Name;
            OnFileRenamed(renamedEventArgs.FullPath);

            CheckForChanges();
        }

        private void WatcherOnDeleted(object sender, FileSystemEventArgs fileSystemEventArgs)
        {
            CheckForChanges();
        }

        private void WatcherOnCreated(object sender, FileSystemEventArgs fileSystemEventArgs)
        {
            CheckForChanges();
        }

        private void WatcherOnChanged(object sender, FileSystemEventArgs fileSystemEventArgs)
        {
            CheckForChanges();
        }

        private void TimerCallback(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            CheckForChanges();
        }

        /// <summary>
        ///     Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing">
        ///     <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only
        ///     unmanaged resources.
        /// </param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_timer != null)
                {
                    _timer.Dispose();
                }

                if (_watcher != null)
                {
                    _watcher.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}