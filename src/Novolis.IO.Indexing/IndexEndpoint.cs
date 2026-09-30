namespace Novolis.IO.Indexing;

/// <summary>One end of a directed <see cref="IndexLink"/>.</summary>
/// <param name="Kind">Document or entry.</param>
/// <param name="Id">Stable id within that kind.</param>
public readonly record struct IndexEndpoint(IndexEndpointKind Kind, string Id);
