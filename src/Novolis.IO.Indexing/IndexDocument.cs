namespace Novolis.IO.Indexing;

/// <summary>A source document known to the index (no format assumptions).</summary>
/// <param name="Id">Stable document id.</param>
/// <param name="Location">Optional host location (path, URI, blob key, …).</param>
public sealed record IndexDocument(string Id, string? Location = null);
