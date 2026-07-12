using System;
using System.IO;
using System.Threading;

namespace Promptino.Platform;

public interface IScriptWatcher : IDisposable
{
    void StartWatching(string filePath, Action onChanged);
    void StopWatching();
}

public sealed class ScriptWatcher : IScriptWatcher
{
    private FileSystemWatcher? _watcher;
    private Action? _onChanged;
    private string? _filePath;
    private Timer? _debounceTimer;
    private bool _stopped;
    private readonly object _lock = new();

    public void StartWatching(string filePath, Action onChanged)
    {
        StopWatching();

        if (string.IsNullOrWhiteSpace(filePath)) return;

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(filePath);
        }
        catch (Exception)
        {
            return;
        }

        var directory = Path.GetDirectoryName(fullPath);
        var fileName = Path.GetFileName(fullPath);
        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName)) return;

        if (!Directory.Exists(directory)) return;

        try
        {
            lock (_lock)
            {
                _filePath = fullPath;
                _onChanged = onChanged;
                _stopped = false;
                _watcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
                };
                _watcher.Changed += OnFileChanged;
                _watcher.Renamed += OnFileChanged;
                _watcher.Error += OnWatcherError;
                _watcher.EnableRaisingEvents = true;
            }
        }
        catch (Exception)
        {
            StopWatching();
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        lock (_lock)
        {
            if (_stopped) return;

            if (_debounceTimer == null)
            {
                _debounceTimer = new Timer(DebounceTimerCallback, null, 500, Timeout.Infinite);
            }
            else
            {
                _debounceTimer.Change(500, Timeout.Infinite);
            }
        }
    }

    private void DebounceTimerCallback(object? state)
    {
        Action? cb;
        lock (_lock)
        {
            if (_stopped) return;
            cb = _onChanged;
        }
        cb?.Invoke();
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        StopWatching();
    }

    public void StopWatching()
    {
        lock (_lock)
        {
            _stopped = true;

            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Changed -= OnFileChanged;
                _watcher.Renamed -= OnFileChanged;
                _watcher.Error -= OnWatcherError;
                _watcher.Dispose();
                _watcher = null;
            }

            if (_debounceTimer != null)
            {
                _debounceTimer.Dispose();
                _debounceTimer = null;
            }

            _onChanged = null;
            _filePath = null;
        }
    }

    public void Dispose() => StopWatching();
}
