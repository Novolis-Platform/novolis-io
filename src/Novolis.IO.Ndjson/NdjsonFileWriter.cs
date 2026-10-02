using System.Text.Json;

namespace Novolis.IO.Ndjson;

/// <summary>
/// Appends complete UTF-8 JSON lines without taking an OS file lock.
/// Cross-process and cross-pod writers must still coordinate ownership.
/// </summary>
public sealed class NdjsonFileWriter : IDisposable
{
    private readonly JsonSerializerOptions _options;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private int _disposed;

    /// <summary>Creates an append writer for <paramref name="path"/>.</summary>
    public NdjsonFileWriter(string path, JsonSerializerOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = System.IO.Path.GetFullPath(path);
        _options = options ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
    }

    /// <summary>Creates an append writer for <paramref name="file"/>.</summary>
    public NdjsonFileWriter(FileInfo file, JsonSerializerOptions? options = null)
        : this((file ?? throw new ArgumentNullException(nameof(file))).FullName, options)
    {
    }

    /// <summary>Absolute path of the append-only file.</summary>
    public string Path { get; }

    /// <summary>Appends one serialized value and an LF terminator.</summary>
    public ValueTask AppendAsync<T>(
        T value,
        CancellationToken cancellationToken = default) =>
        AppendSerializedAsync(
            JsonSerializer.SerializeToUtf8Bytes(value, _options),
            cancellationToken);

    /// <summary>
    /// Appends one serialized value and an LF terminator synchronously.
    /// Set <paramref name="flushToDisk"/> when durable delivery is required.
    /// </summary>
    public void Append<T>(T value, bool flushToDisk = false)
    {
        ThrowIfDisposed();
        _writeGate.Wait();
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, _options);
            WriteLine(bytes, flushToDisk);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <summary>Appends an already serialized UTF-8 JSON value and an LF.</summary>
    public ValueTask AppendJsonAsync(
        ReadOnlyMemory<byte> json,
        CancellationToken cancellationToken = default) =>
        AppendSerializedAsync(json, cancellationToken);

    /// <summary>Appends an already serialized UTF-8 JSON value and an LF synchronously.</summary>
    public void AppendJson(ReadOnlySpan<byte> json, bool flushToDisk = false)
    {
        ThrowIfDisposed();
        _writeGate.Wait();
        try
        {
            WriteLine(json, flushToDisk);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            _writeGate.Dispose();
    }

    private async ValueTask AppendSerializedAsync(
        ReadOnlyMemory<byte> json,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WriteLineAsync(json, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private void WriteLine(ReadOnlySpan<byte> json, bool flushToDisk)
    {
        var payload = CreatePayload(json);
        using var stream = Open(FileOptions.None);
        stream.Write(payload);
        stream.Flush(flushToDisk);
    }

    private async ValueTask WriteLineAsync(
        ReadOnlyMemory<byte> json,
        CancellationToken cancellationToken)
    {
        var payload = CreatePayload(json.Span);
        await using var stream = Open(FileOptions.Asynchronous);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static byte[] CreatePayload(ReadOnlySpan<byte> json)
    {
        var payload = new byte[json.Length + 1];
        json.CopyTo(payload);
        payload[^1] = (byte)'\n';
        return payload;
    }

    private FileStream Open(FileOptions options) =>
        new(
            Path,
            new FileStreamOptions
            {
                Mode = FileMode.Append,
                Access = FileAccess.Write,
                Share = FileShare.ReadWrite | FileShare.Delete,
                BufferSize = 4 * 1024,
                Options = options,
            });

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
}
