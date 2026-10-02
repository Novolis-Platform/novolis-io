namespace Novolis.IO.Ndjson;

internal sealed class NdjsonDocument : INdjsonDocument
{
    private readonly FileInfo _file;
    private readonly NdjsonOpenOptions _options;
    private readonly NdjsonFileIndexer _indexer;
    private readonly NdjsonRecordReader _recordReader;
    private readonly SemaphoreSlim _indexGate = new(1, 1);
    private NdjsonIndexState _index;
    private long _recordCount;
    private int _disposed;

    public NdjsonDocument(
        FileInfo file,
        NdjsonOpenOptions options,
        NdjsonFileIndexer indexer,
        NdjsonIndexState index)
    {
        _file = file;
        _options = options;
        _indexer = indexer;
        _recordReader = new NdjsonRecordReader();
        _index = index;
    }

    public FileInfo File => _file;

    public long RecordCount => Volatile.Read(ref _recordCount);

    internal long LastScanBytes => _index.LastScanBytes;

    public async Task<NdjsonSlice> ReadAsync(
        long skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        ValidateSlice(skip, take);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        var requiredRecordCount = checked(skip + (long)take + 1);
        NdjsonIndexSnapshot snapshot;

        await _indexGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            await _indexer.UpdateAsync(
                _file,
                _index,
                requiredRecordCount,
                cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _recordCount, _index.RecordCount);
            snapshot = _index.Snapshot();
        }
        finally
        {
            _indexGate.Release();
        }

        var parsed = await _recordReader.ReadAsync(
            _file,
            snapshot,
            skip,
            take,
            cancellationToken).ConfigureAwait(false);

        var hasMore = parsed.Count > take;
        var records = hasMore
            ? parsed.Take(take).ToArray()
            : parsed;

        return new NdjsonSlice(skip, take, records, skip > 0, hasMore);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        await _indexGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            await _indexer.UpdateAsync(
                _file,
                _index,
                long.MaxValue,
                cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _recordCount, _index.RecordCount);
        }
        finally
        {
            _indexGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        await _indexGate.WaitAsync().ConfigureAwait(false);
        _indexGate.Release();
        _indexGate.Dispose();
    }

    private void ValidateSlice(long skip, int take)
    {
        if (skip < 0)
            throw new ArgumentOutOfRangeException(nameof(skip), "The slice offset cannot be negative.");

        if (take <= 0)
            throw new ArgumentOutOfRangeException(nameof(take), "The slice size must be positive.");

        if (take > _options.MaxTake)
            throw new ArgumentOutOfRangeException(nameof(take), $"The slice size cannot exceed {_options.MaxTake:N0}.");

        if (skip > long.MaxValue - take - 1)
            throw new ArgumentOutOfRangeException(nameof(skip), "The requested slice exceeds the supported record range.");
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(NdjsonDocument));
    }
}
