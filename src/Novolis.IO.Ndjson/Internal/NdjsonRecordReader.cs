using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Novolis.IO.Ndjson;

internal sealed class NdjsonRecordReader
{
    private const int BufferSize = 64 * 1024;
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public async Task<IReadOnlyList<NdjsonRecord>> ReadAsync(
        FileInfo file,
        NdjsonIndexSnapshot index,
        long skip,
        int take,
        CancellationToken cancellationToken)
    {
        var entry = FindEntry(index.Entries, skip);
        var recordNumber = entry.RecordNumber;
        var lineStart = entry.ByteOffset;
        var position = entry.ByteOffset;
        var records = new List<NdjsonRecord>(Math.Min(take + 1, 1024));

        await using var stream = Open(file);
        stream.Seek(entry.ByteOffset, SeekOrigin.Begin);

        var readBuffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        using var line = new PooledByteBuffer(NdjsonOpenOptions.MaxRecordBytes);
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = await stream.ReadAsync(readBuffer.AsMemory(0, readBuffer.Length), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    break;

                for (var indexOffset = 0; indexOffset < read; indexOffset++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var value = readBuffer[indexOffset];
                    position++;

                    if (value != (byte)'\n')
                    {
                        line.Append(value);
                        continue;
                    }

                    if (recordNumber >= skip)
                    {
                        records.Add(ParseRecord(recordNumber, lineStart, line));
                        if (records.Count > take)
                            return records;
                    }

                    recordNumber++;
                    lineStart = position;
                    line.Reset();
                }
            }

            return records;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(readBuffer);
        }
    }

    private static NdjsonRecord ParseRecord(
        long recordNumber,
        long byteOffset,
        PooledByteBuffer line)
    {
        if (line.IsExceeded)
        {
            return new NdjsonRecord(
                recordNumber,
                byteOffset,
                Json: null,
                Raw: $"<record exceeds {NdjsonOpenOptions.MaxRecordBytes:N0} bytes>",
                Error: new JsonException($"The record exceeds the {NdjsonOpenOptions.MaxRecordBytes:N0}-byte limit."));
        }

        var content = line.GetContent();
        if (recordNumber == 0
            && content.Length >= 3
            && content.Span[0] == 0xEF
            && content.Span[1] == 0xBB
            && content.Span[2] == 0xBF)
        {
            content = content[3..];
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            return new NdjsonRecord(
                recordNumber,
                byteOffset,
                document.RootElement.Clone(),
                Raw: null,
                Error: null);
        }
        catch (JsonException error)
        {
            return new NdjsonRecord(
                recordNumber,
                byteOffset,
                Json: null,
                Raw: Utf8.GetString(content.Span),
                Error: error);
        }
    }

    private static NdjsonIndexEntry FindEntry(
        IReadOnlyList<NdjsonIndexEntry> entries,
        long requestedRecord)
    {
        var low = 0;
        var high = entries.Count - 1;
        var best = entries[0];

        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var candidate = entries[middle];
            if (candidate.RecordNumber <= requestedRecord)
            {
                best = candidate;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return best;
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
                Options = FileOptions.Asynchronous,
            });
}
