namespace Novolis.IO.Indexing;

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
