namespace Novolis.IO.Watching;

/// <summary>Debounces <see cref="SingleFileWatcher.FileChanged"/> notifications.</summary>
public sealed class DebouncedFileWatcher : IDisposable
{
    readonly SingleFileWatcher _inner = new();
    readonly int _debounceMs;
    CancellationTokenSource? _cts;

    /// <summary>Creates a debounced watcher.</summary>
    public DebouncedFileWatcher(int debounceMilliseconds = 300)
    {
        _debounceMs = Math.Max(0, debounceMilliseconds);
        _inner.FileChanged += OnInnerChanged;
    }

    /// <summary>Raised after the debounce window with no further changes.</summary>
    public event Action<string>? FileChanged;

    /// <summary>Starts watching.</summary>
    public void Watch(string filePath) => _inner.Watch(filePath);

    /// <summary>Stops watching.</summary>
    public void Stop() => _inner.Stop();

    void OnInnerChanged(string path)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_debounceMs, token).ConfigureAwait(false);
                if (!token.IsCancellationRequested)
                    FileChanged?.Invoke(path);
            }
            catch (OperationCanceledException) { /* ignore */ }
        }, token);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _inner.Dispose();
    }
}
