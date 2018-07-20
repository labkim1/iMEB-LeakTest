// -------------------------------------------------------------------------------------
// Alex Wiese
// Copyright (c) 2014
// 
// Assembly:	LiveLogViewer4
// Filename:	FileMonitor.cs
// Created:	29/10/2014 8:46 AM
// Author:	Alex Wiese
// 
// -------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace TLog
{
    /// <summary>
    /// 프로퍼티 그리드용
    /// </summary>
    public abstract class FileMonitor : IFileMonitor, IDisposable
    {
        private const int DefaultBufferSize = 1048576;
        private readonly object _syncRoot = new object();
        private bool _bufferedRead = true;
        private Encoding _encoding;
        private bool _fileExists;
        protected string _filePath;
        private bool _isDisposed;
        private int _readBufferSize = DefaultBufferSize;
        private Stream _stream;
        private StreamReader _streamReader;

        protected FileMonitor(string filePath, Encoding encoding = null)
        {
            encoding = encoding ?? Encoding.UTF8;

            Preconditions.CheckNotEmptyOrNull(filePath, "filePath");

            _filePath = filePath;
            _encoding = encoding;

            // Track file existence for the delete/replace events
            _fileExists = GetFileExists();

            if (_fileExists)
            {
                OpenFile(filePath);
            }
        }

        #region IFileMonitor Members

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Occurs when the file being monitored is updated.
        /// </summary>
        public event Action<IFileMonitor, string> FileUpdated;

        /// <summary>
        ///     Occurs when the file being monitored is deleted.
        /// </summary>
        public event Action<IFileMonitor> FileDeleted;

        /// <summary>
        ///     Occurs when the file being monitored is recreated.
        /// </summary>
        public event Action<IFileMonitor> FileCreated;

        /// <summary>
        ///     Occurs when the file being monitored is renamed.
        /// </summary>
        public event Action<IFileMonitor, string> FileRenamed;

        /// <summary>
        ///     Gets the path of the file being monitored.
        /// </summary>
        /// <value>
        ///     The file path.
        /// </value>
        public string FilePath
        {
            get { return _filePath; }
        }

        /// <summary>
        ///     Gets or sets the length of the read buffer.
        /// </summary>
        /// <value>
        ///     The length of the read buffer.
        /// </value>
        public int ReadBufferSize
        {
            get { return _readBufferSize; }
            set
            {
                // Buffer cannot be 0 or negative
                Preconditions.CheckArgumentRange("value", value, 1, int.MaxValue);
                _readBufferSize = value;
            }
        }

        /// <summary>
        ///     Refreshes the <see cref="IFileMonitor" /> checking for any changes.
        /// </summary>
        /// <returns></returns>
        public Task RefreshAsync()
        {
            return Task.Run(() => CheckForChanges());
        }

        /// <summary>
        ///     Updates the encoding used to read the file.
        /// </summary>
        /// <param name="encoding">The encoding.</param>
        public void UpdateEncoding(Encoding encoding)
        {
            lock (_syncRoot)
            {
                _encoding = encoding;
                OpenFile(_filePath);
            }
        }

        /// <summary>
        ///     Gets or sets a value indicating whether a buffer is used read the changes in blocks.
        /// </summary>
        /// <value>
        ///     <c>true</c> if a buffer is used; otherwise, <c>false</c>.
        /// </value>
        public bool BufferedRead
        {
            get { return _bufferedRead; }
            set { _bufferedRead = value; }
        }

        #endregion

        private void OpenFile(string filePath)
        {
            // Dispose existing stream
            DisposeStream();

            // File is opened for read only, and shared for read, write and delete
            try
            {
                _stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                _streamReader = new StreamReader(_stream, _encoding);

                // Start at the end of the file
                _streamReader.BaseStream.Seek(0, SeekOrigin.End);
            }
            catch (Exception e) { }
        }

        protected virtual void OnFileRenamed(string newName)
        {
            var handler = FileRenamed;
            if (handler != null)
            {
                handler(this, newName);
            }
        }

        protected virtual void OnFileUpdated(string updatedContent)
        {
            var handler = FileUpdated;

            if (handler != null)
            {
                handler(this, updatedContent);
            }
        }

        protected virtual void OnFileDeleted()
        {
            var handler = FileDeleted;

            if (handler != null)
            {
                handler(this);
            }
        }

        protected virtual void OnFileCreated()
        {
            var handler = FileCreated;

            if (handler != null)
            {
                handler(this);
            }
        }

        private bool GetFileExists()
        {
            return File.Exists(_filePath);
        }

        /// <summary>
        ///     Checks for changes to the file.
        /// </summary>
        protected virtual void CheckForChanges()
        {
            lock (_syncRoot)
            {
                if (_fileExists && !GetFileExists())
                {
                    // File has been deleted
                    _fileExists = false;
                    OnFileDeleted();
                }
                else if (!_fileExists && GetFileExists())
                {
                    // File has been created
                    OpenFile(_filePath);
                    _fileExists = true;
                    OnFileCreated();
                }

                if (_streamReader == null)
                {
                    // File is not open
                    return;
                }

                var baseStream = _streamReader.BaseStream;

                if (baseStream.Position > baseStream.Length)
                {
                    // File is smaller than the current position
                    // Seek to the end
                    baseStream.Seek(0, SeekOrigin.End);
                }

                if (_streamReader.EndOfStream)
                    return;

                if (BufferedRead)
                {
                    var buffer = new char[ReadBufferSize];
                    int charCount;

                    while ((charCount = _streamReader.Read(buffer, 0, ReadBufferSize)) > 0)
                    {
                        var appendedContent = new string(buffer, 0, charCount);

                        if (!string.IsNullOrEmpty(appendedContent))
                        {
                            OnFileUpdated(appendedContent);
                        }
                    }
                }
                else
                {
                    var appendedContent = _streamReader.ReadToEnd();

                    if (!string.IsNullOrEmpty(appendedContent))
                    {
                        OnFileUpdated(appendedContent);
                    }
                }
            }
        }


        /// <summary>
        ///     Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing">
        ///     <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only
        ///     unmanaged resources.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
            {
                return;
            }

            if (disposing)
            {
                DisposeStream();
            }

            _isDisposed = true;
        }

        private void DisposeStream()
        {
            if (_streamReader != null)
            {
                _streamReader.Dispose();
            }

            if (_stream != null)
            {
                _stream.Dispose();
            }
        }

        /// <summary>
        ///     Finalizes an instance of the <see cref="FileMonitor" /> class.
        /// </summary>
        ~FileMonitor()
        {
            Dispose(false);
        }
    }
}