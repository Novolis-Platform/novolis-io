using System.Buffers;

namespace Novolis.IO.Ndjson;

internal sealed class NdjsonFileIndexer(NdjsonOpenOptions options)
{
    private const int BufferSize = 64 * 1024;

    public async Task<NdjsonIndexState> CreateInitialAsync(
        FileInfo file,
        CancellationToken cancellationToken)
    {
        var identity = await NdjsonFileIdentity.ReadAsync(file, cancellationToken).ConfigureAwait(false);
        return new NdjsonIndexState(identity);
    }

    public async Task UpdateAsync(
        FileInfo file,
        NdjsonIndexState state,
        long requiredRecordCount,
        CancellationToken cancellationToken)
    {
        var currentIdentity = await NdjsonFileIdentity.ReadAsync(file, cancellationToken).ConfigureAwait(false);
        var canContinue = currentIdentity.Length > state.Identity.Length
            ? await NdjsonFileIdentity.HasSameBoundaryAsync(file, state.Identity, cancellationToken).ConfigureAwait(false)
            : state.Identity.CanContinueWith(currentIdentity);
        if (!canContinue)
        {
            state = Reset(state, currentIdentity);
        }
        else
        {
            state.Identity = currentIdentity;
        }

        if (state.RecordCount >= requiredRecordCount
            || state.LastCompleteRecordOffset >= currentIdentity.Length)
        {
            state.IndexedLength = Math.Min(state.IndexedLength, currentIdentity.Length);
            return;
        }

        await ScanAsync(file, state, requiredRecordCount, cancellationToken).ConfigureAwait(false);
    }

    private async Task ScanAsync(
        FileInfo file,
        NdjsonIndexState state,
        long requiredRecordCount,
        CancellationToken cancellationToken)
    {
        var startOffset = state.LastCompleteRecordOffset;
        var targetLength = state.Identity.Length;
        if (startOffset >= targetLength)
        {
            state.LastScanBytes = 0;
            state.IndexedLength = targetLength;
            return;
        }

        await using var stream = Open(file);
        stream.Seek(startOffset, SeekOrigin.Begin);

        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        var position = startOffset;
        state.LastScanBytes = 0;
        try
        {
            while (position < targetLength)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var requested = (int)Math.Min(buffer.Length, targetLength - position);
                var read = await stream.ReadAsync(buffer.AsMemory(0, requested), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    break;

                state.LastScanBytes = position + read - startOffset;
                for (var index = 0; index < read; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    position++;

                    if (buffer[index] != (byte)'\n')
                        continue;

                    state.RecordCount++;
                    state.LastCompleteRecordOffset = position;
                    state.IndexedLength = position;

                    if (state.RecordCount % options.IndexInterval == 0)
                        AddEntry(state, state.RecordCount, position);

                    if (state.RecordCount >= requiredRecordCount)
                        return;
                }
            }

            state.IndexedLength = position;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static NdjsonIndexState Reset(
        NdjsonIndexState current,
        NdjsonFileIdentity identity)
    {
        current.Identity = identity;
        current.IndexedLength = 0;
        current.RecordCount = 0;
        current.LastCompleteRecordOffset = 0;
        current.Entries.Clear();
        current.Entries.Add(new NdjsonIndexEntry(0, 0));
        return current;
    }

    private static void AddEntry(
        NdjsonIndexState state,
        long recordNumber,
        long byteOffset)
    {
        if (state.Entries.Count > 0
            && state.Entries[^1] is var previous
            && previous.RecordNumber == recordNumber)
        {
            state.Entries[^1] = new NdjsonIndexEntry(recordNumber, byteOffset);
            return;
        }

        state.Entries.Add(new NdjsonIndexEntry(recordNumber, byteOffset));
    }

    private static FileStream Open(FileInfo file) =>
        new(
            file.FullName,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.ReadWrite | FileShare.Delete,
                BufferSize = BufferSize,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            });
}
