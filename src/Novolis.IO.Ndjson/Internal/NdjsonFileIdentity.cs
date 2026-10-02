namespace Novolis.IO.Ndjson;

internal readonly record struct NdjsonFileIdentity(
    long Length,
    DateTime LastWriteTimeUtc,
    ulong PrefixHash,
    ulong TailHash)
{
    public static async Task<NdjsonFileIdentity> ReadAsync(
        FileInfo file,
        CancellationToken cancellationToken)
    {
        file.Refresh();
        if (!file.Exists)
            throw new FileNotFoundException("The NDJSON file was not found.", file.FullName);

        var length = file.Length;
        var lastWriteTimeUtc = file.LastWriteTimeUtc;
        var prefixLength = Math.Min(length, 64 * 1024);

        await using var stream = Open(file);
        var prefixHash = await ReadHashAsync(stream, prefixLength, cancellationToken).ConfigureAwait(false);
        var tailStart = Math.Max(0, length - (64 * 1024));
        stream.Seek(tailStart, SeekOrigin.Begin);
        var tailHash = await ReadHashAsync(stream, length - tailStart, cancellationToken).ConfigureAwait(false);

        return new NdjsonFileIdentity(length, lastWriteTimeUtc, prefixHash, tailHash);
    }

    public static async Task<bool> HasSameBoundaryAsync(
        FileInfo file,
        NdjsonFileIdentity previous,
        CancellationToken cancellationToken)
    {
        if (previous.Length == 0)
            return true;

        await using var stream = Open(file);
        var prefixHash = await ReadHashAsync(
            stream,
            Math.Min(previous.Length, 64 * 1024),
            cancellationToken).ConfigureAwait(false);
        if (prefixHash != previous.PrefixHash)
            return false;

        var tailStart = Math.Max(0, previous.Length - (64 * 1024));
        stream.Seek(tailStart, SeekOrigin.Begin);
        var tailHash = await ReadHashAsync(
            stream,
            previous.Length - tailStart,
            cancellationToken).ConfigureAwait(false);
        return tailHash == previous.TailHash;
    }

    public bool CanContinueWith(NdjsonFileIdentity current)
    {
        if (current.Length < Length
            || current.PrefixHash != PrefixHash
            || current.TailHash != TailHash)
            return false;

        return current.Length > Length || current.LastWriteTimeUtc == LastWriteTimeUtc;
    }

    private static FileStream Open(FileInfo file) =>
        new(
            file.FullName,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.ReadWrite | FileShare.Delete,
                BufferSize = 16 * 1024,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            });

    private static async Task<ulong> ReadHashAsync(
        FileStream stream,
        long length,
        CancellationToken cancellationToken)
    {
        ulong hash = 14695981039346656037UL;
        var remaining = length;
        var buffer = new byte[16 * 1024];
        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var requested = (int)Math.Min(buffer.Length, remaining);
            var read = await stream.ReadAsync(buffer.AsMemory(0, requested), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                break;

            for (var index = 0; index < read; index++)
            {
                hash ^= buffer[index];
                hash *= 1099511628211UL;
            }

            remaining -= read;
        }

        return hash;
    }
}
