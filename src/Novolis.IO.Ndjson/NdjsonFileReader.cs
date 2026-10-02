namespace Novolis.IO.Ndjson;

/// <summary>Opens NDJSON files without scanning them to EOF.</summary>
public sealed class NdjsonFileReader
{
    private readonly NdjsonOpenOptions _options;

    /// <summary>Creates a reader with optional indexing and slice bounds.</summary>
    public NdjsonFileReader(NdjsonOpenOptions? options = null)
    {
        _options = options ?? new NdjsonOpenOptions();
        _options.Validate();
    }

    /// <summary>Captures file identity and opens a lazy document.</summary>
    public async Task<INdjsonDocument> OpenAsync(
        FileInfo file,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        cancellationToken.ThrowIfCancellationRequested();

        file.Refresh();
        if (!file.Exists)
            throw new FileNotFoundException("The NDJSON file was not found.", file.FullName);

        if ((file.Attributes & FileAttributes.Directory) != 0)
            throw new ArgumentException("The supplied path is a directory.", nameof(file));

        var normalizedFile = new FileInfo(Path.GetFullPath(file.FullName));
        var indexer = new NdjsonFileIndexer(_options);
        var index = await indexer.CreateInitialAsync(normalizedFile, cancellationToken).ConfigureAwait(false);
        return new NdjsonDocument(normalizedFile, _options, indexer, index);
    }
}
