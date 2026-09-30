namespace Novolis.IO.Watching;

/// <summary>Watches a single file for change, rename, or delete.</summary>
public sealed class SingleFileWatcher : IDisposable
{
    FileSystemWatcher? _watcher;
    string? _watchedPath;
    readonly object _gate = new();

    /// <summary>Raised with the full path when the watched file changes.</summary>
    public event Action<string>? FileChanged;

    /// <summary>Starts watching <paramref name="filePath"/>.</summary>
    public void Watch(string filePath)
    {
        lock (_gate)
        {
            StopInternal();
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;

            var dir = Path.GetDirectoryName(filePath);
            var name = Path.GetFileName(filePath);
            if (dir is null)
                return;

            _watchedPath = Path.GetFullPath(filePath);
            _watcher = new FileSystemWatcher(dir, name)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };
            _watcher.Changed += OnChanged;
            _watcher.Renamed += OnRenamed;
            _watcher.Deleted += OnChanged;
        }
    }

    /// <summary>Stops watching.</summary>
    public void Stop()
    {
        lock (_gate)
            StopInternal();
    }

    void OnRenamed(object sender, RenamedEventArgs e) => Raise(e.FullPath);

    void OnChanged(object sender, FileSystemEventArgs e) => Raise(e.FullPath);

    void Raise(string path)
    {
        if (_watchedPath is null)
            return;
        if (!string.Equals(Path.GetFullPath(path), _watchedPath, StringComparison.OrdinalIgnoreCase))
            return;
        FileChanged?.Invoke(_watchedPath);
    }

    void StopInternal()
    {
        if (_watcher is null)
            return;
        _watcher.EnableRaisingEvents = false;
        _watcher.Changed -= OnChanged;
        _watcher.Renamed -= OnRenamed;
        _watcher.Deleted -= OnChanged;
        _watcher.Dispose();
        _watcher = null;
        _watchedPath = null;
    }

    /// <inheritdoc />
    public void Dispose() => Stop();
}
