using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using SeriousView.Core.Abstractions;

namespace SeriousView.Platform;

/// <summary>
/// <see cref="IDocumentWatcher"/> over <see cref="FileSystemWatcher"/> (M14 live-reload).
/// One watcher per DIRECTORY with a ref-counted set of file names: editor save strategies are
/// temp-write + rename/replace dances, and a directory watcher sees the whole dance land on the
/// watched name (a per-file filter can miss rename-onto-name). Raw events are debounced per
/// path with a last-kind-wins window, so a Delete+Create pair (File.Replace) coalesces into one
/// <see cref="DocumentChangeKind.Changed"/> while a lone delete stays Removed. Events fire on
/// timer/watcher threads — consumers marshal to the UI thread. Fail-open: an unwatchable path
/// (network drive quirks) simply gets no live-reload.
/// </summary>
public sealed class DocumentWatcher : IDocumentWatcher
{
    private readonly TimeSpan _debounce;
    private readonly object _gate = new();
    private readonly Dictionary<string, DirectoryWatch> _directories = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public event Action<string, DocumentChangeKind>? Changed;

    public DocumentWatcher() : this(TimeSpan.FromMilliseconds(300)) { }

    /// <summary>Test seam: a short debounce keeps the integration tests fast.</summary>
    public DocumentWatcher(TimeSpan debounce) => _debounce = debounce;

    public void Watch(string path)
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            try
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(path));
                var fileName = Path.GetFileName(path);
                if (string.IsNullOrEmpty(directory) || fileName.Length == 0)
                    return;

                if (!_directories.TryGetValue(directory, out var watch))
                    _directories[directory] = watch = new DirectoryWatch(this, directory);
                watch.AddRef(fileName, path);
            }
            catch
            {
                // Fail-open: no live-reload for this path, never an exception to the caller.
            }
        }
    }

    public void Unwatch(string path)
    {
        lock (_gate)
        {
            try
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(path));
                var fileName = Path.GetFileName(path);
                if (directory is null || !_directories.TryGetValue(directory, out var watch))
                    return;

                if (watch.RemoveRef(fileName))
                {
                    watch.Dispose();
                    _directories.Remove(directory);
                }
            }
            catch
            {
                // Symmetric with Watch — best-effort.
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            foreach (var watch in _directories.Values)
                watch.Dispose();
            _directories.Clear();
        }
    }

    private void RaiseChanged(string fullPath, DocumentChangeKind kind)
        => Changed?.Invoke(fullPath, kind);

    /// <summary>One FileSystemWatcher + the ref-counted file names of one directory.</summary>
    private sealed class DirectoryWatch : IDisposable
    {
        private readonly DocumentWatcher _owner;
        private readonly FileSystemWatcher _fsw;
        private readonly object _gate = new();
        private readonly Dictionary<string, (int Refs, string FullPath)> _files = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, (Timer Timer, DocumentChangeKind Kind)> _pending = new(StringComparer.OrdinalIgnoreCase);

        public DirectoryWatch(DocumentWatcher owner, string directory)
        {
            _owner = owner;
            _fsw = new FileSystemWatcher(directory)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                IncludeSubdirectories = false,
                InternalBufferSize = 64 * 1024,
            };
            _fsw.Changed += (_, e) => Touch(e.Name, DocumentChangeKind.Changed);
            _fsw.Created += (_, e) => Touch(e.Name, DocumentChangeKind.Changed);
            _fsw.Deleted += (_, e) => Touch(e.Name, DocumentChangeKind.Removed);
            _fsw.Renamed += (_, e) =>
            {
                Touch(e.OldName, DocumentChangeKind.Removed); // renamed away
                Touch(e.Name, DocumentChangeKind.Changed);    // renamed onto a watched name
            };
            // Buffer overflow = "you missed something": over-notify every watched file —
            // a spurious reload is idempotent, a missed change is a stale view.
            _fsw.Error += (_, _) => TouchAll();
            _fsw.EnableRaisingEvents = true;
        }

        public void AddRef(string fileName, string fullPath)
        {
            lock (_gate)
            {
                _files[fileName] = _files.TryGetValue(fileName, out var entry)
                    ? (entry.Refs + 1, entry.FullPath)
                    : (1, fullPath);
            }
        }

        /// <summary>Returns true when the directory has no watched files left.</summary>
        public bool RemoveRef(string fileName)
        {
            lock (_gate)
            {
                if (_files.TryGetValue(fileName, out var entry))
                {
                    if (entry.Refs > 1)
                        _files[fileName] = (entry.Refs - 1, entry.FullPath);
                    else
                        _files.Remove(fileName);
                }

                return _files.Count == 0;
            }
        }

        private void Touch(string? fileName, DocumentChangeKind kind)
        {
            if (fileName is null)
                return;

            lock (_gate)
            {
                if (!_files.TryGetValue(fileName, out var entry))
                    return;

                if (_pending.TryGetValue(fileName, out var pending))
                {
                    pending.Timer.Change(_owner._debounce, Timeout.InfiniteTimeSpan);
                    _pending[fileName] = (pending.Timer, kind); // last kind in the window wins
                }
                else
                {
                    var timer = new Timer(_ => Flush(fileName, entry.FullPath), null,
                        _owner._debounce, Timeout.InfiniteTimeSpan);
                    _pending[fileName] = (timer, kind);
                }
            }
        }

        private void TouchAll()
        {
            lock (_gate)
            {
                foreach (var fileName in new List<string>(_files.Keys))
                    Touch(fileName, DocumentChangeKind.Changed);
            }
        }

        private void Flush(string fileName, string fullPath)
        {
            DocumentChangeKind kind;
            lock (_gate)
            {
                if (!_pending.TryGetValue(fileName, out var pending))
                    return;
                kind = pending.Kind;
                pending.Timer.Dispose();
                _pending.Remove(fileName);
            }

            _owner.RaiseChanged(fullPath, kind);
        }

        public void Dispose()
        {
            _fsw.Dispose();
            lock (_gate)
            {
                foreach (var pending in _pending.Values)
                    pending.Timer.Dispose();
                _pending.Clear();
            }
        }
    }
}
