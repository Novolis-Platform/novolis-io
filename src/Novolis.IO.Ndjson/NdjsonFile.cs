namespace Novolis.IO.Ndjson;

/// <summary>Validated file identity used when composing NDJSON services.</summary>
public sealed class NdjsonFile(FileInfo file)
{
    /// <summary>The file represented by this value.</summary>
    public FileInfo File { get; } = file ?? throw new ArgumentNullException(nameof(file));
}
