namespace Novolis.IO.Indexing;

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
