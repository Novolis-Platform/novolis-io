namespace Novolis.IO.Ndjson;

/// <summary>Opens NDJSON files without scanning them to EOF.</summary>
public sealed class NdjsonFileReader
{
    private readonly NdjsonOpenOptions _defaultOptions;

    /// <summary>Creates a reader with optional indexing and slice bounds.</summary>
    public NdjsonFileReader(NdjsonOpenOptions? options = null)
    {
        _defaultOptions = options ?? new NdjsonOpenOptions();
        _defaultOptions.Validate();
    }

    /// <summary>Captures file identity and opens a lazy document.</summary>
    public async Task<INdjsonDocument> OpenAsync(
        FileInfo file,
        NdjsonOpenOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        cancellationToken.ThrowIfCancellationRequested();
        var openOptions = options ?? _defaultOptions;
        openOptions.Validate();

        file.Refresh();
        if (!file.Exists)
            throw new FileNotFoundException("The NDJSON file was not found.", file.FullName);

        if ((file.Attributes & FileAttributes.Directory) != 0)
            throw new ArgumentException("The supplied path is a directory.", nameof(file));

        var normalizedFile = new FileInfo(Path.GetFullPath(file.FullName));
        var indexer = new NdjsonFileIndexer(openOptions);
        var index = await indexer.CreateInitialAsync(normalizedFile, cancellationToken).ConfigureAwait(false);
        return new NdjsonDocument(normalizedFile, openOptions, indexer, index);
    }

    /// <summary>Opens a file using the reader defaults and cancellation.</summary>
    public Task<INdjsonDocument> OpenAsync(
        FileInfo file,
        CancellationToken cancellationToken) =>
        OpenAsync(file, options: null, cancellationToken: cancellationToken);
}
