namespace Novolis.IO.Ndjson;

/// <summary>Seekable access to a newline-delimited JSON file.</summary>
public interface INdjsonDocument : IAsyncDisposable
{
    /// <summary>The file currently being read.</summary>
    FileInfo File { get; }

    /// <summary>Number of complete records discovered so far.</summary>
    long RecordCount { get; }

    /// <summary>Reads a bounded record slice.</summary>
    Task<NdjsonSlice> ReadAsync(
        long skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>Refreshes the sparse index from the current file identity.</summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
