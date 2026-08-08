namespace Novolis.IO.Indexing;

/// <summary>Opaque span inside a document (caller-defined units: bytes, chars, or tokens).</summary>
/// <param name="Start">Start offset.</param>
/// <param name="Length">Length from <paramref name="Start"/>.</param>
public readonly record struct IndexSpan(int Start, int Length);

/// <summary>What an index endpoint refers to.</summary>
public enum IndexEndpointKind
{
    /// <summary>A document registered in the index.</summary>
    Document = 0,

    /// <summary>A conceptual entry (person, place, topic, … — caller-defined).</summary>
    Entry = 1,
}

/// <summary>One end of a directed <see cref="IndexLink"/>.</summary>
/// <param name="Kind">Document or entry.</param>
/// <param name="Id">Stable id within that kind.</param>
public readonly record struct IndexEndpoint(IndexEndpointKind Kind, string Id);

/// <summary>A source document known to the index (no format assumptions).</summary>
/// <param name="Id">Stable document id.</param>
/// <param name="Location">Optional host location (path, URI, blob key, …).</param>
public sealed record IndexDocument(string Id, string? Location = null);

/// <summary>A conceptual entry with aliases and open facets.</summary>
/// <param name="Id">Stable entry id.</param>
/// <param name="Title">Optional display title.</param>
/// <param name="Aliases">Alternate names that resolve to this entry.</param>
/// <param name="Facets">Author/host-defined key → values (no closed taxonomy).</param>
public sealed record IndexEntry(
    string Id,
    string? Title = null,
    IReadOnlyList<string>? Aliases = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Facets = null);

/// <summary>Directed link between two endpoints with an open relation string.</summary>
/// <param name="From">Source endpoint.</param>
/// <param name="To">Target endpoint.</param>
/// <param name="Relation">Caller-defined relation (e.g. <c>mentions</c>, <c>see-also</c>).</param>
/// <param name="ProvenanceDocumentId">Optional document where the link was observed.</param>
/// <param name="Span">Optional span within that document.</param>
public sealed record IndexLink(
    IndexEndpoint From,
    IndexEndpoint To,
    string? Relation = null,
    string? ProvenanceDocumentId = null,
    IndexSpan? Span = null);
