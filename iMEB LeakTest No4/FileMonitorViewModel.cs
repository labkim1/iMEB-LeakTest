// -------------------------------------------------------------------------------------
// Alex Wiese
// Copyright (c) 2014
// 
// Assembly:	LiveLogViewer4
// Filename:	FileMonitorViewModel.cs
// Created:	29/10/2014 11:06 AM
// Author:	Alex Wiese
// 
// -------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;

using TLog.Properties;

namespace TLog
{
    public class FileMonitorViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly IFileMonitor _fileMonitor;
        private string _filePath;
        private bool _fileExists;
        private string _fileName;
        private bool _isFrozen;
        private string _contents;
        private Encoding _encoding;
        private string _encodingName;
        public event Action<FileMonitorViewModel> Renamed;

        public string EncodingName
        {
            get { return _encodingName; }
            set
            {
                if (value == _encodingName) return;
                _encodingName = value;
                _encoding = Encoding.GetEncoding(value);
                _fileMonitor.UpdateEncoding(_encoding);
                OnPropertyChanged();
            }
        }

        protected virtual void OnRenamed()
        {
            var handler = Renamed;
            if (handler != null)
            {
                handler(this);
            }
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="T:System.Object" /> class.
        /// </summary>
        public FileMonitorViewModel(string filePath, string fileName, string encodingName, bool bufferedRead)
        {
            Preconditions.CheckNotEmptyOrNull(filePath);
            Preconditions.CheckNotEmptyOrNull(fileName);

            _filePath = filePath;
            _fileName = fileName;

            FileExists = File.Exists(filePath);

            try
            {
                //_encoding = Encoding.GetEncoding(encodingName);
                _encoding = Encoding.GetEncoding(51949);
            }
            catch (Exception)
            {
                MessageBox.Show("Could not use encoding " + encodingName + ". Defaulting to UTF8 instead.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            _encoding = _encoding ?? Encoding.UTF8;
            _encodingName = _encoding.BodyName;

            this._fileMonitor = new TimedFileMonitor(filePath, _encoding) { BufferedRead = bufferedRead };
            this._fileMonitor.FileUpdated += FileMonitorOnFileUpdated;
            this._fileMonitor.FileDeleted += FileMonitorOnFileDeleted;
            this._fileMonitor.FileCreated += FileMonitorOnFileCreated;
            this._fileMonitor.FileRenamed += FileMonitorOnFileRenamed;
        }

        private void FileMonitorOnFileRenamed(IFileMonitor fileMonitor, string newPath)
        {
            this._filePath = newPath;
            OnRenamed();
        }

        public bool FileExists
        {
            get { return _fileExists; }
            set
            {
                if (value.Equals(_fileExists)) return;
                _fileExists = value;
                OnPropertyChanged();
            }
        }

        public string FileName
        {
            get { return _fileName; }
            set
            {
                if (value == _fileName) return;
                _fileName = value;
                OnPropertyChanged();
            }
        }

        public string Contents
        {
            get { return _contents; }
            set
            {
                if (value == _contents) return;
                _contents = value;
                OnPropertyChanged();
            }
        }

        public string FilePath
        {
            get { return _filePath; }
        }

        public bool IsFrozen
        {
            get { return _isFrozen; }
            set
            {
                if (value.Equals(_isFrozen)) return;
                _isFrozen = value;
                OnPropertyChanged();
            }
        }

        public bool BufferedRead
        {
            get { return _fileMonitor.BufferedRead; }
            set { _fileMonitor.BufferedRead = value; }
        }

        public event Action<FileMonitorViewModel> Updated;

        protected virtual void OnUpdated()
        {
            var handler = Updated;
            if (handler != null) handler(this);
        }

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        private void FileMonitorOnFileCreated(IFileMonitor obj)
        {
            OnUpdated();
            this.FileExists = true;
        }

        private void FileMonitorOnFileDeleted(IFileMonitor obj)
        {
            OnUpdated();
            this.FileExists = false;
        }

        private void FileMonitorOnFileUpdated(IFileMonitor fileMonitor, string contents)
        {
            OnUpdated();
            Contents += contents;
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_fileMonitor != null)
                {
                    _fileMonitor.Dispose();
                }
            }
        }

    }
}